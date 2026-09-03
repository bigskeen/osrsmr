using System;

namespace OsrsMr.Core.Profiles
{
    public class AccountProfile
    {
        public string ProfileName { get; set; } = "Default Profile";
        public string AccountName { get; set; } = "Player";
        public int PreferredWorld { get; set; } = 301;
        public string BankPin { get; set; } = "";
        
        // Safety & Auto-pause triggers
        public bool AutoPauseLowHp { get; set; } = true;
        public int LowHpThresholdPercent { get; set; } = 20;
        public bool AutoPauseOnDeath { get; set; } = true;
        public bool AutoPauseOnStaffNearby { get; set; } = true;
        
        // Break Scheduler
        public bool EnableBreaks { get; set; } = true;
        public int PlayDurationMinMinutes { get; set; } = 45;
        public int PlayDurationMaxMinutes { get; set; } = 75;
        public int BreakDurationMinMinutes { get; set; } = 5;
        public int BreakDurationMaxMinutes { get; set; } = 15;
        public bool LogoutDuringLongBreaks { get; set; } = true;
    }
}
