using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ets2SaveEditor.Core
{
    public static class FleetParser
    {
        public static void Parse(string content, out List<FleetUnit> trucks, out List<FleetUnit> trailers)
        {
            trucks = new List<FleetUnit>();
            trailers = new List<FleetUnit>();
            if (string.IsNullOrEmpty(content)) return;

            ResolveAssigned(content, out string assignedTruckId, out string assignedTrailerId);
            var truckIds = CollectTruckIds(content);
            var trailerIds = CollectTrailerIds(content);

            foreach (string id in truckIds)
            {
                string block = ExtractBlock(content, "vehicle", id);
                if (block == null) continue;
                var unit = ParseTruck(id, block, content);
                unit.IsAssigned = string.Equals(id, assignedTruckId, StringComparison.OrdinalIgnoreCase);
                trucks.Add(unit);
            }

            foreach (string id in trailerIds)
            {
                string block = ExtractBlock(content, "trailer", id);
                if (block == null) continue;
                var unit = ParseTrailer(id, block, content);
                unit.IsAssigned = string.Equals(id, assignedTrailerId, StringComparison.OrdinalIgnoreCase);
                trailers.Add(unit);
            }

            trucks = trucks.OrderByDescending(t => t.IsAssigned).ThenBy(t => t.DisplayName).ToList();
            trailers = trailers.OrderByDescending(t => t.IsAssigned).ThenBy(t => t.DisplayName).ToList();
        }

        private static HashSet<string> CollectTruckIds(string content)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string player = ExtractBlock(content, "player");
            if (player != null)
            {
                AddId(ids, Regex.Match(player, @"(?m)^\s*assigned_truck:\s*(\S+)"));
                AddId(ids, Regex.Match(player, @"(?m)^\s*my_truck:\s*(\S+)"));
                foreach (Match m in Regex.Matches(player, @"(?m)^\s*my_vehicles\[\d+\]:\s*(\S+)"))
                {
                    string pvId = m.Groups[1].Value.Trim();
                    if (pvId == "null") continue;
                    string pvb = ExtractBlock(content, "player_vehicles", pvId);
                    if (pvb == null) continue;
                    AddId(ids, Regex.Match(pvb, @"(?m)^\s*vehicle:\s*(\S+)"));
                }

                var av = Regex.Match(player, @"(?m)^\s*assigned_vehicles:\s*(\S+)");
                if (av.Success)
                {
                    string pvb = ExtractBlock(content, "player_vehicles", av.Groups[1].Value.Trim());
                    if (pvb != null)
                        AddId(ids, Regex.Match(pvb, @"(?m)^\s*vehicle:\s*(\S+)"));
                }
            }

            string job = ExtractBlock(content, "player_job");
            if (job != null)
                AddId(ids, Regex.Match(job, @"(?m)^\s*company_truck:\s*(\S+)"));

            return ids;
        }

        private static HashSet<string> CollectTrailerIds(string content)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string player = ExtractBlock(content, "player");
            if (player != null)
            {
                AddId(ids, Regex.Match(player, @"(?m)^\s*assigned_trailer:\s*(\S+)"));
                AddId(ids, Regex.Match(player, @"(?m)^\s*my_trailer:\s*(\S+)"));
                foreach (Match m in Regex.Matches(player, @"(?m)^\s*trailers\[\d+\]:\s*(\S+)"))
                    AddId(ids, m);
                foreach (Match m in Regex.Matches(player, @"(?m)^\s*my_vehicles\[\d+\]:\s*(\S+)"))
                {
                    string pvId = m.Groups[1].Value.Trim();
                    if (pvId == "null") continue;
                    string pvb = ExtractBlock(content, "player_vehicles", pvId);
                    if (pvb == null) continue;
                    AddId(ids, Regex.Match(pvb, @"(?m)^\s*trailer:\s*(\S+)"));
                }

                var av = Regex.Match(player, @"(?m)^\s*assigned_vehicles:\s*(\S+)");
                if (av.Success)
                {
                    string pvb = ExtractBlock(content, "player_vehicles", av.Groups[1].Value.Trim());
                    if (pvb != null)
                        AddId(ids, Regex.Match(pvb, @"(?m)^\s*trailer:\s*(\S+)"));
                }
            }

            string job = ExtractBlock(content, "player_job");
            if (job != null)
                AddId(ids, Regex.Match(job, @"(?m)^\s*company_trailer:\s*(\S+)"));

            return ids;
        }

        private static void AddId(HashSet<string> ids, Match m)
        {
            if (!m.Success) return;
            string id = m.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(id) && !id.Equals("null", StringComparison.OrdinalIgnoreCase))
                ids.Add(id);
        }

        private static void ResolveAssigned(string content, out string truckId, out string trailerId)
        {
            truckId = null;
            trailerId = null;
            string player = ExtractBlock(content, "player");
            if (player == null) return;

            var at = Regex.Match(player, @"(?m)^\s*assigned_truck:\s*(\S+)");
            if (at.Success && at.Groups[1].Value != "null") truckId = at.Groups[1].Value.Trim();

            var atr = Regex.Match(player, @"(?m)^\s*assigned_trailer:\s*(\S+)");
            if (atr.Success && atr.Groups[1].Value != "null") trailerId = atr.Groups[1].Value.Trim();

            if (truckId == null || trailerId == null)
            {
                var av = Regex.Match(player, @"(?m)^\s*assigned_vehicles:\s*(\S+)");
                if (av.Success && av.Groups[1].Value != "null")
                {
                    string pvb = ExtractBlock(content, "player_vehicles", av.Groups[1].Value.Trim());
                    if (pvb != null)
                    {
                        if (truckId == null)
                        {
                            var v = Regex.Match(pvb, @"(?m)^\s*vehicle:\s*(\S+)");
                            if (v.Success && v.Groups[1].Value != "null") truckId = v.Groups[1].Value.Trim();
                        }
                        if (trailerId == null)
                        {
                            var t = Regex.Match(pvb, @"(?m)^\s*trailer:\s*(\S+)");
                            if (t.Success && t.Groups[1].Value != "null") trailerId = t.Groups[1].Value.Trim();
                        }
                    }
                }
            }

            if (truckId == null || trailerId == null)
            {
                string job = ExtractBlock(content, "player_job");
                if (job != null)
                {
                    if (truckId == null)
                    {
                        var v = Regex.Match(job, @"(?m)^\s*company_truck:\s*(\S+)");
                        if (v.Success && v.Groups[1].Value != "null") truckId = v.Groups[1].Value.Trim();
                    }
                    if (trailerId == null)
                    {
                        var t = Regex.Match(job, @"(?m)^\s*company_trailer:\s*(\S+)");
                        if (t.Success && t.Groups[1].Value != "null") trailerId = t.Groups[1].Value.Trim();
                    }
                }
            }
        }

        private static FleetUnit ParseTruck(string id, string block, string fullContent)
        {
            string model = ResolveModelName(block, fullContent, isTruck: true);
            string plate = CleanPlate(Regex.Match(block, @"(?m)^\s*license_plate:\s*""([^""]*)""").Groups[1].Value);
            return new FleetUnit
            {
                Id = id,
                IsTruck = true,
                DisplayName = string.IsNullOrWhiteSpace(model) ? ShortId(id) : model,
                LicensePlate = plate,
                CabinWear = ReadWear(block, "cabin_wear"),
                ChassisWear = ReadWear(block, "chassis_wear"),
                EngineWear = ReadWear(block, "engine_wear"),
                TransmissionWear = ReadWear(block, "transmission_wear"),
                WheelsWear = ReadWheelsWear(block),
                FuelRelative = ReadFloat(block, "fuel_relative", 1.0)
            };
        }

        private static FleetUnit ParseTrailer(string id, string block, string fullContent)
        {
            string model = ResolveModelName(block, fullContent, isTruck: false);
            string plate = CleanPlate(Regex.Match(block, @"(?m)^\s*license_plate:\s*""([^""]*)""").Groups[1].Value);
            return new FleetUnit
            {
                Id = id,
                IsTruck = false,
                DisplayName = string.IsNullOrWhiteSpace(model) ? ShortId(id) : model,
                LicensePlate = plate,
                BodyWear = ReadWear(block, "trailer_body_wear") > 0
                    ? ReadWear(block, "trailer_body_wear")
                    : ReadWear(block, "body_wear"),
                ChassisWear = ReadWear(block, "chassis_wear"),
                WheelsWear = ReadWheelsWear(block),
                FuelRelative = 1.0
            };
        }

        private static string ResolveModelName(string unitBlock, string fullContent, bool isTruck)
        {
            foreach (Match am in Regex.Matches(unitBlock, @"(?m)^\s*accessories\[\d+\]:\s*(\S+)"))
            {
                string accId = am.Groups[1].Value.Trim();
                if (accId == "null") continue;
                string acc = ExtractBlock(fullContent, "vehicle_accessory", accId)
                             ?? ExtractBlock(fullContent, "trailer_accessory", accId);
                if (acc == null) continue;
                var path = Regex.Match(acc, @"data_path:\s*""([^""]+)""");
                if (!path.Success) continue;
                string p = path.Groups[1].Value.Replace('\\', '/');
                // /def/vehicle/truck/volvo.fh16/data.sii  or  .../trailer/...
                var m = Regex.Match(p, isTruck
                    ? @"/truck/([^/]+)/"
                    : @"/trailer(?:s)?/([^/]+)/");
                if (m.Success)
                    return PrettyModel(m.Groups[1].Value);
            }
            return null;
        }

        private static string PrettyModel(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            return raw.Replace('.', ' ').Replace('_', ' ');
        }

        private static string CleanPlate(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string s = Regex.Replace(raw, @"<[^>]+>", "");
            int pipe = s.IndexOf('|');
            if (pipe >= 0) s = s.Substring(0, pipe);
            return Regex.Replace(s, @"\s+", " ").Trim();
        }

        private static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "?";
            int dot = id.LastIndexOf('.');
            return dot >= 0 && dot < id.Length - 1 ? id.Substring(dot + 1) : id;
        }

        private static double ReadWear(string block, string key)
        {
            // Prefer unrepaired wear when present
            var u = Regex.Match(block, $@"(?m)^\s*{Regex.Escape(key)}_unrepairable:\s*(\S+)");
            if (u.Success) return Clamp01(ParseFloat(u.Groups[1].Value));
            var w = Regex.Match(block, $@"(?m)^\s*{Regex.Escape(key)}:\s*(\S+)");
            if (w.Success) return Clamp01(ParseFloat(w.Groups[1].Value));
            return 0;
        }

        private static double ReadWheelsWear(string block)
        {
            var values = new List<double>();
            foreach (Match m in Regex.Matches(block, @"(?m)^\s*wheels_wear_unrepairable\[\d+\]:\s*(\S+)"))
                values.Add(Clamp01(ParseFloat(m.Groups[1].Value)));
            if (values.Count == 0)
            {
                foreach (Match m in Regex.Matches(block, @"(?m)^\s*wheels_wear\[\d+\]:\s*(\S+)"))
                    values.Add(Clamp01(ParseFloat(m.Groups[1].Value)));
            }
            return values.Count == 0 ? 0 : values.Average();
        }

        private static double ReadFloat(string block, string key, double fallback)
        {
            var m = Regex.Match(block, $@"(?m)^\s*{Regex.Escape(key)}:\s*(\S+)");
            return m.Success ? ParseFloat(m.Groups[1].Value) : fallback;
        }

        private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);

        private static double ParseFloat(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0;
            raw = raw.Trim();
            if (raw.StartsWith("&", StringComparison.Ordinal))
            {
                string hex = raw.TrimStart('&');
                if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint bits))
                {
                    byte[] bytes = BitConverter.GetBytes(bits);
                    return BitConverter.ToSingle(bytes, 0);
                }
                return 0;
            }
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                return d;
            return 0;
        }

        private static string ExtractBlock(string text, string blockClass, string blockName = null)
        {
            string pattern = blockName != null
                ? @"\b" + Regex.Escape(blockClass) + @"\s*:\s*" + Regex.Escape(blockName) + @"\s*\{"
                : @"\b" + Regex.Escape(blockClass) + @"\s*:\s*[^{\r\n]+\{";

            var match = Regex.Match(text, pattern);
            if (!match.Success) return null;

            int startIndex = match.Index + match.Length;
            int braceCount = 1;
            int currentIndex = startIndex;
            while (braceCount > 0 && currentIndex < text.Length)
            {
                char c = text[currentIndex];
                if (c == '{') braceCount++;
                else if (c == '}') braceCount--;
                if (braceCount == 0)
                    return text.Substring(startIndex, currentIndex - startIndex);
                currentIndex++;
            }
            return null;
        }
    }
}
