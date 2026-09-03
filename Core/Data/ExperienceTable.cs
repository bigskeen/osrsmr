using System;

namespace OsrsMr.Core.Data
{
    /// <summary>
    /// Accurate Old School RuneScape experience and leveling formulas up to virtual level 126.
    /// Provides calculations for XP requirements, progress percentages, XP/hr, and Time-To-Level (TTL).
    /// </summary>
    public static class ExperienceTable
    {
        public const int MaxStandardLevel = 99;
        public const int MaxVirtualLevel = 126;
        public const int MaxXp = 200_000_000;

        private static readonly int[] ExperiencePoints = new int[MaxVirtualLevel + 2];

        static ExperienceTable()
        {
            double points = 0.0;
            ExperiencePoints[1] = 0;

            for (int lvl = 1; lvl <= MaxVirtualLevel; lvl++)
            {
                points += Math.Floor(lvl + 300.0 * Math.Pow(2.0, lvl / 7.0));
                ExperiencePoints[lvl + 1] = (int)Math.Floor(points / 4.0);
            }
        }

        /// <summary>
        /// Gets the minimum XP required to achieve the specified level.
        /// </summary>
        public static int GetExperienceForLevel(int level)
        {
            if (level <= 1) return 0;
            if (level >= MaxVirtualLevel) return ExperiencePoints[MaxVirtualLevel];
            return ExperiencePoints[level];
        }

        /// <summary>
        /// Gets the current level achieved for a given amount of XP.
        /// </summary>
        public static int GetLevelForExperience(int xp)
        {
            if (xp <= 0) return 1;
            if (xp >= ExperiencePoints[MaxVirtualLevel]) return MaxVirtualLevel;

            for (int lvl = MaxVirtualLevel; lvl >= 1; lvl--)
            {
                if (xp >= ExperiencePoints[lvl])
                    return lvl;
            }

            return 1;
        }

        /// <summary>
        /// Calculates the remaining XP required to achieve the next level.
        /// </summary>
        public static int GetXpToNextLevel(int currentXp, int currentLevel = -1)
        {
            if (currentLevel <= 0)
                currentLevel = GetLevelForExperience(currentXp);

            if (currentLevel >= MaxStandardLevel)
            {
                // If already 99 or above, target next virtual level or 200M XP
                if (currentLevel >= MaxVirtualLevel)
                    return Math.Max(0, MaxXp - currentXp);

                int nextLvlXp = GetExperienceForLevel(currentLevel + 1);
                return Math.Max(0, nextLvlXp - currentXp);
            }

            int nextLevelXp = GetExperienceForLevel(currentLevel + 1);
            return Math.Max(0, nextLevelXp - currentXp);
        }

        /// <summary>
        /// Calculates the percentage of progress towards the next level (0.0 to 100.0).
        /// </summary>
        public static double GetProgressPercentage(int currentXp, int currentLevel = -1)
        {
            if (currentLevel <= 0)
                currentLevel = GetLevelForExperience(currentXp);

            if (currentLevel >= MaxStandardLevel)
            {
                if (currentXp >= MaxXp) return 100.0;
                int currLvlXp = GetExperienceForLevel(currentLevel);
                int nextLvlXp = GetExperienceForLevel(Math.Min(MaxVirtualLevel, currentLevel + 1));
                if (nextLvlXp <= currLvlXp) return 100.0;

                double prog = (double)(currentXp - currLvlXp) / (nextLvlXp - currLvlXp) * 100.0;
                return Math.Clamp(prog, 0.0, 100.0);
            }

            int startXp = GetExperienceForLevel(currentLevel);
            int targetXp = GetExperienceForLevel(currentLevel + 1);

            if (targetXp <= startXp) return 100.0;

            double progress = (double)(currentXp - startXp) / (targetXp - startXp) * 100.0;
            return Math.Clamp(progress, 0.0, 100.0);
        }

        /// <summary>
        /// Calculates estimated time to next level (TTL) based on remaining XP and XP per hour rate.
        /// </summary>
        public static TimeSpan CalculateTimeToLevel(int xpRemaining, double xpPerHour)
        {
            if (xpRemaining <= 0) return TimeSpan.Zero;
            if (xpPerHour <= 0.0) return TimeSpan.MaxValue;

            double hoursRemaining = xpRemaining / xpPerHour;
            if (hoursRemaining > 9999.0 || double.IsInfinity(hoursRemaining) || double.IsNaN(hoursRemaining))
                return TimeSpan.MaxValue;

            return TimeSpan.FromHours(hoursRemaining);
        }

        /// <summary>
        /// Formats a TTL TimeSpan into human-readable text (e.g., "01h 24m 10s", "45m 12s", "Max", or "∞").
        /// </summary>
        public static string FormatTtl(TimeSpan ttl, bool isMax = false)
        {
            if (isMax) return "MAX";
            if (ttl == TimeSpan.Zero) return "0s";
            if (ttl == TimeSpan.MaxValue) return "∞";

            if (ttl.TotalHours >= 24)
            {
                int days = (int)ttl.TotalDays;
                return $"{days}d {ttl.Hours:D2}h {ttl.Minutes:D2}m";
            }

            if (ttl.TotalHours >= 1)
            {
                return $"{ttl.Hours:D2}h {ttl.Minutes:D2}m {ttl.Seconds:D2}s";
            }

            if (ttl.TotalMinutes >= 1)
            {
                return $"{ttl.Minutes:D2}m {ttl.Seconds:D2}s";
            }

            return $"{ttl.Seconds:D2}s";
        }

        /// <summary>
        /// Formats XP values into readable abbreviated numbers (e.g. 13.04M, 150.5K, 4,520).
        /// </summary>
        public static string FormatCompactXp(long xp)
        {
            if (xp >= 1_000_000)
                return $"{(xp / 1_000_000.0):F2}M";
            if (xp >= 1_000)
                return $"{(xp / 1_000.0):F1}K";
            return xp.ToString("N0");
        }
    }
}
