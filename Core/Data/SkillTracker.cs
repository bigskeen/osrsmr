using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OsrsMr.Core.Data
{
    public class SkillProgressItem : INotifyPropertyChanged
    {
        private string _name = "";
        private string _category = "General";
        private int _level = 1;
        private int _boostedLevel = 1;
        private int _currentXp = 0;
        private int _startXp = -1;
        private int _xpGained = 0;
        private int _nextLevel = 2;
        private int _xpForNextLevel = 83;
        private int _xpRemaining = 83;
        private double _progressPercentage = 0.0;
        private double _xpPerHour = 0.0;
        private TimeSpan _timeToLevel = TimeSpan.MaxValue;
        private string _ttlFormatted = "∞";
        private bool _isActive = false;

        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        public string Category
        {
            get => _category;
            set => SetField(ref _category, value);
        }

        public string Icon
        {
            get => GetSkillIcon(_name);
        }

        public int Level
        {
            get => _level;
            set
            {
                if (SetField(ref _level, value))
                {
                    OnPropertyChanged(nameof(LevelDisplay));
                    Recalculate();
                }
            }
        }

        public int BoostedLevel
        {
            get => _boostedLevel;
            set
            {
                if (SetField(ref _boostedLevel, value))
                {
                    OnPropertyChanged(nameof(LevelDisplay));
                }
            }
        }

        public int CurrentXp
        {
            get => _currentXp;
            set
            {
                if (SetField(ref _currentXp, value))
                {
                    if (_startXp < 0)
                        _startXp = value;

                    XpGained = Math.Max(0, _currentXp - _startXp);
                    OnPropertyChanged(nameof(CurrentXpFormatted));
                    Recalculate();
                }
            }
        }

        public int StartXp
        {
            get => _startXp;
            set
            {
                if (SetField(ref _startXp, value))
                {
                    XpGained = Math.Max(0, _currentXp - _startXp);
                }
            }
        }

        public int XpGained
        {
            get => _xpGained;
            set
            {
                if (SetField(ref _xpGained, value))
                {
                    IsActive = _xpGained > 0;
                    OnPropertyChanged(nameof(XpGainedFormatted));
                }
            }
        }

        public int NextLevel
        {
            get => _nextLevel;
            set => SetField(ref _nextLevel, value);
        }

        public int XpForNextLevel
        {
            get => _xpForNextLevel;
            set => SetField(ref _xpForNextLevel, value);
        }

        public int XpRemaining
        {
            get => _xpRemaining;
            set
            {
                if (SetField(ref _xpRemaining, value))
                {
                    OnPropertyChanged(nameof(XpRemainingFormatted));
                }
            }
        }

        public double ProgressPercentage
        {
            get => _progressPercentage;
            set
            {
                if (SetField(ref _progressPercentage, value))
                {
                    OnPropertyChanged(nameof(ProgressPercentageFormatted));
                }
            }
        }

        public double XpPerHour
        {
            get => _xpPerHour;
            set
            {
                if (SetField(ref _xpPerHour, value))
                {
                    OnPropertyChanged(nameof(XpPerHourFormatted));
                }
            }
        }

        public TimeSpan TimeToLevel
        {
            get => _timeToLevel;
            set => SetField(ref _timeToLevel, value);
        }

        public string TtlFormatted
        {
            get => _ttlFormatted;
            set => SetField(ref _ttlFormatted, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetField(ref _isActive, value);
        }

        public string LevelDisplay => _boostedLevel != _level ? $"{_boostedLevel}/{_level}" : $"{_level}";
        public string CurrentXpFormatted => $"{_currentXp:N0} XP";
        public string XpRemainingFormatted => _level >= 99 ? $"{_xpRemaining:N0} to Virtual {NextLevel}" : $"{_xpRemaining:N0} XP to Lvl {NextLevel}";
        public string XpGainedFormatted => $"+{_xpGained:N0} XP";
        public string XpPerHourFormatted => $"{ExperienceTable.FormatCompactXp((long)_xpPerHour)}/hr";
        public string ProgressPercentageFormatted => $"{_progressPercentage:F1}%";

        public void ResetSession()
        {
            _startXp = _currentXp;
            XpGained = 0;
            XpPerHour = 0.0;
            TimeToLevel = TimeSpan.MaxValue;
            TtlFormatted = "∞";
            IsActive = false;
        }

        public void RecalculateRates(TimeSpan elapsed)
        {
            if (elapsed.TotalSeconds < 2 || _xpGained <= 0)
            {
                XpPerHour = 0.0;
                TimeToLevel = TimeSpan.MaxValue;
                TtlFormatted = (_level >= 99 && _xpRemaining == 0) ? "MAX" : "∞";
                return;
            }

            double hours = elapsed.TotalHours;
            XpPerHour = hours > 0 ? (_xpGained / hours) : 0.0;
            TimeToLevel = ExperienceTable.CalculateTimeToLevel(_xpRemaining, _xpPerHour);
            TtlFormatted = ExperienceTable.FormatTtl(TimeToLevel, _level >= 99 && _xpRemaining == 0);
        }

        private void Recalculate()
        {
            int calculatedLevel = ExperienceTable.GetLevelForExperience(_currentXp);
            int baseLevel = Math.Max(_level, calculatedLevel);

            NextLevel = Math.Min(ExperienceTable.MaxVirtualLevel, baseLevel + 1);
            XpForNextLevel = ExperienceTable.GetExperienceForLevel(NextLevel);
            XpRemaining = ExperienceTable.GetXpToNextLevel(_currentXp, baseLevel);
            ProgressPercentage = ExperienceTable.GetProgressPercentage(_currentXp, baseLevel);

            if (_xpPerHour > 0)
            {
                TimeToLevel = ExperienceTable.CalculateTimeToLevel(_xpRemaining, _xpPerHour);
                TtlFormatted = ExperienceTable.FormatTtl(TimeToLevel, _level >= 99 && _xpRemaining == 0);
            }
            else
            {
                TtlFormatted = (_level >= 99 && _xpRemaining == 0) ? "MAX" : "∞";
            }
        }

        private static string GetSkillIcon(string skillName) => skillName.ToLowerInvariant() switch
        {
            "attack" => "⚔️",
            "strength" => "💪",
            "defence" => "🛡️",
            "ranged" => "🏹",
            "prayer" => "✨",
            "magic" => "🔮",
            "hitpoints" => "❤️",
            "agility" => "🏃",
            "herblore" => "🌿",
            "thieving" => "🗝️",
            "crafting" => "💎",
            "fletching" => "🎯",
            "slayer" => "💀",
            "hunter" => "🐾",
            "mining" => "⛏️",
            "smithing" => "🔨",
            "fishing" => "🎣",
            "cooking" => "🍖",
            "firemaking" => "🔥",
            "woodcutting" => "🪓",
            "farming" => "🌱",
            "runecraft" => "🌀",
            "construction" => "🏠",
            "overall" or "total" => "🏆",
            _ => "⭐"
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Singleton engine managing live XP tracking, rate calculations, and Time-To-Level for all OSRS skills.
    /// </summary>
    public class SkillTrackerEngine : INotifyPropertyChanged
    {
        public static readonly string[] CanonicalSkills =
        {
            "Attack", "Strength", "Defence", "Ranged", "Prayer", "Magic", "Hitpoints",
            "Agility", "Herblore", "Thieving", "Crafting", "Fletching", "Slayer", "Hunter",
            "Mining", "Smithing", "Fishing", "Cooking", "Firemaking", "Woodcutting",
            "Farming", "Runecraft", "Construction"
        };

        private static readonly Lazy<SkillTrackerEngine> _lazy = new(() => new SkillTrackerEngine());
        public static SkillTrackerEngine Instance => _lazy.Value;

        private readonly ConcurrentDictionary<string, SkillProgressItem> _skillMap = new(StringComparer.OrdinalIgnoreCase);
        public ObservableCollection<SkillProgressItem> Skills { get; } = new();
        public SkillProgressItem Overall { get; } = new() { Name = "Overall", Category = "Total" };

        private DateTime _sessionStartTime = DateTime.UtcNow;
        private TimeSpan _elapsedTime = TimeSpan.Zero;
        private long _totalXpGained = 0;
        private double _totalXpPerHour = 0.0;
        private int _totalLevel = 32;
        private long _totalXp = 0;

        public DateTime SessionStartTime
        {
            get => _sessionStartTime;
            private set => SetField(ref _sessionStartTime, value);
        }

        public TimeSpan ElapsedTime
        {
            get => _elapsedTime;
            private set
            {
                if (SetField(ref _elapsedTime, value))
                {
                    OnPropertyChanged(nameof(ElapsedTimeFormatted));
                }
            }
        }

        public long TotalXpGained
        {
            get => _totalXpGained;
            private set
            {
                if (SetField(ref _totalXpGained, value))
                {
                    OnPropertyChanged(nameof(TotalXpGainedFormatted));
                }
            }
        }

        public double TotalXpPerHour
        {
            get => _totalXpPerHour;
            private set
            {
                if (SetField(ref _totalXpPerHour, value))
                {
                    OnPropertyChanged(nameof(TotalXpPerHourFormatted));
                }
            }
        }

        public int TotalLevel
        {
            get => _totalLevel;
            set => SetField(ref _totalLevel, value);
        }

        public long TotalXp
        {
            get => _totalXp;
            set
            {
                if (SetField(ref _totalXp, value))
                {
                    OnPropertyChanged(nameof(TotalXpFormatted));
                }
            }
        }

        public string ElapsedTimeFormatted => $"{_elapsedTime:hh\\:mm\\:ss}";
        public string TotalXpGainedFormatted => $"+{_totalXpGained:N0} XP";
        public string TotalXpPerHourFormatted => $"{ExperienceTable.FormatCompactXp((long)_totalXpPerHour)}/hr";
        public string TotalXpFormatted => $"{_totalXp:N0} XP";

        public SkillTrackerEngine()
        {
            var skills = CanonicalSkills ?? new[]
            {
                "Attack", "Strength", "Defence", "Ranged", "Prayer", "Magic", "Hitpoints",
                "Agility", "Herblore", "Thieving", "Crafting", "Fletching", "Slayer", "Hunter",
                "Mining", "Smithing", "Fishing", "Cooking", "Firemaking", "Woodcutting",
                "Farming", "Runecraft", "Construction"
            };

            foreach (var name in skills)
            {
                var item = new SkillProgressItem
                {
                    Name = name,
                    Category = GetCategory(name),
                    Level = name.Equals("Hitpoints", StringComparison.OrdinalIgnoreCase) ? 10 : 1,
                    BoostedLevel = name.Equals("Hitpoints", StringComparison.OrdinalIgnoreCase) ? 10 : 1,
                    CurrentXp = name.Equals("Hitpoints", StringComparison.OrdinalIgnoreCase) ? 1154 : 0
                };
                _skillMap[name] = item;
                Skills.Add(item);
            }
            RecalculateTotals();
        }

        public SkillProgressItem? GetSkill(string skillName)
        {
            if (skillName.Equals("Overall", StringComparison.OrdinalIgnoreCase) || skillName.Equals("Total", StringComparison.OrdinalIgnoreCase))
                return Overall;

            _skillMap.TryGetValue(skillName, out var item);
            return item;
        }

        public void UpdateSkillLevels(string skillName, int boosted, int real)
        {
            if (_skillMap.TryGetValue(skillName, out var item))
            {
                item.BoostedLevel = boosted;
                item.Level = real;
                RecalculateTotals();
            }
        }

        public void UpdateSkillXp(string skillName, int xp)
        {
            if (_skillMap.TryGetValue(skillName, out var item))
            {
                item.CurrentXp = xp;
                RecalculateTotals();
            }
        }

        public void UpdateTimerTick()
        {
            ElapsedTime = DateTime.UtcNow - _sessionStartTime;
            foreach (var item in Skills)
            {
                item.RecalculateRates(_elapsedTime);
            }

            if (_elapsedTime.TotalHours > 0)
            {
                TotalXpPerHour = _totalXpGained / _elapsedTime.TotalHours;
            }
            else
            {
                TotalXpPerHour = 0.0;
            }
        }

        public void ResetSession()
        {
            _sessionStartTime = DateTime.UtcNow;
            ElapsedTime = TimeSpan.Zero;
            _totalXpGained = 0;
            TotalXpPerHour = 0.0;

            foreach (var item in Skills)
            {
                item.ResetSession();
            }

            RecalculateTotals();
        }

        private void RecalculateTotals()
        {
            int totLvl = 0;
            long totXp = 0;
            long totGained = 0;

            foreach (var item in Skills)
            {
                totLvl += item.Level;
                totXp += item.CurrentXp;
                totGained += item.XpGained;
            }

            TotalLevel = totLvl;
            TotalXp = totXp;
            TotalXpGained = totGained;

            Overall.Level = totLvl;
            Overall.CurrentXp = (int)Math.Min(int.MaxValue, totXp);
        }

        private static string GetCategory(string skill) => skill.ToLowerInvariant() switch
        {
            "attack" or "strength" or "defence" or "ranged" or "prayer" or "magic" or "hitpoints" => "Combat",
            "mining" or "fishing" or "woodcutting" or "farming" or "hunter" => "Gathering",
            "smithing" or "cooking" or "firemaking" or "crafting" or "fletching" or "herblore" or "runecraft" or "construction" => "Artisan",
            "agility" or "thieving" or "slayer" => "Support",
            _ => "Other"
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
