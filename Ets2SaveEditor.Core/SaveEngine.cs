using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Ets2SaveEditor.Core
{
    public static class SaveEngine
    {
        private const int MaxTimestampedBackups = 5;

        /// <summary>
        /// Decrypts/reads a save file for editing. Does not modify the original encrypted file
        /// when using the integrated decryptor. External tools run against a temp copy.
        /// </summary>
        public static string DecryptFile(string filePath, string externalDecryptorPath = null)
        {
            if (!File.Exists(filePath)) return null;

            try
            {
                byte[] header = new byte[4];
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (fs.Length >= 4)
                        fs.Read(header, 0, 4);
                }

                string signature = Encoding.ASCII.GetString(header);
                if (signature == "ScsC" || signature == "SIIB")
                {
                    if (!string.IsNullOrEmpty(externalDecryptorPath) && File.Exists(externalDecryptorPath))
                        return DecryptWithExternalTool(filePath, externalDecryptorPath);

                    byte[] decryptedBytes = SIIDecryptSharp.Decryptor.Decrypt(filePath, true);
                    if (decryptedBytes != null && decryptedBytes.Length > 0)
                        return Encoding.UTF8.GetString(decryptedBytes);

                    return null;
                }

                return File.ReadAllText(filePath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(externalDecryptorPath) && File.Exists(externalDecryptorPath))
                {
                    try
                    {
                        return DecryptWithExternalTool(filePath, externalDecryptorPath);
                    }
                    catch (Exception extEx)
                    {
                        throw new Exception($"Failed to decrypt save file: {ex.Message}; external: {extEx.Message}", ex);
                    }
                }

                throw new Exception($"Failed to decrypt save file: {ex.Message}", ex);
            }
        }

        private static string DecryptWithExternalTool(string filePath, string toolExe)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ets2save_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string tempFile = Path.Combine(tempDir, Path.GetFileName(filePath));

            try
            {
                File.Copy(filePath, tempFile, true);

                var startInfo = new ProcessStartInfo
                {
                    FileName = toolExe,
                    Arguments = $"\"{tempFile}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = tempDir
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                        throw new InvalidOperationException("Failed to start external decryptor.");

                    if (!process.WaitForExit(15000))
                    {
                        try { process.Kill(true); } catch { }
                        throw new TimeoutException("External decryptor timed out.");
                    }

                    if (process.ExitCode != 0)
                    {
                        string err = process.StandardError.ReadToEnd();
                        throw new Exception($"External decryptor exited with code {process.ExitCode}. {err}".Trim());
                    }
                }

                if (!File.Exists(tempFile))
                    throw new FileNotFoundException("Decrypted temp file was not found after external tool ran.");

                return File.ReadAllText(tempFile, Encoding.UTF8);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch { }
            }
        }

        /// <summary>
        /// Creates/updates .bak and a timestamped backup, keeping the newest few stamps.
        /// </summary>
        public static void CreateBackup(string filePath)
        {
            if (!File.Exists(filePath)) return;

            try
            {
                string bakPath = filePath + ".bak";
                File.Copy(filePath, bakPath, true);

                string stamped = filePath + $".bak.{DateTime.Now:yyyyMMdd_HHmmss}";
                File.Copy(filePath, stamped, true);
                PruneTimestampedBackups(filePath);
            }
            catch
            {
                // Backup failure must not block save write — caller still writes atomically.
            }
        }

        private static void PruneTimestampedBackups(string filePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(filePath);
                string prefix = Path.GetFileName(filePath) + ".bak.";
                if (string.IsNullOrEmpty(dir)) return;

                var stamps = Directory.GetFiles(dir, Path.GetFileName(filePath) + ".bak.*")
                    .Where(f => Path.GetFileName(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .ToList();

                foreach (var old in stamps.Skip(MaxTimestampedBackups))
                {
                    try { old.Delete(); } catch { }
                }
            }
            catch { }
        }

        /// <summary>
        /// Backs up then atomically replaces the save file.
        /// </summary>
        public static void WriteSaveFile(string filePath, string content)
        {
            CreateBackup(filePath);

            string tempPath = filePath + ".tmp";
            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            File.WriteAllText(tempPath, content, utf8NoBom);

            try
            {
                File.Copy(tempPath, filePath, overwrite: true);
            }
            finally
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            }
        }

        public static string DecryptBytesToString(byte[] data)
        {
            if (data.Length > 4 && Encoding.ASCII.GetString(data, 0, 4) == "ScsC")
            {
                string tempFile = Path.GetTempFileName();
                try
                {
                    File.WriteAllBytes(tempFile, data);
                    byte[] decryptedBytes = SIIDecryptSharp.Decryptor.Decrypt(tempFile, true);
                    return Encoding.UTF8.GetString(decryptedBytes);
                }
                finally
                {
                    if (File.Exists(tempFile)) File.Delete(tempFile);
                }
            }
            return Encoding.UTF8.GetString(data);
        }
    }
}
