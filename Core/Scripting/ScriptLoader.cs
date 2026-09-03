using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OsrsMr.Core.Scripting
{
    /// <summary>
    /// Discovers and dynamically loads external Bot Scripts from precompiled .dll assemblies or runtime plugins in Scripts/.
    /// </summary>
    public static class ScriptLoader
    {
        public static string ScriptsDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts");

        /// <summary>
        /// Ensures the external Scripts directory exists.
        /// </summary>
        public static void EnsureScriptsDirectory()
        {
            if (!Directory.Exists(ScriptsDirectory))
            {
                Directory.CreateDirectory(ScriptsDirectory);
            }
        }

        /// <summary>
        /// Scans built-in assemblies and the external Scripts/ folder for all available bot scripts.
        /// </summary>
        public static List<ScriptMetadata> LoadAllScripts()
        {
            EnsureScriptsDirectory();
            var results = new List<ScriptMetadata>();

            // 1. Scan current executing assembly (built-in starter scripts)
            results.AddRange(DiscoverScriptsFromAssembly(Assembly.GetExecutingAssembly()));

            // 2. Scan external precompiled .dll assemblies in Scripts/
            foreach (var dllPath in Directory.GetFiles(ScriptsDirectory, "*.dll", SearchOption.AllDirectories))
            {
                try
                {
                    var asm = Assembly.LoadFrom(dllPath);
                    results.AddRange(DiscoverScriptsFromAssembly(asm));
                }
                catch (Exception)
                {
                    // Ignore unloadable DLLs
                }
            }

            return results.GroupBy(s => s.Name).Select(g => g.First()).ToList();
        }

        private static IEnumerable<ScriptMetadata> DiscoverScriptsFromAssembly(Assembly asm)
        {
            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).Select(t => t!).ToArray();
            }
            catch
            {
                yield break;
            }

            var scriptTypes = types.Where(t => typeof(BotScript).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass);

            foreach (var type in scriptTypes)
            {
                var manifest = type.GetCustomAttribute<ScriptManifestAttribute>() ?? new ScriptManifestAttribute(type.Name);
                yield return new ScriptMetadata
                {
                    ScriptType = type,
                    Name = manifest.Name,
                    Author = manifest.Author,
                    Version = manifest.Version,
                    Category = manifest.Category,
                    Description = manifest.Description
                };
            }
        }
    }

    /// <summary>
    /// Descriptor for a discovered bot script.
    /// </summary>
    public class ScriptMetadata
    {
        public Type ScriptType { get; set; } = null!;
        public string Name { get; set; } = "";
        public string Author { get; set; } = "";
        public string Version { get; set; } = "";
        public ScriptCategory Category { get; set; } = ScriptCategory.Other;
        public string Description { get; set; } = "";

        public string CategoryName => Category.ToString();

        public string CategoryIcon => Category switch
        {
            ScriptCategory.Mining => "⛏️",
            ScriptCategory.Woodcutting => "🪓",
            ScriptCategory.Fishing => "🎣",
            ScriptCategory.Combat => "⚔️",
            ScriptCategory.Magic => "✨",
            ScriptCategory.Minigames => "🏆",
            ScriptCategory.Thieving => "🗝️",
            ScriptCategory.Agility => "🏃",
            ScriptCategory.Cooking => "🍖",
            ScriptCategory.Crafting => "💎",
            ScriptCategory.Fletching => "🏹",
            ScriptCategory.Herblore => "🌿",
            ScriptCategory.Runecrafting => "🔮",
            ScriptCategory.Prayer => "✨",
            ScriptCategory.Bossing => "🐉",
            ScriptCategory.Clues => "📜",
            ScriptCategory.Quests => "🗺️",
            ScriptCategory.Slayer => "💀",
            ScriptCategory.Wilderness => "☠️",
            _ => "🤖"
        };

        public string CategoryBadgeColor => Category switch
        {
            ScriptCategory.Mining => "#2B1A0E",
            ScriptCategory.Woodcutting => "#1E331E",
            ScriptCategory.Fishing => "#102A43",
            ScriptCategory.Combat => "#3D1217",
            ScriptCategory.Magic => "#2A183D",
            ScriptCategory.Minigames => "#332B10",
            ScriptCategory.Agility => "#113A35",
            ScriptCategory.Thieving => "#1E2A33",
            ScriptCategory.Cooking => "#3D1D0B",
            ScriptCategory.Smithing => "#2B1D16",
            ScriptCategory.Fletching => "#1A3311",
            ScriptCategory.Herblore => "#0D3315",
            ScriptCategory.Runecrafting => "#220D3D",
            ScriptCategory.Bossing => "#3B0815",
            ScriptCategory.Clues => "#332E0B",
            ScriptCategory.Quests => "#0B2733",
            ScriptCategory.Slayer => "#380D22",
            ScriptCategory.Wilderness => "#380D0D",
            _ => "#1F2433"
        };

        public string CategoryTextColor => Category switch
        {
            ScriptCategory.Mining => "#F59E0B",
            ScriptCategory.Woodcutting => "#34D399",
            ScriptCategory.Fishing => "#38BDF8",
            ScriptCategory.Combat => "#F87171",
            ScriptCategory.Magic => "#C084FC",
            ScriptCategory.Minigames => "#FBBF24",
            ScriptCategory.Agility => "#2DD4BF",
            ScriptCategory.Thieving => "#94A3B8",
            ScriptCategory.Cooking => "#FB923C",
            ScriptCategory.Smithing => "#A8A29E",
            ScriptCategory.Fletching => "#84CC16",
            ScriptCategory.Herblore => "#4ADE80",
            ScriptCategory.Runecrafting => "#A78BFA",
            ScriptCategory.Bossing => "#FB7185",
            ScriptCategory.Clues => "#FACC15",
            ScriptCategory.Quests => "#38BDF8",
            ScriptCategory.Slayer => "#F43F5E",
            ScriptCategory.Wilderness => "#EF4444",
            _ => "#00E5FF"
        };

        public BotScript? Instantiate()
        {
            try
            {
                return (BotScript?)Activator.CreateInstance(ScriptType);
            }
            catch
            {
                return null;
            }
        }

        public override string ToString() => $"[{Category}] {Name} v{Version} (by {Author})";
    }
}
