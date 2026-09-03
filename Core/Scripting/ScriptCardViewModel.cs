using System;
using System.Windows.Media;

namespace OsrsMr.Core.Scripting
{
    public class ScriptCardViewModel
    {
        public ScriptMetadata Metadata { get; set; } = null!;
        public string Name => Metadata.Name;
        public string Author => string.IsNullOrWhiteSpace(Metadata.Author) ? "Community" : Metadata.Author;
        public string Version => string.IsNullOrWhiteSpace(Metadata.Version) ? "1.0" : Metadata.Version;
        public ScriptCategory Category => Metadata.Category;
        public string CategoryName => Metadata.Category.ToString();
        public string Description => string.IsNullOrWhiteSpace(Metadata.Description) ? "Automated bot script for Old School RuneScape." : Metadata.Description;

        public string CategoryIcon => Category switch
        {
            ScriptCategory.Mining => "⛏️",
            ScriptCategory.Woodcutting => "🪓",
            ScriptCategory.Fishing => "🎣",
            ScriptCategory.Combat => "⚔️",
            ScriptCategory.Magic => "✨",
            ScriptCategory.Cooking => "🍳",
            ScriptCategory.Agility => "🏃",
            ScriptCategory.Thieving => "🗝️",
            ScriptCategory.Smithing => "🔨",
            ScriptCategory.Crafting => "🧵",
            ScriptCategory.Fletching => "🏹",
            ScriptCategory.Herblore => "🌿",
            ScriptCategory.Runecrafting => "🔮",
            ScriptCategory.Prayer => "✨",
            ScriptCategory.Minigames => "🏆",
            ScriptCategory.MoneyMaking => "💰",
            ScriptCategory.Quests => "📜",
            _ => "⚡"
        };

        public SolidColorBrush CategoryBadgeColor => Category switch
        {
            ScriptCategory.Mining => new SolidColorBrush(Color.FromRgb(62, 39, 35)),
            ScriptCategory.Woodcutting => new SolidColorBrush(Color.FromRgb(46, 125, 50)),
            ScriptCategory.Fishing => new SolidColorBrush(Color.FromRgb(2, 119, 189)),
            ScriptCategory.Combat => new SolidColorBrush(Color.FromRgb(198, 40, 40)),
            ScriptCategory.Magic => new SolidColorBrush(Color.FromRgb(106, 27, 154)),
            ScriptCategory.Cooking => new SolidColorBrush(Color.FromRgb(230, 81, 0)),
            ScriptCategory.Agility => new SolidColorBrush(Color.FromRgb(38, 166, 154)),
            ScriptCategory.Thieving => new SolidColorBrush(Color.FromRgb(120, 144, 156)),
            ScriptCategory.Smithing => new SolidColorBrush(Color.FromRgb(93, 64, 55)),
            ScriptCategory.Crafting => new SolidColorBrush(Color.FromRgb(141, 110, 99)),
            ScriptCategory.Fletching => new SolidColorBrush(Color.FromRgb(51, 105, 30)),
            ScriptCategory.Herblore => new SolidColorBrush(Color.FromRgb(27, 94, 32)),
            ScriptCategory.Runecrafting => new SolidColorBrush(Color.FromRgb(74, 20, 140)),
            ScriptCategory.Prayer => new SolidColorBrush(Color.FromRgb(255, 214, 0)),
            ScriptCategory.Minigames => new SolidColorBrush(Color.FromRgb(123, 31, 162)),
            ScriptCategory.MoneyMaking => new SolidColorBrush(Color.FromRgb(249, 168, 37)),
            ScriptCategory.Quests => new SolidColorBrush(Color.FromRgb(0, 137, 123)),
            _ => new SolidColorBrush(Color.FromRgb(55, 71, 79))
        };

        public SolidColorBrush CategoryTextColor => Category switch
        {
            ScriptCategory.MoneyMaking => new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            ScriptCategory.Prayer => new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            _ => Brushes.White
        };
    }
}
