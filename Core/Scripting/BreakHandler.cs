using System;
using OsrsMr.Core.Profiles;

namespace OsrsMr.Core.Scripting
{
    public class BreakHandler
    {
        private static readonly Random Rng = new();
        private DateTime _sessionStartTime;
        private DateTime _nextBreakTime;
        private int _breakDurationMinutes;
        private bool _isCurrentlyOnBreak;

        public bool IsOnBreak => _isCurrentlyOnBreak;
        public TimeSpan TimeUntilBreak => _nextBreakTime > DateTime.UtcNow ? _nextBreakTime - DateTime.UtcNow : TimeSpan.Zero;

        public event Action<string>? OnBreakEvent;

        public void Initialize(AccountProfile profile)
        {
            _sessionStartTime = DateTime.UtcNow;
            _isCurrentlyOnBreak = false;
            ScheduleNextBreak(profile);
        }

        private void ScheduleNextBreak(AccountProfile profile)
        {
            if (!profile.EnableBreaks)
            {
                _nextBreakTime = DateTime.MaxValue;
                return;
            }

            int playMins = Rng.Next(
                Math.Max(5, profile.PlayDurationMinMinutes),
                Math.Max(profile.PlayDurationMinMinutes + 1, profile.PlayDurationMaxMinutes + 1));

            _breakDurationMinutes = Rng.Next(
                Math.Max(1, profile.BreakDurationMinMinutes),
                Math.Max(profile.BreakDurationMinMinutes + 1, profile.BreakDurationMaxMinutes + 1));

            _nextBreakTime = DateTime.UtcNow.AddMinutes(playMins);
            OnBreakEvent?.Invoke($"[BREAK SCHEDULER] Next break scheduled in {playMins}m (Duration: {_breakDurationMinutes}m)");
        }

        public bool CheckBreakCondition(AccountProfile profile, out int breakDurationSeconds)
        {
            breakDurationSeconds = 0;
            if (!profile.EnableBreaks) return false;

            if (DateTime.UtcNow >= _nextBreakTime && !_isCurrentlyOnBreak)
            {
                _isCurrentlyOnBreak = true;
                breakDurationSeconds = _breakDurationMinutes * 60;
                OnBreakEvent?.Invoke($"[BREAK SCHEDULER] Starting scheduled break for {_breakDurationMinutes} minutes.");
                return true;
            }

            return false;
        }

        public void CompleteBreak(AccountProfile profile)
        {
            _isCurrentlyOnBreak = false;
            OnBreakEvent?.Invoke("[BREAK SCHEDULER] Break finished. Resuming execution.");
            ScheduleNextBreak(profile);
        }

        /// <summary>
        /// Evaluates safety triggers (low HP, player death). Returns true if script should auto-pause for safety.
        /// </summary>
        public static bool CheckSafetyTriggers(GameState state, AccountProfile profile, out string reason)
        {
            reason = string.Empty;
            if (state?.Player == null) return false;

            // Check Low HP trigger
            if (profile.AutoPauseLowHp && state.Player.MaxHp > 0)
            {
                double hpPercent = (double)state.Player.CurrentHp / state.Player.MaxHp * 100.0;
                if (hpPercent <= profile.LowHpThresholdPercent && state.Player.CurrentHp > 0)
                {
                    reason = $"Health dropped to {hpPercent:F0}% (Below safety threshold of {profile.LowHpThresholdPercent}%)";
                    return true;
                }
            }

            // Check Death trigger
            if (profile.AutoPauseOnDeath && state.Player.CurrentHp == 0 && state.Player.Animation == 836) // 836 standard death anim
            {
                reason = "Player death detected";
                return true;
            }

            return false;
        }
    }
}
