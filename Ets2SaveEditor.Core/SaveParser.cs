using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Ets2SaveEditor.Core
{
    /// <summary>Selective truck/trailer repair targets.</summary>
    public class RepairOptions
    {
        public bool TruckCabin { get; set; }
        public bool TruckChassis { get; set; }
        public bool TruckEngine { get; set; }
        public bool TruckTransmission { get; set; }
        public bool TruckWheels { get; set; }
        public bool TruckFuel { get; set; }
        public bool TrailerBody { get; set; }
        public bool TrailerChassis { get; set; }
        public bool TrailerWheels { get; set; }

        /// <summary>When set, repair this truck unit instead of the currently assigned one.</summary>
        public string TargetTruckId { get; set; }

        /// <summary>When set, repair this trailer unit instead of the currently assigned one.</summary>
        public string TargetTrailerId { get; set; }

        public bool AnyTruck =>
            TruckCabin || TruckChassis || TruckEngine || TruckTransmission || TruckWheels || TruckFuel;

        public bool AnyTrailer =>
            TrailerBody || TrailerChassis || TrailerWheels;

        public bool Any => AnyTruck || AnyTrailer;

        /// <summary>True when all truck wear components are being repaired (safe to zero aggregate wear).</summary>
        public bool FullTruckWear =>
            TruckCabin && TruckChassis && TruckEngine && TruckTransmission && TruckWheels;

        public bool FullTrailerWear =>
            TrailerBody && TrailerChassis && TrailerWheels;

        public static RepairOptions AllTruck() => new RepairOptions
        {
            TruckCabin = true,
            TruckChassis = true,
            TruckEngine = true,
            TruckTransmission = true,
            TruckWheels = true,
            TruckFuel = true
        };

        public static RepairOptions AllTrailer() => new RepairOptions
        {
            TrailerBody = true,
            TrailerChassis = true,
            TrailerWheels = true
        };

        public static RepairOptions All()
        {
            var all = AllTruck();
            all.TrailerBody = true;
            all.TrailerChassis = true;
            all.TrailerWheels = true;
            return all;
        }
    }

    public static class SaveParser
    {
        public static string ProcessSaveFile(
            string content,
            decimal money,
            int xp,
            Dictionary<string, int> skills,
            bool unlockCities,
            bool buyUpgradeGarages,
            RepairOptions repair,
            out string log,
            List<string> selectedVisitedCities = null,
            Dictionary<string, int> selectedGarages = null)
        {
            repair ??= new RepairOptions();
            skills ??= new Dictionary<string, int>();
            var sbLog = new StringBuilder();
            string[] lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            ResolveVehicleIds(lines, out string assignedTruckId, out string assignedTrailerId);
            if (!string.IsNullOrWhiteSpace(repair.TargetTruckId))
                assignedTruckId = repair.TargetTruckId.Trim();
            if (!string.IsNullOrWhiteSpace(repair.TargetTrailerId))
                assignedTrailerId = repair.TargetTrailerId.Trim();

            var allCities = CollectUnlockableCities(content);

            var output = new StringBuilder();
            var modifiedSkills = new HashSet<string>();
            var blockStack = new Stack<(string Type, string Name)>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();

                if (trimmed.EndsWith("{"))
                {
                    var match = Regex.Match(trimmed, @"^(\w+)\s*:\s*(\S+)");
                    if (match.Success)
                        blockStack.Push((match.Groups[1].Value, match.Groups[2].Value));
                    else
                    {
                        string type = trimmed.Split(new[] { ':' }, 2)[0].Trim();
                        blockStack.Push((type, trimmed));
                    }
                    output.AppendLine(line);
                    continue;
                }

                if (trimmed == "}")
                {
                    if (blockStack.Count > 0)
                    {
                        var top = blockStack.Peek();
                        if (top.Type == "economy")
                        {
                            foreach (var skill in skills)
                            {
                                if (!modifiedSkills.Contains(skill.Key))
                                {
                                    output.AppendLine($"\t{skill.Key}: {skill.Value}");
                                    sbLog.AppendLine($"Добавлен отсутствующий навык {skill.Key}: {skill.Value}");
                                }
                            }
                        }
                        blockStack.Pop();
                    }
                    output.AppendLine(line);
                    continue;
                }

                if (blockStack.Count == 0)
                {
                    output.AppendLine(line);
                    continue;
                }

                var current = blockStack.Peek();
                string currentBlockType = current.Type;
                string currentBlockName = current.Name;

                // Newer ETS2/ATS: player cash lives in top-level `bank` unit (referenced from economy).
                // Older saves still keep money_account inside economy.
                if (currentBlockType == "bank")
                {
                    var bankParts = trimmed.Split(new[] { ':' }, 2);
                    if (bankParts.Length == 2 && bankParts[0].Trim() == "money_account")
                    {
                        long moneyValue = decimal.ToInt64(Math.Truncate(money));
                        output.AppendLine($"{GetIndent(line)}money_account: {moneyValue}");
                        sbLog.AppendLine($"Баланс изменен на: {moneyValue}");
                        continue;
                    }
                }

                if (currentBlockType == "economy" || blockStack.Any(b => b.Type == "economy"))
                {
                    var parts = trimmed.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        string key = parts[0].Trim();

                        // Only rewrite economy fields when we are still inside the economy unit tree
                        // and the key is a known economy field (avoid touching nested company money etc.)
                        bool isEconomyField = key is "money_account" or "experience_points" or "visited_cities"
                            || skills.ContainsKey(key);

                        if (isEconomyField && currentBlockType == "economy")
                        {
                        if (key == "money_account")
                        {
                            long moneyValue = decimal.ToInt64(Math.Truncate(money));
                            output.AppendLine($"{GetIndent(line)}money_account: {moneyValue}");
                            sbLog.AppendLine($"Баланс изменен на: {moneyValue}");
                            continue;
                        }
                        if (key == "experience_points")
                        {
                            output.AppendLine($"{GetIndent(line)}experience_points: {xp}");
                            sbLog.AppendLine($"Опыт изменен на: {xp}");
                            continue;
                        }

                        if (skills.ContainsKey(key))
                        {
                            output.AppendLine($"{GetIndent(line)}{key}: {skills[key]}");
                            modifiedSkills.Add(key);
                            continue;
                        }

                        if (selectedVisitedCities != null && key == "visited_cities")
                        {
                            RewriteVisitedCities(output, GetIndent(line), selectedVisitedCities);
                            i = SkipArrayLines(lines, i, "visited_cities");
                            sbLog.AppendLine($"Разблокировано выбранных городов: {selectedVisitedCities.Count}");
                            continue;
                        }
                        if (unlockCities && key == "visited_cities")
                        {
                            var cityList = allCities.ToList();
                            RewriteVisitedCities(output, GetIndent(line), cityList);
                            i = SkipArrayLines(lines, i, "visited_cities");
                            sbLog.AppendLine($"Открыты города карты: {cityList.Count}.");
                            continue;
                        }
                        }
                    }
                }

                if (currentBlockType == "garage")
                {
                    int? targetStatus = null;
                    if (selectedGarages != null && selectedGarages.ContainsKey(currentBlockName))
                        targetStatus = selectedGarages[currentBlockName];
                    else if (buyUpgradeGarages)
                        targetStatus = 3;

                    if (targetStatus.HasValue)
                    {
                        var parts = trimmed.Split(new[] { ':' }, 2);
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Trim();
                            string indent = GetIndent(line);

                            if (key == "status")
                            {
                                output.AppendLine($"{indent}status: {targetStatus.Value}");
                                continue;
                            }
                            if (key == "vehicles" || key == "drivers")
                            {
                                int size = GarageSlotCount(targetStatus.Value);
                                i = RewriteSizedArrayPreserving(lines, i, output, indent, key, size);
                                continue;
                            }
                        }
                    }
                }

                if (repair.AnyTruck && currentBlockType == "vehicle" && currentBlockName == assignedTruckId)
                {
                    if (TryApplyTruckRepair(line, trimmed, repair, output, lines, ref i))
                        continue;
                }

                if (repair.AnyTrailer && currentBlockType == "trailer" && currentBlockName == assignedTrailerId)
                {
                    if (TryApplyTrailerRepair(line, trimmed, repair, output, lines, ref i))
                        continue;
                }

                output.AppendLine(line);
            }

            if (repair.FullTruckWear && repair.FullTrailerWear)
                sbLog.AppendLine("Грузовик и прицеп отремонтированы.");
            else if (repair.AnyTruck && repair.TruckFuel && !repair.FullTruckWear && !repair.TruckCabin && !repair.TruckChassis
                     && !repair.TruckEngine && !repair.TruckTransmission && !repair.TruckWheels)
                sbLog.AppendLine("Грузовик заправлен.");
            else if (repair.AnyTruck)
                sbLog.AppendLine("Грузовик отремонтирован" + (repair.TruckFuel ? " и заправлен." : "."));
            else if (repair.AnyTrailer)
                sbLog.AppendLine("Прицеп отремонтирован.");

            if (buyUpgradeGarages)
                sbLog.AppendLine("Все гаражи куплены и расширены до максимального уровня.");

            log = sbLog.ToString();
            return output.ToString();
        }

        private static void ResolveVehicleIds(string[] lines, out string assignedTruckId, out string assignedTrailerId)
        {
            assignedTruckId = null;
            assignedTrailerId = null;
            string companyTruckId = null;
            string companyTrailerId = null;
            string assignedVehiclesId = null;

            int depth = 0;
            int playerDepth = -1;
            int playerJobDepth = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.EndsWith("{"))
                {
                    depth++;
                    if (trimmed.StartsWith("player :") || trimmed.StartsWith("player:"))
                        playerDepth = depth;
                    else if (trimmed.StartsWith("player_job :") || trimmed.StartsWith("player_job:"))
                        playerJobDepth = depth;
                    continue;
                }

                if (trimmed == "}")
                {
                    if (depth == playerDepth) playerDepth = -1;
                    if (depth == playerJobDepth) playerJobDepth = -1;
                    depth = Math.Max(0, depth - 1);
                    continue;
                }

                var parts = trimmed.Split(new[] { ':' }, 2);
                if (parts.Length != 2) continue;
                string key = parts[0].Trim();
                string val = parts[1].Trim();
                if (val == "null") continue;

                if (playerDepth >= 0 && depth >= playerDepth)
                {
                    if (key == "assigned_truck") assignedTruckId = val;
                    else if (key == "assigned_trailer") assignedTrailerId = val;
                    else if (key == "assigned_vehicles") assignedVehiclesId = val;
                }
                else if (playerJobDepth >= 0 && depth >= playerJobDepth)
                {
                    if (key == "company_truck") companyTruckId = val;
                    else if (key == "company_trailer") companyTrailerId = val;
                }
            }

            if (assignedTruckId == null && assignedVehiclesId != null)
            {
                var blockStack = new Stack<(string Type, string Name)>();
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.EndsWith("{"))
                    {
                        var m = Regex.Match(trimmed, @"^(\w+)\s*:\s*(\S+)");
                        if (m.Success)
                            blockStack.Push((m.Groups[1].Value, m.Groups[2].Value));
                        continue;
                    }
                    if (trimmed == "}")
                    {
                        if (blockStack.Count > 0) blockStack.Pop();
                        continue;
                    }
                    if (blockStack.Count == 0) continue;
                    var cur = blockStack.Peek();
                    if (cur.Type != "player_vehicles" || cur.Name != assignedVehiclesId) continue;

                    var parts = trimmed.Split(new[] { ':' }, 2);
                    if (parts.Length != 2) continue;
                    string key = parts[0].Trim();
                    string val = parts[1].Trim();
                    if (val == "null") continue;
                    if (key == "vehicle") assignedTruckId = val;
                    if (key == "trailer") assignedTrailerId = val;
                }
            }

            assignedTruckId ??= companyTruckId;
            assignedTrailerId ??= companyTrailerId;
        }

        /// <summary>
        /// Cities that can be unlocked: garage cities + company cities + already visited.
        /// </summary>
        public static HashSet<string> CollectUnlockableCities(string content)
        {
            var cities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match gm in Regex.Matches(content, @"\bgarage\s*:\s*garage\.([a-zA-Z0-9_\-]+)"))
                cities.Add(gm.Groups[1].Value);

            foreach (Match cm in Regex.Matches(content, @"\bcompany\s*:\s*company\.([a-zA-Z0-9_\-]+)\."))
                cities.Add(cm.Groups[1].Value);

            foreach (Match vm in Regex.Matches(content, @"visited_cities\[\d+\]:\s*([a-zA-Z0-9_\-]+)"))
                cities.Add(vm.Groups[1].Value);

            return cities;
        }

        private static int GarageSlotCount(int status) => status switch
        {
            3 => 5,
            2 => 3,
            1 => 1,
            _ => 0
        };

        private static void RewriteVisitedCities(StringBuilder output, string indent, List<string> cities)
        {
            output.AppendLine($"{indent}visited_cities: {cities.Count}");
            for (int c = 0; c < cities.Count; c++)
                output.AppendLine($"{indent}visited_cities[{c}]: {cities[c]}");
        }

        private static int SkipArrayLines(string[] lines, int currentIndex, string key)
        {
            int nextIdx = currentIndex + 1;
            while (nextIdx < lines.Length && lines[nextIdx].Trim().StartsWith(key + "[", StringComparison.Ordinal))
                nextIdx++;
            return nextIdx - 1;
        }

        /// <summary>
        /// Rewrites vehicles/drivers arrays while preserving existing unit IDs in overlapping slots.
        /// </summary>
        private static int RewriteSizedArrayPreserving(
            string[] lines, int index, StringBuilder output, string indent, string key, int newSize)
        {
            var existing = new List<string>();
            int nextIdx = index + 1;
            while (nextIdx < lines.Length && lines[nextIdx].Trim().StartsWith(key + "[", StringComparison.Ordinal))
            {
                var p = lines[nextIdx].Trim().Split(new[] { ':' }, 2);
                existing.Add(p.Length == 2 ? p[1].Trim() : "null");
                nextIdx++;
            }

            output.AppendLine($"{indent}{key}: {newSize}");
            for (int v = 0; v < newSize; v++)
            {
                string val = v < existing.Count ? existing[v] : "null";
                output.AppendLine($"{indent}{key}[{v}]: {val}");
            }

            return nextIdx - 1;
        }

        private static bool TryApplyTruckRepair(
            string line, string trimmed, RepairOptions repair, StringBuilder output, string[] lines, ref int i)
        {
            var parts = trimmed.Split(new[] { ':' }, 2);
            if (parts.Length != 2) return false;

            string key = parts[0].Trim();
            string indent = GetIndent(line);

            bool zeroWear =
                (key == "wear" && repair.FullTruckWear)
                || (key == "cabin_wear" && repair.TruckCabin)
                || (key == "chassis_wear" && repair.TruckChassis)
                || (key == "engine_wear" && repair.TruckEngine)
                || (key == "transmission_wear" && repair.TruckTransmission);

            if (zeroWear)
            {
                output.AppendLine($"{indent}{key}: 0");
                return true;
            }

            if (key == "fuel_relative" && repair.TruckFuel)
            {
                output.AppendLine($"{indent}fuel_relative: &3f800000");
                return true;
            }

            if (key == "wheels_wear" && repair.TruckWheels && int.TryParse(parts[1].Trim(), out int wheelCount))
            {
                output.AppendLine($"{indent}wheels_wear: {wheelCount}");
                for (int w = 0; w < wheelCount; w++)
                    output.AppendLine($"{indent}wheels_wear[{w}]: 0");
                i = SkipArrayLines(lines, i, "wheels_wear");
                return true;
            }

            return false;
        }

        private static bool TryApplyTrailerRepair(
            string line, string trimmed, RepairOptions repair, StringBuilder output, string[] lines, ref int i)
        {
            var parts = trimmed.Split(new[] { ':' }, 2);
            if (parts.Length != 2) return false;

            string key = parts[0].Trim();
            string indent = GetIndent(line);

            bool zeroWear =
                (key == "wear" && repair.FullTrailerWear)
                || (key == "body_wear" && repair.TrailerBody)
                || (key == "chassis_wear" && repair.TrailerChassis);

            if (zeroWear)
            {
                output.AppendLine($"{indent}{key}: 0");
                return true;
            }

            if (key == "wheels_wear" && repair.TrailerWheels && int.TryParse(parts[1].Trim(), out int wheelCount))
            {
                output.AppendLine($"{indent}wheels_wear: {wheelCount}");
                for (int w = 0; w < wheelCount; w++)
                    output.AppendLine($"{indent}wheels_wear[{w}]: 0");
                i = SkipArrayLines(lines, i, "wheels_wear");
                return true;
            }

            return false;
        }

        private static string GetIndent(string line)
        {
            int count = 0;
            while (count < line.Length && char.IsWhiteSpace(line[count]))
                count++;
            return line.Substring(0, count);
        }
    }
}
