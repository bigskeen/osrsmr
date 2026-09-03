using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;
using OsrsMr.Core.Spatial;

namespace OsrsMr.Core.Questing
{
    public class QuestDefinition
    {
        public string Name { get; set; } = "";
        public int VarpId { get; set; }
        public int CompletedValue { get; set; }
        public WorldPoint StartLocation { get; set; } = new(3222, 3218, 0);
        public string StartNpc { get; set; } = "";
        public string[] RequiredItems { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Autonomous Quest Helper & Progression Engine.
    /// Manages quest requirements, dialogue flow progression, and NPC interactions.
    /// </summary>
    public static class QuestHelperEngine
    {
        private static GameState State => BrainEngine.Instance.State;

        private static readonly Dictionary<string, QuestDefinition> _questRegistry = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Cook's Assistant"] = new QuestDefinition
            {
                Name = "Cook's Assistant",
                VarpId = 29,
                CompletedValue = 2,
                StartLocation = new WorldPoint(3208, 3213, 0),
                StartNpc = "Cook",
                RequiredItems = new[] { "Pot of flour", "Bucket of milk", "Egg" }
            },
            ["Romeo & Juliet"] = new QuestDefinition
            {
                Name = "Romeo & Juliet",
                VarpId = 144,
                CompletedValue = 100,
                StartLocation = new WorldPoint(3218, 3422, 0),
                StartNpc = "Romeo",
                RequiredItems = new[] { "Cadava berries" }
            },
            ["Rune Mysteries"] = new QuestDefinition
            {
                Name = "Rune Mysteries",
                VarpId = 63,
                CompletedValue = 6,
                StartLocation = new WorldPoint(3210, 3220, 0),
                StartNpc = "Duke Horacio",
                RequiredItems = new[] { "Air talisman" }
            },
            ["Sheep Shearer"] = new QuestDefinition
            {
                Name = "Sheep Shearer",
                VarpId = 179,
                CompletedValue = 21,
                StartLocation = new WorldPoint(3190, 3272, 0),
                StartNpc = "Fred the Farmer",
                RequiredItems = new[] { "Ball of wool", "Shears" }
            }
        };

        /// <summary>
        /// Gets registered metadata for a given quest name.
        /// </summary>
        public static QuestDefinition? GetQuest(string questName)
        {
            _questRegistry.TryGetValue(questName, out var quest);
            return quest;
        }

        /// <summary>
        /// Checks if all required items for a quest are present in the player's inventory.
        /// </summary>
        public static bool HasAllRequiredItems(QuestDefinition quest)
        {
            if (quest.RequiredItems == null || quest.RequiredItems.Length == 0) return true;

            foreach (var item in quest.RequiredItems)
            {
                if (!InventoryActions.Contains(item)) return false;
            }

            return true;
        }

        /// <summary>
        /// Advances dialogue with a quest NPC by auto-continuing and picking the first matching option.
        /// </summary>
        public static async Task<bool> ProgressDialogAsync(params string[] targetOptions)
        {
            if (Dialogs.CanContinue())
            {
                await Dialogs.ContinueAsync();
                await Condition.SleepAsync(400, 700);
                return true;
            }

            if (Dialogs.IsChoiceOpen())
            {
                foreach (var opt in targetOptions)
                {
                    if (await Dialogs.SelectOptionAsync(opt))
                    {
                        await Condition.SleepAsync(400, 700);
                        return true;
                    }
                }

                // Default: choose first option
                await Dialogs.SelectOptionIndexAsync(1);
                await Condition.SleepAsync(400, 700);
                return true;
            }

            return false;
        }
    }
}
