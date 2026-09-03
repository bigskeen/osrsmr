using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace OsrsMr.Core.Scripting
{
    public class SkillProgress
    {
        public string SkillName { get; set; } = "";
        public int StartXp { get; set; }
        public int CurrentXp { get; set; }
        public int GainedXp => Math.Max(0, CurrentXp - StartXp);
        public int Level { get; set; } = 1;
        public int BoostedLevel { get; set; } = 1;
        public int NextLevel { get; set; } = 2;
        public int XpToNextLevel { get; set; }
        public double ProgressPercentage { get; set; }
        public double XpPerHour { get; set; }
        public TimeSpan TimeToLevel { get; set; }
    }

    /// <summary>
    /// Thread-safe real-time XP tracker calculating gain rates, TTL, and progress across all trained skills.
    /// </summary>
    public class XpTracker
    {
        private static XpTracker? _instance;
        public static XpTracker Instance => _instance ??= new XpTracker();

        private readonly ConcurrentDictionary<string, int> _startXp = new(StringComparer.OrdinalIgnoreCase);
        private DateTime _sessionStartTime = DateTime.UtcNow;

        public DateTime SessionStartTime => _sessionStartTime;
        public TimeSpan SessionDuration => DateTime.UtcNow - _sessionStartTime;
        public long TotalGainedXp { get; private set; }
        public double TotalXpPerHour { get; private set; }

        public void Reset(GameState? state = null)
        {
            _sessionStartTime = DateTime.UtcNow;
            _startXp.Clear();
            TotalGainedXp = 0;
            TotalXpPerHour = 0;

            if (state != null)
            {
                foreach (var kvp in state.Skills)
                {
                    _startXp[kvp.Key] = kvp.Value.Experience;
                }
            }
        }

        public List<SkillProgress> Update(GameState state)
        {
            var results = new List<SkillProgress>();
            double elapsedHours = SessionDuration.TotalHours;
            long totalGained = 0;

            foreach (var kvp in state.Skills)
            {
                string skillName = kvp.Key;
                var snap = kvp.Value;
                int start = _startXp.GetOrAdd(skillName, snap.Experience);
                int gained = Math.Max(0, snap.Experience - start);
                totalGained += gained;

                double xpHr = elapsedHours > 0.0002 ? gained / elapsedHours : 0;
                int xpRemaining = snap.XpToNextLevel;
                TimeSpan ttl = xpHr > 0 ? TimeSpan.FromHours((double)xpRemaining / xpHr) : TimeSpan.Zero;

                var prog = new SkillProgress
                {
                    SkillName = skillName,
                    StartXp = start,
                    CurrentXp = snap.Experience,
                    Level = snap.Level,
                    BoostedLevel = snap.BoostedLevel,
                    NextLevel = snap.NextLevel,
                    XpToNextLevel = xpRemaining,
                    ProgressPercentage = snap.ProgressPercentage,
                    XpPerHour = xpHr,
                    TimeToLevel = ttl
                };

                if (gained > 0)
                {
                    results.Add(prog);
                }
            }

            TotalGainedXp = totalGained;
            TotalXpPerHour = elapsedHours > 0.0002 ? totalGained / elapsedHours : 0;

            return results.OrderByDescending(r => r.GainedXp).ToList();
        }
    }
}
