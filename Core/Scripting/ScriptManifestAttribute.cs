using System;

namespace OsrsMr.Core.Scripting
{
    public enum ScriptCategory
    {
        Other,
        Combat,
        Mining,
        Woodcutting,
        Fishing,
        Cooking,
        Agility,
        Thieving,
        Magic,
        Smithing,
        Crafting,
        Fletching,
        Herblore,
        Runecrafting,
        Prayer,
        MoneyMaking,
        Minigames,
        Quests,
        Bossing,
        Clues,
        Slayer,
        Wilderness
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ScriptManifestAttribute : Attribute
    {
        public string Name { get; }
        public string Author { get; }
        public string Version { get; }
        public string Description { get; }
        public ScriptCategory Category { get; }

        public ScriptManifestAttribute(
            string name,
            string author = "Community",
            string version = "1.0.0",
            string description = "",
            ScriptCategory category = ScriptCategory.Other)
        {
            Name = name;
            Author = author;
            Version = version;
            Description = description;
            Category = category;
        }
    }
}
