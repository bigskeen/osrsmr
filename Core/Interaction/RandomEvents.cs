using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Input;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;
using OsrsMr.Core.Spatial;

namespace OsrsMr.Core.Interaction
{
    /// <summary>
    /// Configuration policy for handling random event NPCs.
    /// </summary>
    public enum RandomEventHandling
    {
        Dismiss,
        Ignore,
        RunAway
    }

    /// <summary>
    /// Interaction controller for detecting and handling OSRS random event NPCs.
    /// </summary>
    public static class RandomEvents
    {
        private static GameState State => BrainEngine.Instance.State;

        private static readonly HashSet<string> KnownRandomNpcNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Genie",
            "Mysterious Old Man",
            "Rick Turpentine",
            "Dr Jekyll",
            "Dr. Jekyll",
            "Dunce",
            "Cap'n Arnav",
            "Sandwich Lady",
            "Miles",
            "Giles",
            "Niles",
            "Postie Pete",
            "Drunken Dwarf",
            "Frog",
            "Freaky Forester",
            "Flippa",
            "Tilt",
            "Sergeant Damien",
            "Mime",
            "Beekeeper",
            "Bee keeper",
            "Pinhead",
            "Evil Bob",
            "Quiz Master",
            "Strange Plant",
            "Swarm",
            "Zombie",
            "Evil Chicken",
            "River Troll",
            "Rock Golem",
            "Tree Spirit",
            "Shade"
        };

        /// <summary>
        /// Checks whether a random event NPC is currently within 6 tiles of the player.
        /// </summary>
        public static bool IsRandomEventPresent()
        {
            return Queries.Queries.Npcs
                .WithinDistance(6)
                .Filter(npc => KnownRandomNpcNames.Contains(npc.Name))
                .Any();
        }

        /// <summary>
        /// Gets the nearest active random event NPC if present.
        /// </summary>
        public static NpcSnapshot? GetActiveRandomNpc()
        {
            return Queries.Queries.Npcs
                .WithinDistance(6)
                .Filter(npc => KnownRandomNpcNames.Contains(npc.Name))
                .Nearest();
        }

        /// <summary>
        /// Executes the configured random event handling policy.
        /// Returns true if a random event was handled/processed.
        /// </summary>
        public static async Task<bool> HandleRandomEventAsync(RandomEventHandling policy)
        {
            if (policy == RandomEventHandling.Ignore)
            {
                return false;
            }

            var randomNpc = GetActiveRandomNpc();
            if (randomNpc == null) return false;

            if (policy == RandomEventHandling.Dismiss)
            {
                // Try interacting with "Dismiss"
                bool dismissed = await randomNpc.InteractAsync("Dismiss");
                if (!dismissed)
                {
                    // Fallback to "Talk-to" if dismiss is not top default
                    await randomNpc.InteractAsync("Talk-to");
                }
                await Condition.SleepAsync(800, 1200);
                return true;
            }
            else if (policy == RandomEventHandling.RunAway)
            {
                // Walk ~10 tiles away in a safe direction to force the random event to despawn
                if (State.Player != null)
                {
                    int targetX = State.Player.WorldX + 9;
                    int targetY = State.Player.WorldY + 9;
                    await Movement.WalkToAsync(targetX, targetY);
                    await Condition.SleepAsync(2000, 3000);
                    return true;
                }
            }

            return false;
        }
    }
}
