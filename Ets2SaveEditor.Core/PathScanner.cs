using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Ets2SaveEditor.Core
{
    public enum GameType { ETS2, ATS }
    public enum ProfileType { Local, SteamCloud, SteamLocal, Custom }

    public class GameProfile
    {
        public string Id { get; set; }          // Directory name in hex
        public string Name { get; set; }        // Decoded UTF-8 profile name
        public string Path { get; set; }        // Full folder path
        public GameType Game { get; set; }
        public ProfileType Type { get; set; }

        /// <summary>Steam account ID from userdata\{id}\ (32-bit).</summary>
        public uint? SteamAccountId { get; set; }

        /// <summary>SteamID64 = accountId + 76561197960265728.</summary>
        public ulong? SteamId64 =>
            SteamAccountId.HasValue
                ? 76561197960265728UL + SteamAccountId.Value
                : null;

        /// <summary>Steam Cloud or Documents\steam_profiles.</summary>
        public bool IsSteamSource =>
            Type == ProfileType.SteamCloud || Type == ProfileType.SteamLocal;

        public override string ToString()
        {
            string kind = Type switch
            {
                ProfileType.SteamCloud => "Steam Cloud",
                ProfileType.SteamLocal => "Steam",
                ProfileType.Custom => "Custom",
                _ => "Local"
            };
            return $"{Name} ({kind})";
        }
    }

    public class SaveGame
    {
        public string FolderName { get; set; }  // "autosave", "1", "2" etc
        public string DisplayName { get; set; } // Read from info.sii
        public string Path { get; set; }        // Full path to save directory
        public DateTime FileTime { get; set; }  // File time from info.sii or directory write time

        public override string ToString()
        {
            return $"{DisplayName} ({FileTime:dd.MM.yyyy HH:mm:ss})";
        }
    }

    public static class PathScanner
    {
        public static string DecodeHexProfileName(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return hex;
            if (!Regex.IsMatch(hex, @"^[a-fA-F0-9]+$")) return hex;
            if (hex.Length % 2 != 0) return hex;
            try
            {
                byte[] bytes = new byte[hex.Length / 2];
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                }
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return hex;
            }
        }

        public static string GetSteamPath()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    string path = key?.GetValue("SteamPath") as string;
                    if (!string.IsNullOrEmpty(path))
                    {
                        path = path.Replace('/', '\\');
                        if (Directory.Exists(path)) return path;
                    }
                }
            }
            catch { }

            string fallback = @"C:\Program Files (x86)\Steam";
            if (Directory.Exists(fallback)) return fallback;

            fallback = @"C:\Program Files\Steam";
            if (Directory.Exists(fallback)) return fallback;

            return null;
        }

        public static List<GameProfile> ScanProfiles(string customPath = null, GameType? customPathGame = null)
        {
            var profiles = new List<GameProfile>();

            // 1. Scan Local Profiles
            string myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            ScanLocalDirectory(profiles, Path.Combine(myDocs, "Euro Truck Simulator 2"), GameType.ETS2);
            ScanLocalDirectory(profiles, Path.Combine(myDocs, "American Truck Simulator"), GameType.ATS);

            // 2. Scan Steam Cloud Profiles
            string steamPath = GetSteamPath();
            if (!string.IsNullOrEmpty(steamPath))
            {
                string userdata = Path.Combine(steamPath, "userdata");
                if (Directory.Exists(userdata))
                {
                    foreach (var userDir in Directory.GetDirectories(userdata))
                    {
                        uint? accountId = TryParseSteamAccountId(Path.GetFileName(userDir));

                        string ets2Cloud = Path.Combine(userDir, "227300", "remote", "profiles");
                        ScanCloudDirectory(profiles, ets2Cloud, GameType.ETS2, accountId);

                        string atsCloud = Path.Combine(userDir, "270880", "remote", "profiles");
                        ScanCloudDirectory(profiles, atsCloud, GameType.ATS, accountId);
                    }
                }
            }

            // 3. Scan Custom Path
            if (!string.IsNullOrEmpty(customPath) && Directory.Exists(customPath))
            {
                GameType guessedGame = customPathGame
                    ?? (customPath.ToLowerInvariant().Contains("american") || customPath.ToLowerInvariant().Contains("ats")
                        ? GameType.ATS
                        : GameType.ETS2);
                uint? customAccount = TryParseSteamAccountIdFromPath(customPath);
                ScanDirectory(profiles, customPath, guessedGame, ProfileType.Custom, customAccount);
            }

            EnrichSteamAccountIds(profiles);

            return profiles;
        }

        /// <summary>Converts a 32-bit Steam account ID to SteamID64.</summary>
        public static ulong ToSteamId64(uint accountId) => 76561197960265728UL + accountId;

        public static uint? TryParseSteamAccountId(string folderName)
        {
            if (uint.TryParse(folderName, out uint id) && id > 0)
                return id;
            return null;
        }

        /// <summary>Finds userdata\{accountId}\ in a full path.</summary>
        public static uint? TryParseSteamAccountIdFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string[] parts = path.Replace('/', '\\').Split('\\');
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (parts[i].Equals("userdata", StringComparison.OrdinalIgnoreCase))
                    return TryParseSteamAccountId(parts[i + 1]);
            }
            return null;
        }

        private static void EnrichSteamAccountIds(List<GameProfile> profiles)
        {
            // Fill IDs from path when missing (e.g. custom path under userdata).
            foreach (var p in profiles)
            {
                if (p.SteamAccountId.HasValue) continue;
                p.SteamAccountId = TryParseSteamAccountIdFromPath(p.Path);
            }

            // Documents\...\steam_profiles often mirror cloud profiles — copy ID by hex folder name.
            foreach (var p in profiles)
            {
                if (p.SteamAccountId.HasValue) continue;
                if (p.Path == null ||
                    p.Path.IndexOf("steam_profiles", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                foreach (var other in profiles)
                {
                    if (!other.SteamAccountId.HasValue) continue;
                    if (other.Game != p.Game) continue;
                    if (!string.Equals(other.Id, p.Id, StringComparison.OrdinalIgnoreCase))
                        continue;
                    p.SteamAccountId = other.SteamAccountId;
                    break;
                }
            }
        }

        private static void ScanLocalDirectory(List<GameProfile> profiles, string gameFolder, GameType game)
        {
            if (!Directory.Exists(gameFolder)) return;

            string standardProfiles = Path.Combine(gameFolder, "profiles");
            ScanDirectory(profiles, standardProfiles, game, ProfileType.Local, null);

            string steamProfiles = Path.Combine(gameFolder, "steam_profiles");
            ScanDirectory(profiles, steamProfiles, game, ProfileType.SteamLocal, null);
        }

        private static void ScanCloudDirectory(List<GameProfile> profiles, string cloudFolder, GameType game, uint? steamAccountId)
        {
            ScanDirectory(profiles, cloudFolder, game, ProfileType.SteamCloud, steamAccountId);
        }

        private static void ScanDirectory(List<GameProfile> profiles, string folder, GameType game, ProfileType type, uint? steamAccountId)
        {
            if (!Directory.Exists(folder)) return;

            try
            {
                foreach (var dir in Directory.GetDirectories(folder))
                {
                    string dirName = Path.GetFileName(dir);
                    if (Regex.IsMatch(dirName, @"^[a-fA-F0-9]+$"))
                    {
                        if (Directory.Exists(Path.Combine(dir, "save")) || Directory.Exists(Path.Combine(dir, "remote")))
                        {
                            profiles.Add(new GameProfile
                            {
                                Id = dirName,
                                Name = DecodeHexProfileName(dirName),
                                Path = dir,
                                Game = game,
                                Type = type,
                                SteamAccountId = steamAccountId
                            });
                        }
                    }
                }
            }
            catch { }
        }

        public static List<SaveGame> ScanSaves(GameProfile profile)
        {
            var saves = new List<SaveGame>();
            if (profile == null || !Directory.Exists(profile.Path)) return saves;

            string saveRoot = Path.Combine(profile.Path, "save");
            if (!Directory.Exists(saveRoot))
            {
                saveRoot = profile.Path;
                if (!Directory.Exists(Path.Combine(saveRoot, "autosave")) && Directory.Exists(Path.Combine(profile.Path, "remote")))
                {
                    saveRoot = Path.Combine(profile.Path, "remote");
                }
            }

            if (!Directory.Exists(saveRoot)) return saves;

            try
            {
                foreach (var dir in Directory.GetDirectories(saveRoot))
                {
                    string dirName = Path.GetFileName(dir);
                    string gameSii = Path.Combine(dir, "game.sii");
                    if (File.Exists(gameSii))
                    {
                        var save = new SaveGame
                        {
                            FolderName = dirName,
                            Path = dir,
                            DisplayName = dirName,
                            FileTime = Directory.GetLastWriteTime(dir)
                        };

                        ParseSaveInfo(save);
                        saves.Add(save);
                    }
                }
            }
            catch { }

            saves.Sort((a, b) => b.FileTime.CompareTo(a.FileTime));
            return saves;
        }

        private static void ParseSaveInfo(SaveGame save)
        {
            string infoSii = Path.Combine(save.Path, "info.sii");
            if (!File.Exists(infoSii)) return;

            try
            {
                byte[] data = File.ReadAllBytes(infoSii);
                string text = null;

                if (data.Length > 4 && Encoding.ASCII.GetString(data, 0, 4) == "ScsC")
                {
                    try
                    {
                        text = SaveEngine.DecryptBytesToString(data);
                    }
                    catch
                    {
                        return;
                    }
                }
                else
                {
                    text = Encoding.UTF8.GetString(data);
                }

                if (!string.IsNullOrEmpty(text))
                {
                    // Newer saves: name: 333  (bare token)
                    // Older / localized: name: "AutoSave" or name: "\xd0\x9c..."
                    // Rare legacy: save_name: "..."
                    var nameMatch = Regex.Match(text, @"(?m)^\s*(?:save_name|name):\s*(?:""([^""]*)""|(\S+))");
                    if (nameMatch.Success)
                    {
                        string rawName = nameMatch.Groups[1].Success
                            ? nameMatch.Groups[1].Value
                            : nameMatch.Groups[2].Value;
                        if (!string.IsNullOrWhiteSpace(rawName))
                            save.DisplayName = DecodeEscapedString(rawName);
                    }

                    var timeMatch = Regex.Match(text, @"file_time:\s*(\d+)");
                    if (timeMatch.Success && long.TryParse(timeMatch.Groups[1].Value, out long seconds))
                    {
                        try
                        {
                            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                            save.FileTime = epoch.AddSeconds(seconds).ToLocalTime();
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch { }
        }

        public static string DecodeEscapedString(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            if (!input.Contains("\\x")) return input;

            try
            {
                return Regex.Replace(input, @"(?:\\x[a-fA-F0-9]{2})+", match =>
                {
                    string hexSeq = match.Value.Replace("\\x", "");
                    byte[] bytes = new byte[hexSeq.Length / 2];
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        bytes[i] = Convert.ToByte(hexSeq.Substring(i * 2, 2), 16);
                    }
                    return Encoding.UTF8.GetString(bytes);
                });
            }
            catch
            {
                return input;
            }
        }
    }
}
