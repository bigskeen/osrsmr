using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using OsrsMr.Core;
using OsrsMr.Core.Scripting;

namespace osrsmr
{
    public partial class OverlayWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private readonly DispatcherTimer _trackerTimer = new();
        private IntPtr _targetHwnd = IntPtr.Zero;
        private readonly ScriptPaintCanvas _paintCanvas = new();

        private static readonly SolidColorBrush GreenStatusBrush = new(Color.FromRgb(30, 70, 32));
        private static readonly SolidColorBrush YellowStatusBrush = new(Color.FromRgb(74, 59, 16));
        private static readonly SolidColorBrush RedStatusBrush = new(Color.FromRgb(78, 30, 30));
        private static readonly SolidColorBrush GrayStatusBrush = new(Color.FromRgb(42, 42, 42));

        public OverlayWindow()
        {
            InitializeComponent();
            RenderLayer.Children.Add(_paintCanvas);
            Loaded += OverlayWindow_Loaded;
            _trackerTimer.Interval = TimeSpan.FromMilliseconds(40);
            _trackerTimer.Tick += TrackerTimer_Tick;
            _trackerTimer.Start();
        }

        private void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW);
        }

        private void TrackerTimer_Tick(object? sender, EventArgs e)
        {
            UpdateTargetWindowPosition();
            UpdateHudData();
            _paintCanvas.InvalidateVisual();
        }

        private void UpdateTargetWindowPosition()
        {
            if (_targetHwnd == IntPtr.Zero || !IsWindowValid(_targetHwnd))
            {
                _targetHwnd = FindRuneLiteHwnd();
            }

            if (_targetHwnd != IntPtr.Zero && GetWindowRect(_targetHwnd, out var rect))
            {
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                if (width > 100 && height > 100)
                {
                    Left = rect.Left;
                    Top = rect.Top;
                    Width = width;
                    Height = height;
                }
            }
        }

        private void UpdateHudData()
        {
            var active = ScriptEngine.Instance.ActiveScript;
            var state = BrainEngine.Instance.State;

            // 1. Script State, Action & Header
            if (active != null && active.Status != ScriptStatus.Stopped)
            {
                string scriptName = active.Manifest != null ? $"{active.Manifest.Name} v{active.Manifest.Version}" : active.GetType().Name;
                string categoryName = active.Manifest != null ? active.Manifest.Category.ToString().ToUpperInvariant() : "AUTOMATION";

                HudScriptTitle.Text = $"🤖 {scriptName}";
                HudScriptCategory.Text = $"CATEGORY: {categoryName}";

                // Status Badge
                HudStatusText.Text = active.Status.ToString().ToUpperInvariant();
                switch (active.Status)
                {
                    case ScriptStatus.Running:
                        HudStatusBadge.Background = GreenStatusBrush;
                        HudStatusText.Foreground = Brushes.LimeGreen;
                        break;
                    case ScriptStatus.Paused:
                        HudStatusBadge.Background = YellowStatusBrush;
                        HudStatusText.Foreground = Brushes.Gold;
                        break;
                    case ScriptStatus.Crashed:
                        HudStatusBadge.Background = RedStatusBrush;
                        HudStatusText.Foreground = Brushes.Tomato;
                        break;
                    default:
                        HudStatusBadge.Background = GrayStatusBrush;
                        HudStatusText.Foreground = Brushes.Gray;
                        break;
                }

                // Health & Diagnostic State
                if (active.HealthState == ScriptHealthState.Issue || active.Status == ScriptStatus.Crashed)
                {
                    HudHealthBanner.Background = new SolidColorBrush(Color.FromArgb(220, 60, 20, 20));
                    HudHealthBanner.BorderBrush = Brushes.Red;
                    HudHealthIcon.Text = "🔴";
                    HudHealthText.Text = string.IsNullOrWhiteSpace(active.LastIssueText) ? "Issue Detected: Script encountered an error" : $"Issue: {active.LastIssueText}";
                    HudHealthText.Foreground = Brushes.Tomato;
                }
                else if (active.HealthState == ScriptHealthState.Warning)
                {
                    HudHealthBanner.Background = new SolidColorBrush(Color.FromArgb(220, 60, 50, 15));
                    HudHealthBanner.BorderBrush = Brushes.Gold;
                    HudHealthIcon.Text = "🟡";
                    HudHealthText.Text = string.IsNullOrWhiteSpace(active.LastIssueText) ? "Warning: Condition alert" : $"Warning: {active.LastIssueText}";
                    HudHealthText.Foreground = Brushes.Gold;
                }
                else
                {
                    HudHealthBanner.Background = new SolidColorBrush(Color.FromArgb(200, 20, 45, 25));
                    HudHealthBanner.BorderBrush = new SolidColorBrush(Color.FromRgb(46, 160, 67));
                    HudHealthIcon.Text = "🟢";
                    HudHealthText.Text = "Status: Healthy / Normal Operations";
                    HudHealthText.Foreground = new SolidColorBrush(Color.FromRgb(163, 228, 215));
                }

                // Tasks & Actions
                HudTaskText.Text = string.IsNullOrWhiteSpace(active.CurrentTaskName) ? "Running..." : active.CurrentTaskName;
                HudActionText.Text = string.IsNullOrWhiteSpace(active.CurrentAction) 
                    ? active.CurrentTaskName 
                    : (string.IsNullOrWhiteSpace(active.CurrentSubTask) ? active.CurrentAction : $"{active.CurrentAction} • {active.CurrentSubTask}");
                HudRuntimeText.Text = $"⏱ {active.RunningTime:hh\\:mm\\:ss}";
            }
            else
            {
                HudScriptTitle.Text = "🤖 osrsmr Bot Engine";
                HudScriptCategory.Text = "CATEGORY: IDLE";
                HudStatusBadge.Background = GrayStatusBrush;
                HudStatusText.Text = "STOPPED";
                HudStatusText.Foreground = Brushes.Gray;

                HudHealthBanner.Background = new SolidColorBrush(Color.FromArgb(180, 30, 30, 34));
                HudHealthBanner.BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 65));
                HudHealthIcon.Text = "⚪";
                HudHealthText.Text = "Status: Ready / Waiting for script";
                HudHealthText.Foreground = Brushes.Gray;

                HudTaskText.Text = "No script currently running";
                HudActionText.Text = "Select a script in Bot Controller and click Start.";
                HudRuntimeText.Text = "⏱ 00:00:00";
            }

            // 2. Player Vitals & Stats
            int curHp = state.Player.CurrentHp;
            int maxHp = Math.Max(1, state.Player.MaxHp);
            HudHpText.Text = $"{curHp} / {maxHp}";
            HudHpBar.Maximum = maxHp;
            HudHpBar.Value = curHp;

            int curPrayer = state.Player.CurrentPrayer;
            int maxPrayer = Math.Max(1, state.Player.MaxPrayer);
            HudPrayerText.Text = $"{curPrayer} / {maxPrayer}";
            HudPrayerBar.Maximum = maxPrayer;
            HudPrayerBar.Value = curPrayer;

            HudEnergyText.Text = $"{state.Player.Energy}%";
            HudEnergyBar.Value = Math.Clamp(state.Player.Energy, 0, 100);

            HudSpecText.Text = $"{state.Player.SpecPercent}%";
            HudSpecBar.Value = Math.Clamp(state.Player.SpecPercent, 0, 100);

            HudPlayerInfoText.Text = $"Combat: {state.Player.CombatLevel} | Total: {state.Player.TotalLevel} | Pos: ({state.Player.WorldX}, {state.Player.WorldY}, P{state.Player.Plane})";

            // 2.5 Active Buffs & Timers
            UpdateBuffsOverlay(state.StatusEffects);

            // 3. Multi-Skill XP Tracker & Rates
            var activeSkills = XpTracker.Instance.Update(state);
            long totalGained = XpTracker.Instance.TotalGainedXp;
            double totalRate = XpTracker.Instance.TotalXpPerHour;

            HudTotalXpHrText.Text = $"+{totalGained:N0} XP ({totalRate:N0}/hr)";

            if (activeSkills.Count > 0)
            {
                // Render top 3 active skills gaining XP
                HudSkillsContainer.Children.Clear();
                int displayCount = Math.Min(3, activeSkills.Count);
                for (int i = 0; i < displayCount; i++)
                {
                    var skill = activeSkills[i];
                    var card = CreateSkillProgressCard(skill);
                    HudSkillsContainer.Children.Add(card);
                }
            }
            else
            {
                // Fallback: show first non-empty skill or generic tracker
                var primarySkill = state.Skills.Values.FirstOrDefault(s => s.Experience > 0);
                if (primarySkill != null)
                {
                    string skillName = state.Skills.FirstOrDefault(k => k.Value == primarySkill).Key ?? "Skill";
                    HudDefaultSkillName.Text = $"🎯 {skillName} (Lvl {primarySkill.Level})";
                    HudDefaultSkillRate.Text = "+0 XP (0/hr)";
                    HudDefaultSkillProgress.Value = primarySkill.ProgressPercentage;
                    HudDefaultSkillRemaining.Text = $"{primarySkill.XpToNextLevel:N0} XP to Lvl {primarySkill.NextLevel}";
                    HudDefaultSkillTtl.Text = "TTL: ∞";
                }
            }

            // 4. Tick & Engine FPS
            HudTpsText.Text = $"Tick: {state.GameTick} | World: {state.WorldNumber} | Engine: {state.EngineState}";
        }

        private static Border CreateSkillProgressCard(SkillProgress skill)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(24, 24, 27)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 50, 55)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 3)
            };

            var stack = new StackPanel();

            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            string icon = GetSkillIcon(skill.SkillName);
            var title = new TextBlock
            {
                Text = $"{icon} {skill.SkillName} (Lvl {skill.Level})",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 10
            };
            Grid.SetColumn(title, 0);

            var rate = new TextBlock
            {
                Text = $"+{skill.GainedXp:N0} ({skill.XpPerHour:N0}/hr)",
                Foreground = new SolidColorBrush(Color.FromRgb(129, 199, 132)),
                FontWeight = FontWeights.Bold,
                FontSize = 9
            };
            Grid.SetColumn(rate, 1);

            topRow.Children.Add(title);
            topRow.Children.Add(rate);
            stack.Children.Add(topRow);

            var pbar = new ProgressBar
            {
                Value = skill.ProgressPercentage,
                Maximum = 100,
                Height = 6,
                Background = new SolidColorBrush(Color.FromRgb(42, 42, 46)),
                Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Margin = new Thickness(0, 2, 0, 2)
            };
            stack.Children.Add(pbar);

            var bottomRow = new Grid();
            bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var remaining = new TextBlock
            {
                Text = $"{skill.XpToNextLevel:N0} XP to {skill.NextLevel} ({skill.ProgressPercentage:F1}%)",
                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
                FontSize = 8
            };
            Grid.SetColumn(remaining, 0);

            string ttlText = $"TTL: {OsrsMr.Core.Data.ExperienceTable.FormatTtl(skill.TimeToLevel, skill.Level >= 99 && skill.XpToNextLevel == 0)}";

            var ttl = new TextBlock
            {
                Text = ttlText,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 183, 77)),
                FontWeight = FontWeights.SemiBold,
                FontSize = 8
            };
            Grid.SetColumn(ttl, 1);

            bottomRow.Children.Add(remaining);
            bottomRow.Children.Add(ttl);
            stack.Children.Add(bottomRow);

            card.Child = stack;
            return card;
        }

        private void UpdateBuffsOverlay(StatusEffectsSnapshot effects)
        {
            if (HudBuffsWrapPanel == null) return;
            if (effects == null)
            {
                HudBuffsWrapPanel.Visibility = Visibility.Collapsed;
                return;
            }

            HudBuffsWrapPanel.Children.Clear();

            // 1. Stamina
            if (effects.HasStamina)
            {
                HudBuffsWrapPanel.Children.Add(CreateBuffBadge("🏃 " + effects.StaminaDurationFormatted, Color.FromRgb(251, 191, 36), Color.FromRgb(45, 35, 10)));
            }

            // 2. Antifire
            if (effects.HasAntifire)
            {
                string label = (effects.IsSuperAntifire ? "🛡️ Super " : "🛡️ Anti ") + effects.AntifireDurationFormatted;
                HudBuffsWrapPanel.Children.Add(CreateBuffBadge(label, Color.FromRgb(129, 199, 132), Color.FromRgb(20, 45, 25)));
            }

            // 3. Poison / Venom
            if (effects.IsEnvenomed)
            {
                HudBuffsWrapPanel.Children.Add(CreateBuffBadge($"☠️ Venom ({effects.VenomDamage})", Color.FromRgb(52, 211, 153), Color.FromRgb(15, 45, 35)));
            }
            else if (effects.IsPoisoned)
            {
                HudBuffsWrapPanel.Children.Add(CreateBuffBadge($"☠️ Poison ({effects.PoisonDamage})", Color.FromRgb(74, 222, 128), Color.FromRgb(20, 45, 20)));
            }
            else if (effects.HasImmunity)
            {
                HudBuffsWrapPanel.Children.Add(CreateBuffBadge("✨ Immune " + effects.ImmunityDurationFormatted, Color.FromRgb(56, 189, 248), Color.FromRgb(15, 35, 50)));
            }

            // 4. Overload
            if (effects.HasOverload)
            {
                HudBuffsWrapPanel.Children.Add(CreateBuffBadge("🧪 OVL " + effects.OverloadDurationFormatted, Color.FromRgb(167, 139, 250), Color.FromRgb(35, 20, 50)));
            }

            // 5. Divine
            if (effects.HasDivine)
            {
                HudBuffsWrapPanel.Children.Add(CreateBuffBadge("⚡ Divine " + effects.DivineDurationFormatted, Color.FromRgb(96, 165, 250), Color.FromRgb(15, 25, 50)));
            }

            // 6. Imbued Heart
            if (!effects.IsImbuedHeartReady)
            {
                HudBuffsWrapPanel.Children.Add(CreateBuffBadge("💙 CD " + effects.ImbuedHeartCooldownFormatted, Color.FromRgb(248, 113, 113), Color.FromRgb(45, 15, 15)));
            }

            // 7. Prayer Enhance
            if (effects.HasPrayerEnhance)
            {
                HudBuffsWrapPanel.Children.Add(CreateBuffBadge("✨ Pray " + effects.PrayerEnhanceDurationFormatted, Color.FromRgb(0, 229, 255), Color.FromRgb(10, 40, 45)));
            }

            // 8. Charge
            if (effects.HasCharge)
            {
                HudBuffsWrapPanel.Children.Add(CreateBuffBadge("🔥 Charge " + effects.ChargeTicks + "t", Color.FromRgb(251, 146, 60), Color.FromRgb(45, 25, 10)));
            }

            HudBuffsWrapPanel.Visibility = HudBuffsWrapPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static Border CreateBuffBadge(string text, Color fgColor, Color bgColor)
        {
            return new Border
            {
                Background = new SolidColorBrush(bgColor),
                BorderBrush = new SolidColorBrush(fgColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 1, 5, 1),
                Margin = new Thickness(0, 0, 4, 3),
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = new SolidColorBrush(fgColor),
                    FontSize = 9,
                    FontWeight = FontWeights.Bold
                }
            };
        }

        private static string GetSkillIcon(string skill)
        {
            return skill.ToLowerInvariant() switch
            {
                "attack" => "⚔",
                "defence" => "🛡",
                "strength" => "💪",
                "hitpoints" => "❤️",
                "ranged" => "🏹",
                "prayer" => "✨",
                "magic" => "🔮",
                "cooking" => "🍳",
                "woodcutting" => "🪓",
                "fletching" => "🏹",
                "fishing" => "🐟",
                "firemaking" => "🔥",
                "crafting" => "💎",
                "smithing" => "🔨",
                "mining" => "⛏",
                "herblore" => "🌿",
                "agility" => "🏃",
                "thieving" => "🗝",
                "slayer" => "💀",
                "farming" => "🌱",
                "runecraft" or "runecrafting" => "🌀",
                "hunter" => "🐾",
                "construction" => "🏠",
                _ => "⭐"
            };
        }

        private static bool IsWindowValid(IntPtr hWnd)
        {
            return hWnd != IntPtr.Zero && GetWindowRect(hWnd, out var rect) && (rect.Right - rect.Left > 0);
        }

        private static IntPtr FindRuneLiteHwnd()
        {
            foreach (var proc in Process.GetProcessesByName("RuneLite"))
            {
                if (proc.MainWindowHandle != IntPtr.Zero)
                    return proc.MainWindowHandle;
            }
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// Custom lightweight UIElement for high-performance canvas painting and entity highlighting.
    /// </summary>
    public class ScriptPaintCanvas : FrameworkElement
    {
        public ScriptPaintCanvas()
        {
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var active = ScriptEngine.Instance.ActiveScript;
            if (active != null && active.Status == ScriptStatus.Running)
            {
                try
                {
                    active.OnPaint(dc);
                }
                catch
                {
                    // Prevent render exceptions from crashing the UI
                }
            }
        }
    }
}
