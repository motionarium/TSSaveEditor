using System;
using System.Collections.Generic;

namespace Ets2SaveEditor.Core
{
    /// <summary>
    /// ETS2/ATS default XP thresholds (def/economy_data / level_xp[]).
    /// After index 29 each next level needs +6800 XP.
    /// </summary>
    public static class XpLevel
    {
        private static readonly int[] LevelXpGain =
        {
            200, 500, 700, 900, 1000, 1100, 1300, 1600, 1700, 2100,
            2300, 2600, 2700, 2900, 3000, 3100, 3400, 3700, 4000, 4300,
            4600, 4700, 4900, 5200, 5700, 5900, 6000, 6200, 6600, 6800
        };

        private const int PlateauGain = 6800;
        public const int MaxUsefulLevel = 150;

        /// <summary>Player level for a total XP amount (0-based in SCS tables → display as 1+).</summary>
        public static int FromXp(long xp)
        {
            if (xp < 0) xp = 0;
            long need = 0;
            int level = 0;
            while (level < MaxUsefulLevel)
            {
                int gain = level < LevelXpGain.Length ? LevelXpGain[level] : PlateauGain;
                if (xp < need + gain)
                    return level;
                need += gain;
                level++;
            }
            return MaxUsefulLevel;
        }

        /// <summary>Minimum total XP required to reach the given display level.</summary>
        public static long ToXp(int level)
        {
            if (level <= 0) return 0;
            if (level > MaxUsefulLevel) level = MaxUsefulLevel;
            long total = 0;
            for (int i = 0; i < level; i++)
                total += i < LevelXpGain.Length ? LevelXpGain[i] : PlateauGain;
            return total;
        }
    }

    public sealed class FleetUnit
    {
        public string Id { get; set; }
        public bool IsTruck { get; set; }
        public string DisplayName { get; set; }
        public string LicensePlate { get; set; }
        /// <summary>Country/style token after '|' in license_plate (e.g. russia, europe).</summary>
        public string LicensePlateType { get; set; }
        public bool IsAssigned { get; set; }
        public double CabinWear { get; set; }
        public double ChassisWear { get; set; }
        public double EngineWear { get; set; }
        public double TransmissionWear { get; set; }
        public double WheelsWear { get; set; }
        public double BodyWear { get; set; }
        public double FuelRelative { get; set; } = 1.0;

        public string TitleLabel
        {
            get
            {
                string name = string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName;
                return name?.Trim() ?? Id;
            }
        }

        public string SubtitleLabel
        {
            get
            {
                string plate = string.IsNullOrWhiteSpace(LicensePlate) ? "" : LicensePlate.Trim();
                string type = PlateTypeLabel;
                if (string.IsNullOrEmpty(plate)) return type;
                if (string.IsNullOrEmpty(type)) return plate;
                return $"{plate} · {type}";
            }
        }

        public string PlateTypeLabel
        {
            get
            {
                if (string.IsNullOrWhiteSpace(LicensePlateType)) return "";
                string t = LicensePlateType.Trim().Replace('_', ' ');
                if (t.Length == 0) return "";
                return char.ToUpperInvariant(t[0]) + t.Substring(1).ToLowerInvariant();
            }
        }

        public string ListLabel
        {
            get
            {
                string sub = SubtitleLabel;
                return string.IsNullOrEmpty(sub) ? TitleLabel : $"{TitleLabel} · {sub}";
            }
        }
    }
}
