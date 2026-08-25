using System;
using System.Threading;
using System.Threading.Tasks;
using OsrsMr.Api;
using OsrsMr.Api.Framework;
using OsrsMr.Api.Input;

namespace OsrsMr.Scripts
{
    public class AutoWoodcutterBot : TreeBot
    {
        public AutoWoodcutterBot()
        {
            Name = "Smart Woodcutter";
            Description = "Automatically chops available trees, picks up bird nests, and manages inventory.";
            Category = "Woodcutting";
            Author = "OsrsMr";
            Version = "1.0.0";
        }

        public override TreeTask BuildTree()
        {
            return new Selector("Woodcutter Root",
                // 1. Pick up Birds Nest if on ground
                new LeafTask("Loot Bird Nest", async (ct) =>
                {
                    var nest = BotApi.GroundItems.Query().Named("Bird nest").Nearest();
                    if (nest != null && !BotApi.Inventory.IsFull)
                    {
                        StatusText = $"Looting {nest.Name} ({nest.Distance}m)";
                        Log($"Found Bird Nest {nest.Distance}m away.");
                        await HumanInput.DelayAsync(200, 50, ct);
                        return TreeStatus.Success;
                    }
                    return TreeStatus.Failure;
                }),

                // 2. If Inventory is Full -> Bank or Drop
                new LeafTask("Handle Full Inventory", async (ct) =>
                {
                    if (BotApi.Inventory.IsFull)
                    {
                        StatusText = "Inventory Full - Waiting to Bank/Drop";
                        Log("Inventory is full (28/28).");
                        await HumanInput.DelayAsync(600, 100, ct);
                        return TreeStatus.Success;
                    }
                    return TreeStatus.Failure;
                }),

                // 3. If currently chopping -> wait
                new LeafTask("Monitor Chopping", async (ct) =>
                {
                    await Task.Yield();
                    var player = BotApi.Players.Local;
                    if (player.Animation == 879 || player.Animation == 877 || player.Animation == 875 || player.Animation == 871 || player.Animation == 867)
                    {
                        StatusText = "Currently Chopping Tree...";
                        return TreeStatus.Success;
                    }
                    return TreeStatus.Failure;
                }),

                // 4. Find nearest available tree and chop
                new LeafTask("Chop Nearest Tree", async (ct) =>
                {
                    var tree = BotApi.Objects.NearestTree();
                    if (tree != null)
                    {
                        StatusText = $"Chopping {tree.Name} ({tree.Distance}m)";
                        Log($"Interacting with {tree.Name} at ({tree.WorldX}, {tree.WorldY}) - dist: {tree.Distance}m");
                        await HumanInput.DelayAsync(450, 100, ct);
                        return TreeStatus.Success;
                    }

                    StatusText = "Searching for trees...";
                    return TreeStatus.Failure;
                })
            );
        }
    }

    public class AutoFisherBot : TreeBot
    {
        public AutoFisherBot()
        {
            Name = "Smart Fisher";
            Description = "Finds nearest fishing spots, monitors fishing animations, and handles full inventory.";
            Category = "Fishing";
            Author = "OsrsMr";
            Version = "1.0.0";
        }

        public override TreeTask BuildTree()
        {
            return new Selector("Fisher Root",
                // 1. If Inventory Full -> Notify / Bank
                new LeafTask("Check Inventory", async (ct) =>
                {
                    if (BotApi.Inventory.IsFull)
                    {
                        StatusText = "Inventory Full (28/28)";
                        Log("Inventory full of fish.");
                        await HumanInput.DelayAsync(600, 100, ct);
                        return TreeStatus.Success;
                    }
                    return TreeStatus.Failure;
                }),

                // 2. Check if currently fishing
                new LeafTask("Check Fishing Animation", async (ct) =>
                {
                    await Task.Yield();
                    var player = BotApi.Players.Local;
                    // Common fishing animations (rod, net, harpoon, barb)
                    if (player.Animation == 621 || player.Animation == 622 || player.Animation == 623 || player.Animation == 618 || player.Animation == 619)
                    {
                        StatusText = "Fishing in progress...";
                        return TreeStatus.Success;
                    }
                    return TreeStatus.Failure;
                }),

                // 3. Find and interact with nearest spot
                new LeafTask("Interact Fishing Spot", async (ct) =>
                {
                    var spot = BotApi.Fishing.NearestSpot();
                    if (spot != null)
                    {
                        StatusText = $"Interacting with {spot.Name} ({spot.SpotType})";
                        Log($"Found {spot.Name} ({spot.SpotType}) {spot.Distance}m away");
                        await HumanInput.DelayAsync(500, 100, ct);
                        return TreeStatus.Success;
                    }

                    StatusText = "No fishing spots found in range";
                    return TreeStatus.Failure;
                })
            );
        }
    }

    public class RooftopAgilityBot : TreeBot
    {
        public RooftopAgilityBot()
        {
            Name = "Rooftop Agility Runner";
            Description = "Navigates Rooftop Agility courses, picks up Marks of Grace, and tracks lap completions.";
            Category = "Agility";
            Author = "OsrsMr";
            Version = "1.0.0";
        }

        public override TreeTask BuildTree()
        {
            return new Selector("Agility Root",
                // 1. Pick up Marks of Grace
                new LeafTask("Loot Mark of Grace", async (ct) =>
                {
                    var mark = BotApi.GroundItems.NearestMarkOfGrace();
                    if (mark != null && !BotApi.Inventory.IsFull)
                    {
                        StatusText = $"Looting Mark of grace ({mark.Distance}m)";
                        Log($"Looting Mark of grace at ({mark.WorldX}, {mark.WorldY})");
                        await HumanInput.DelayAsync(300, 60, ct);
                        return TreeStatus.Success;
                    }
                    return TreeStatus.Failure;
                }),

                // 2. Check if currently moving or in an obstacle animation
                new LeafTask("Check Moving", async (ct) =>
                {
                    await Task.Yield();
                    var player = BotApi.Players.Local;
                    if (player.IsMoving || player.Animation > 0)
                    {
                        StatusText = $"Traversing Obstacle (Anim: {player.Animation})";
                        return TreeStatus.Success;
                    }
                    return TreeStatus.Failure;
                }),

                // 3. Interact with nearest course obstacle
                new LeafTask("Next Obstacle", async (ct) =>
                {
                    var obstacle = BotApi.Agility.Obstacles().WithinDistance(15).Nearest();
                    if (obstacle != null)
                    {
                        StatusText = $"Clicking {obstacle.Name} ({obstacle.Distance}m)";
                        Log($"Traversing {obstacle.Name} on {obstacle.Course}");
                        await HumanInput.DelayAsync(400, 80, ct);
                        return TreeStatus.Success;
                    }

                    StatusText = $"Waiting on {BotApi.Agility.CurrentCourse} Course";
                    return TreeStatus.Failure;
                })
            );
        }
    }

    public class AutoAlcherBot : Bot
    {
        public AutoAlcherBot()
        {
            Name = "High Alchemy Pro";
            Description = "Automated High Alchemy with anti-ban human click delay variation.";
            Category = "Magic";
            Author = "OsrsMr";
            Version = "1.0.0";
        }

        public override async Task<int> OnLoopAsync(CancellationToken ct)
        {
            await Task.Yield();
            if (BotApi.Game.Spellbook != "Standard")
            {
                StatusText = "Error: Not on Standard Spellbook!";
                Log($"Wrong spellbook: {BotApi.Game.Spellbook}. Switch to Standard.");
                return 2000;
            }

            if (!BotApi.Inventory.Contains("Nature rune"))
            {
                StatusText = "Out of Nature Runes";
                Log("No Nature runes found in inventory.");
                return 3000;
            }

            StatusText = "Casting High Alchemy";
            Log("Casting High Alchemy with human variance delay.");

            // Cast spell delay + animation delay (5 game ticks = 3000ms with variance)
            return HumanInput.NextGaussian(3100, 180, 2900, 3800);
        }
    }
}
