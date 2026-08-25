using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OsrsMr.Api.Entities;
using OsrsMr.Api.Input;
using OsrsMr.Api.Queries;
using OsrsMr.Core;

namespace OsrsMr.Api
{
    public static class BotApi
    {
        private static GameState State => BrainEngine.Instance.State;

        public static class Game
        {
            public static bool IsLoggedIn => State.IsLoggedIn;
            public static string ActiveTab => State.Player.ActiveTab;
            public static string Spellbook => State.Player.Spellbook;
            public static string AutocastSpell => State.Player.AutocastSpell;
            public static int RunEnergy => State.Player.Energy;
            public static int SpecPercent => State.Player.SpecPercent;
            public static bool IsSpecActive => State.Player.IsSpecActive;
            public static int Weight => State.Player.Weight;
            public static DateTime LastUpdated => State.LastUpdated;
        }

        public static class Players
        {
            public static PlayerEntity Local => new()
            {
                Name = State.Player.Name,
                CombatLevel = State.Player.CombatLevel,
                CurrentHp = State.Player.CurrentHp,
                MaxHp = State.Player.MaxHp,
                CurrentPrayer = State.Player.CurrentPrayer,
                MaxPrayer = State.Player.MaxPrayer,
                Energy = State.Player.Energy,
                Weight = State.Player.Weight,
                SpecPercent = State.Player.SpecPercent,
                IsSpecActive = State.Player.IsSpecActive,
                Spellbook = State.Player.Spellbook,
                AutocastSpell = State.Player.AutocastSpell,
                ActiveTab = State.Player.ActiveTab,
                Animation = State.Player.Animation,
                WorldX = State.Player.WorldX,
                WorldY = State.Player.WorldY,
                Plane = State.Player.Plane,
                IsInteracting = State.Player.IsInteracting,
                InteractingName = State.Player.InteractingName
            };
        }

        public static class Combat
        {
            public static int SpecialAttackEnergy => State.Player.SpecPercent;
            public static bool IsSpecialAttackActive => State.Player.IsSpecActive;

            public static async Task<bool> ToggleSpecialAttackAsync(CancellationToken ct = default)
            {
                await HumanInput.SimulateHumanDelayAsync(150, 300, ct);
                return true;
            }
        }

        public static class Prayers
        {
            public static async Task<bool> ToggleAsync(string prayerName, CancellationToken ct = default)
            {
                await HumanInput.SimulateHumanDelayAsync(150, 300, ct);
                return true;
            }
        }

        public static class Magic
        {
            public static async Task<bool> CastHighAlchemyAsync(string itemName, CancellationToken ct = default)
            {
                await HumanInput.SimulateHumanDelayAsync(250, 500, ct);
                return true;
            }

            public static async Task<bool> CastOnInventoryItemAsync(string spellName, string itemName, CancellationToken ct = default)
            {
                await HumanInput.SimulateHumanDelayAsync(250, 500, ct);
                return true;
            }

            public static async Task<bool> CastTeleportAsync(string spellName, CancellationToken ct = default)
            {
                await HumanInput.SimulateHumanDelayAsync(300, 600, ct);
                return true;
            }
        }

        public static class Movement
        {
            public static async Task<bool> WalkToAsync(int worldX, int worldY, CancellationToken ct = default)
            {
                await HumanInput.SimulateHumanDelayAsync(300, 600, ct);
                return true;
            }
        }

        public static class Npcs
        {
            public static EntityQuery<NpcEntity> Query()
            {
                var list = State.Npcs.Values.Select(n => new NpcEntity
                {
                    Index = n.Index,
                    Id = n.Id,
                    Name = n.Name,
                    CombatLevel = n.CombatLevel,
                    Distance = n.Distance,
                    WorldX = n.WorldX,
                    WorldY = n.WorldY,
                    Animation = n.Animation,
                    Role = n.Role
                });
                return new EntityQuery<NpcEntity>(list);
            }

            public static NpcEntity? Nearest(string? name = null)
            {
                var q = Query();
                if (!string.IsNullOrEmpty(name)) q = q.Named(name);
                return q.Nearest();
            }

            public static NpcEntity? NearestBanker() => Query().Filter(n => n.IsBanker).Nearest();
            public static NpcEntity? NearestSlayerMaster() => Query().Filter(n => n.IsSlayerMaster).Nearest();
            public static NpcEntity? NearestShopkeeper() => Query().Filter(n => n.IsShopkeeper).Nearest();
        }

        public static class Objects
        {
            public static EntityQuery<GameObjectEntity> Query()
            {
                var list = State.Objects.Values.Select(o => new GameObjectEntity
                {
                    Id = o.Id,
                    Name = o.Name,
                    Category = o.Category,
                    Status = o.Status,
                    Distance = o.Distance,
                    WorldX = o.WorldX,
                    WorldY = o.WorldY,
                    Plane = o.Plane,
                    RequiredLevel = o.RequiredLevel
                });
                return new EntityQuery<GameObjectEntity>(list);
            }

            public static GameObjectEntity? Nearest(string? name = null)
            {
                var q = Query();
                if (!string.IsNullOrEmpty(name)) q = q.Named(name);
                return q.Nearest();
            }
            public static GameObjectEntity? NearestTree(string? treeName = null)
            {
                var q = Query().Filter(o => o.IsAvailableTree);
                if (!string.IsNullOrEmpty(treeName)) q = q.Named(treeName);
                return q.Nearest();
            }
            public static GameObjectEntity? NearestBank() => Query().Filter(o => o.IsBank).Nearest();
        }

        public static class GroundItems
        {
            public static EntityQuery<GroundItemEntity> Query()
            {
                var list = State.GroundItems.Values.Select(gi => new GroundItemEntity
                {
                    Id = gi.Id,
                    Name = gi.Name,
                    Quantity = gi.Quantity,
                    Distance = gi.Distance,
                    WorldX = gi.WorldX,
                    WorldY = gi.WorldY,
                    GePrice = gi.GePrice
                });
                return new EntityQuery<GroundItemEntity>(list);
            }

            public static GroundItemEntity? Nearest(string? name = null)
            {
                var q = Query();
                if (!string.IsNullOrEmpty(name)) q = q.Named(name);
                return q.Nearest();
            }
            public static GroundItemEntity? NearestMarkOfGrace() => Query().Named("Mark of grace").Nearest();
        }

        public static class Inventory
        {
            public static EntityQuery<ItemEntity> Query()
            {
                var list = State.Inventory.Values.Select(i => new ItemEntity
                {
                    Slot = i.Slot,
                    Id = i.Id,
                    Name = i.Name,
                    Quantity = i.Quantity,
                    HighAlchValue = i.HighAlchValue,
                    GePrice = i.GePrice
                });
                return new EntityQuery<ItemEntity>(list);
            }

            public static bool Contains(string name) => Query().Named(name).Exists();
            public static bool Contains(int id) => Query().WithId(id).Exists();
            public static int Count(string name) => Query().Named(name).ToList().Sum(i => i.Quantity);
            public static int GetCount(string name) => Count(name);
            public static int TotalItems => State.Inventory.Count;
            public static int FreeSlots => Math.Max(0, 28 - State.Inventory.Count);
            public static bool IsFull => State.Inventory.Count >= 28;
            public static bool IsEmpty => State.Inventory.IsEmpty;
            public static ItemEntity? GetItemAt(int slot)
            {
                if (State.Inventory.TryGetValue(slot, out var it))
                {
                    return new ItemEntity { Slot = it.Slot, Id = it.Id, Name = it.Name, Quantity = it.Quantity };
                }
                return null;
            }
        }

        public static class Bank
        {
            public static bool IsOpen => State.IsBankOpen;
            public static EntityQuery<ItemEntity> Query()
            {
                var list = State.Bank.Values.Select(i => new ItemEntity
                {
                    Slot = i.Slot,
                    Id = i.Id,
                    Name = i.Name,
                    Quantity = i.Quantity,
                    HighAlchValue = i.HighAlchValue,
                    GePrice = i.GePrice
                });
                return new EntityQuery<ItemEntity>(list);
            }

            public static bool Contains(string name) => Query().Named(name).Exists();
            public static int Count(string name) => Query().Named(name).ToList().Sum(i => i.Quantity);
            public static int GetCount(string name) => Count(name);

            public static async Task<bool> DepositAllAsync(CancellationToken ct = default)
            {
                await HumanInput.SimulateHumanDelayAsync(250, 450, ct);
                return true;
            }

            public static async Task<bool> DepositAllExceptAsync(IEnumerable<string> keepNames, CancellationToken ct = default)
            {
                await HumanInput.SimulateHumanDelayAsync(300, 600, ct);
                return true;
            }

            public static async Task<bool> DepositEquipmentAsync(CancellationToken ct = default)
            {
                await HumanInput.SimulateHumanDelayAsync(200, 400, ct);
                return true;
            }

            public static async Task<bool> WithdrawAsync(string itemName, int quantity, CancellationToken ct = default)
            {
                await HumanInput.SimulateHumanDelayAsync(250, 500, ct);
                return true;
            }

            public static async Task<bool> WithdrawAllAsync(string itemName, CancellationToken ct = default)
            {
                await HumanInput.SimulateHumanDelayAsync(250, 450, ct);
                return true;
            }

            public static async Task<bool> WithdrawAllButOneAsync(string itemName, CancellationToken ct = default)
            {
                await HumanInput.SimulateHumanDelayAsync(250, 450, ct);
                return true;
            }

            public static async Task<bool> CloseAsync(CancellationToken ct = default)
            {
                await HumanInput.SimulateHumanDelayAsync(150, 300, ct);
                return true;
            }
        }

        public static class Dialog
        {
            public static bool IsOpen => State.Dialog.IsOpen;
            public static string Type => State.Dialog.Type;
            public static string Speaker => State.Dialog.Speaker;
            public static string Text => State.Dialog.Text;
            public static IReadOnlyList<string> Options => State.Dialog.Options;
            public static bool HasOptions => State.Dialog.Options.Count > 0;

            public static async Task<bool> PressSpaceAsync(CancellationToken ct = default)
            {
                await HumanInput.SimulateHumanDelayAsync(200, 400, ct);
                return true;
            }

            public static async Task<bool> SelectOptionAsync(int optionIndex, CancellationToken ct = default)
            {
                await HumanInput.SimulateHumanDelayAsync(250, 450, ct);
                return true;
            }
        }

        public static class Skills
        {
            public static int GetLevel(string skill) => State.Skills.TryGetValue(skill, out var s) ? s.Level : 1;
            public static int GetBoostedLevel(string skill) => State.Skills.TryGetValue(skill, out var s) ? s.BoostedLevel : 1;
            public static int GetXp(string skill) => State.Skills.TryGetValue(skill, out var s) ? s.Experience : 0;
        }

        public static class Agility
        {
            public static string CurrentCourse => State.Agility.CurrentCourse;
            public static int CourseLevelReq => State.Agility.CourseLevelReq;
            public static int MarksOfGraceNearby => State.Agility.MarksOfGraceNearby;

            public static EntityQuery<AgilityObstacleEntity> Obstacles()
            {
                var list = State.AgilityObstacles.Values.Select(o => new AgilityObstacleEntity
                {
                    Id = o.Id,
                    Name = o.Name,
                    Course = o.Status,
                    Distance = o.Distance,
                    WorldX = o.WorldX,
                    WorldY = o.WorldY
                });
                return new EntityQuery<AgilityObstacleEntity>(list);
            }

            public static AgilityObstacleEntity? NearestObstacle(string? name = null)
            {
                var q = Obstacles();
                if (!string.IsNullOrEmpty(name)) q = q.Named(name);
                return q.Nearest();
            }

            public static EntityQuery<ShortcutEntity> Shortcuts()
            {
                var list = State.Shortcuts.Values.Select(s => new ShortcutEntity
                {
                    Id = s.Id,
                    Name = s.Name,
                    RequiredLevel = int.TryParse(s.RequiredLevel, out int lvl) ? lvl : 1,
                    Distance = s.Distance,
                    WorldX = s.WorldX,
                    WorldY = s.WorldY
                });
                return new EntityQuery<ShortcutEntity>(list);
            }
        }

        public static class Fishing
        {
            public static EntityQuery<FishingSpotEntity> Spots()
            {
                var list = State.FishingSpots.Values.Select(f => new FishingSpotEntity
                {
                    Id = f.Id,
                    Name = f.Name,
                    SpotType = f.SpotType,
                    Distance = f.Distance,
                    WorldX = f.WorldX,
                    WorldY = f.WorldY
                });
                return new EntityQuery<FishingSpotEntity>(list);
            }

            public static FishingSpotEntity? NearestSpot(string? spotType = null)
            {
                var q = Spots();
                if (!string.IsNullOrEmpty(spotType))
                    q = q.Filter(s => s.SpotType.Contains(spotType, StringComparison.OrdinalIgnoreCase) || s.Name.Contains(spotType, StringComparison.OrdinalIgnoreCase));
                return q.Nearest();
            }
        }

        public static class Minigames
        {
            public static bool IsActive => State.Minigame.IsActive;
            public static string Name => State.Minigame.Name;
            public static string Status => State.Minigame.Status;
            public static string Points => State.Minigame.Points;
            public static string Extra => State.Minigame.Extra;
        }

        public static class Slayer
        {
            public static string TaskName => State.Slayer.TaskName;
            public static int AmountRemaining => State.Slayer.AmountRemaining;
            public static string Master => State.Slayer.Master;
            public static int Points => State.Slayer.Points;
        }
    }
}
