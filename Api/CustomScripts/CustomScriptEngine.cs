using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using OsrsMr.Api.Entities;
using OsrsMr.Api.Framework;
using OsrsMr.Api.Input;

namespace OsrsMr.Api.CustomScripts
{
    public enum CustomActionType
    {
        ChopObject,
        MineObject,
        ClickObject,
        AttackNpc,
        TalkNpc,
        DropItem,
        DropAllOfItem,
        DropAllExcept,
        EatFood,
        DrinkPotion,
        TogglePrayer,
        ToggleSpecialAttack,
        EquipItem,
        CleanHerb,
        UseItemOnItem,
        FletchItem,
        CookFood,
        SmeltOre,
        AlchItem,
        LootGroundItem,
        LootAllConfigured,
        OpenNearestBank,
        BankDepositAll,
        BankDepositAllExcept,
        BankDepositEquipment,
        BankWithdrawItem,
        BankWithdrawAll,
        BankWithdrawAllButOne,
        CloseBank,
        CastSpellOnItem,
        CastTeleport,
        RunAgilityObstacle,
        WalkToCoords,
        WalkToBank,
        ContinueDialog,
        SelectDialogOption,
        WaitSeconds,
        WaitForIdle
    }

    public enum CustomConditionType
    {
        Always,
        InventoryFull,
        InventoryNotFull,
        InventoryHasItem,
        InventoryDoesNotHaveItem,
        InventoryCountLessThan,
        InventoryCountGreaterThan,
        PlayerIsIdle,
        PlayerIsNotIdle,
        PlayerInCombat,
        PlayerNotInCombat,
        TargetInCombat,
        HpBelowPercent,
        PrayerBelow,
        SpecialAttackAbove,
        RunEnergyBelow,
        BankIsOpen,
        BankIsClosed,
        DialogIsOpen,
        GroundItemNearby,
        Poisoned
    }

    public class CustomScriptConfigField
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string FieldType { get; set; } = "Text"; // Text, Number, Dropdown, Checkbox, BankingOption
        public string DefaultValue { get; set; } = "";
        public string Value { get; set; } = "";
        public List<string> Options { get; set; } = new();
        public string Description { get; set; } = "";
        public string Category { get; set; } = "General";
    }

    public class CustomActionStep
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "Action Step";
        public bool Enabled { get; set; } = true;
        public string ActionCategory { get; set; } = "General";
        public CustomActionType ActionType { get; set; } = CustomActionType.ChopObject;
        public CustomConditionType Condition { get; set; } = CustomConditionType.Always;
        public string ConditionArg { get; set; } = "";
        public string TargetName { get; set; } = "Tree";
        public string ActionVerb { get; set; } = "Chop down";
        public string Param1 { get; set; } = "";
        public string Param2 { get; set; } = "";
        public int WaitAfterMs { get; set; } = 1200;
        public bool WaitForAnimation { get; set; } = true;

        [JsonIgnore]
        public string IconEmoji
        {
            get
            {
                return ActionType switch
                {
                    CustomActionType.AttackNpc => "⚔️",
                    CustomActionType.ChopObject => "🪓",
                    CustomActionType.MineObject => "⛏️",
                    CustomActionType.ClickObject => "🎯",
                    CustomActionType.TalkNpc => "💬",
                    CustomActionType.EatFood => "🍗",
                    CustomActionType.DrinkPotion => "🧪",
                    CustomActionType.TogglePrayer => "🛡️",
                    CustomActionType.ToggleSpecialAttack => "💥",
                    CustomActionType.EquipItem => "🗡️",
                    CustomActionType.DropItem or CustomActionType.DropAllOfItem or CustomActionType.DropAllExcept => "🗑️",
                    CustomActionType.CleanHerb or CustomActionType.FletchItem or CustomActionType.UseItemOnItem => "🌿",
                    CustomActionType.CookFood or CustomActionType.SmeltOre => "🔥",
                    CustomActionType.AlchItem or CustomActionType.CastSpellOnItem or CustomActionType.CastTeleport => "✨",
                    CustomActionType.LootGroundItem or CustomActionType.LootAllConfigured => "📦",
                    CustomActionType.OpenNearestBank or CustomActionType.BankDepositAll or CustomActionType.BankDepositAllExcept or 
                    CustomActionType.BankDepositEquipment or CustomActionType.BankWithdrawItem or CustomActionType.BankWithdrawAll or 
                    CustomActionType.BankWithdrawAllButOne or CustomActionType.CloseBank or CustomActionType.WalkToBank => "💰",
                    CustomActionType.RunAgilityObstacle => "🏃",
                    CustomActionType.WalkToCoords => "🧭",
                    CustomActionType.ContinueDialog or CustomActionType.SelectDialogOption => "🗨️",
                    CustomActionType.WaitSeconds or CustomActionType.WaitForIdle => "⌛",
                    _ => "⚙️"
                };
            }
        }

        [JsonIgnore]
        public string ActionTypeDisplay
        {
            get
            {
                return ActionType switch
                {
                    CustomActionType.ChopObject => "Chop Object/Tree",
                    CustomActionType.MineObject => "Mine Rock/Ore",
                    CustomActionType.ClickObject => "Interact Object",
                    CustomActionType.AttackNpc => "Attack NPC/Monster",
                    CustomActionType.TalkNpc => "Talk to NPC",
                    CustomActionType.DropItem => "Drop Single Item",
                    CustomActionType.DropAllOfItem => "Drop All Of Item",
                    CustomActionType.DropAllExcept => "Drop All Except",
                    CustomActionType.EatFood => "Eat Food",
                    CustomActionType.DrinkPotion => "Drink Potion",
                    CustomActionType.TogglePrayer => "Toggle Prayer",
                    CustomActionType.ToggleSpecialAttack => "Activate Special Attack",
                    CustomActionType.EquipItem => "Equip Item",
                    CustomActionType.CleanHerb => "Clean Herb",
                    CustomActionType.UseItemOnItem => "Use Item On Item",
                    CustomActionType.FletchItem => "Fletch Item",
                    CustomActionType.CookFood => "Cook Food on Fire/Range",
                    CustomActionType.SmeltOre => "Smelt Ore at Furnace",
                    CustomActionType.AlchItem => "High Alchemy Item",
                    CustomActionType.LootGroundItem => "Loot Ground Item",
                    CustomActionType.LootAllConfigured => "Loot Configured Items",
                    CustomActionType.OpenNearestBank => "Open Nearest Bank",
                    CustomActionType.BankDepositAll => "Bank: Deposit All",
                    CustomActionType.BankDepositAllExcept => "Bank: Deposit All Except",
                    CustomActionType.BankDepositEquipment => "Bank: Deposit Equipment",
                    CustomActionType.BankWithdrawItem => "Bank: Withdraw Item",
                    CustomActionType.BankWithdrawAll => "Bank: Withdraw All",
                    CustomActionType.BankWithdrawAllButOne => "Bank: Withdraw All-But-1",
                    CustomActionType.CloseBank => "Bank: Close Interface",
                    CustomActionType.CastSpellOnItem => "Cast Spell on Item",
                    CustomActionType.CastTeleport => "Cast Teleport Spell",
                    CustomActionType.RunAgilityObstacle => "Traverse Agility Obstacle",
                    CustomActionType.WalkToCoords => "Walk to Coordinates",
                    CustomActionType.WalkToBank => "Walk to Nearest Bank",
                    CustomActionType.ContinueDialog => "Dialog: Continue / Space",
                    CustomActionType.SelectDialogOption => "Dialog: Select Option",
                    CustomActionType.WaitSeconds => "Wait Delay",
                    CustomActionType.WaitForIdle => "Wait for Player Idle",
                    _ => ActionType.ToString()
                };
            }
        }

        [JsonIgnore]
        public string ConditionDisplay
        {
            get
            {
                return Condition switch
                {
                    CustomConditionType.Always => "Always",
                    CustomConditionType.InventoryFull => "If Inv Full",
                    CustomConditionType.InventoryNotFull => "If Inv Not Full",
                    CustomConditionType.InventoryHasItem => $"If Has '{ConditionArg}'",
                    CustomConditionType.InventoryDoesNotHaveItem => $"If No '{ConditionArg}'",
                    CustomConditionType.InventoryCountLessThan => $"If Inv '{ConditionArg.Split(':')[0]}' < {(ConditionArg.Contains(':') ? ConditionArg.Split(':')[1] : "1")}",
                    CustomConditionType.InventoryCountGreaterThan => $"If Inv '{ConditionArg.Split(':')[0]}' > {(ConditionArg.Contains(':') ? ConditionArg.Split(':')[1] : "1")}",
                    CustomConditionType.PlayerIsIdle => "If Player Idle",
                    CustomConditionType.PlayerIsNotIdle => "If Animating / Moving",
                    CustomConditionType.PlayerInCombat => "If In Combat",
                    CustomConditionType.PlayerNotInCombat => "If Out of Combat",
                    CustomConditionType.TargetInCombat => "If Target In Combat",
                    CustomConditionType.HpBelowPercent => $"If HP < {ConditionArg}%",
                    CustomConditionType.PrayerBelow => $"If Pray < {ConditionArg}",
                    CustomConditionType.SpecialAttackAbove => $"If Spec >= {ConditionArg}%",
                    CustomConditionType.RunEnergyBelow => $"If Energy < {ConditionArg}%",
                    CustomConditionType.BankIsOpen => "If Bank Open",
                    CustomConditionType.BankIsClosed => "If Bank Closed",
                    CustomConditionType.DialogIsOpen => "If Dialog Open",
                    CustomConditionType.GroundItemNearby => $"If Ground Item '{ConditionArg}'",
                    CustomConditionType.Poisoned => "If Poisoned",
                    _ => "Always"
                };
            }
        }

        [JsonIgnore]
        public string Summary
        {
            get
            {
                string act = ActionType switch
                {
                    CustomActionType.ChopObject => $"Chop '{TargetName}' ({ActionVerb})",
                    CustomActionType.MineObject => $"Mine '{TargetName}'",
                    CustomActionType.ClickObject => $"Interact '{TargetName}' ({ActionVerb})",
                    CustomActionType.AttackNpc => $"Attack '{TargetName}'",
                    CustomActionType.TalkNpc => $"Talk to '{TargetName}'",
                    CustomActionType.DropItem => $"Drop '{TargetName}'",
                    CustomActionType.DropAllOfItem => $"Drop All '{TargetName}'",
                    CustomActionType.DropAllExcept => $"Drop All Except '{TargetName}'",
                    CustomActionType.EatFood => $"Eat '{TargetName}'",
                    CustomActionType.DrinkPotion => $"Drink Potion '{TargetName}'",
                    CustomActionType.TogglePrayer => $"Toggle Prayer '{TargetName}'",
                    CustomActionType.ToggleSpecialAttack => "Activate Special Attack",
                    CustomActionType.EquipItem => $"Equip '{TargetName}'",
                    CustomActionType.CleanHerb => $"Clean Herb '{TargetName}'",
                    CustomActionType.UseItemOnItem => $"Use '{TargetName}' on '{Param1}'",
                    CustomActionType.FletchItem => $"Fletch '{TargetName}'",
                    CustomActionType.CookFood => $"Cook '{TargetName}' on '{Param1}'",
                    CustomActionType.SmeltOre => $"Smelt '{TargetName}' at '{Param1}'",
                    CustomActionType.AlchItem => $"High Alch '{TargetName}'",
                    CustomActionType.LootGroundItem => $"Loot '{TargetName}'",
                    CustomActionType.LootAllConfigured => $"Loot All ({TargetName})",
                    CustomActionType.OpenNearestBank => "Open Nearest Bank",
                    CustomActionType.BankDepositAll => "Bank: Deposit All Items",
                    CustomActionType.BankDepositAllExcept => $"Bank: Deposit All Except '{TargetName}'",
                    CustomActionType.BankDepositEquipment => "Bank: Deposit Equipment",
                    CustomActionType.BankWithdrawItem => $"Bank: Withdraw {Param1}x '{TargetName}'",
                    CustomActionType.BankWithdrawAll => $"Bank: Withdraw ALL '{TargetName}'",
                    CustomActionType.BankWithdrawAllButOne => $"Bank: Withdraw All-But-1 '{TargetName}'",
                    CustomActionType.CloseBank => "Bank: Close Interface",
                    CustomActionType.CastSpellOnItem => $"Cast '{TargetName}' on '{Param1}'",
                    CustomActionType.CastTeleport => $"Cast Teleport '{TargetName}'",
                    CustomActionType.RunAgilityObstacle => $"Agility: Traverse '{TargetName}'",
                    CustomActionType.WalkToCoords => $"Walk to ({Param1}, {Param2})",
                    CustomActionType.WalkToBank => "Walk to Nearest Bank",
                    CustomActionType.ContinueDialog => "Dialog: Press Space / Continue",
                    CustomActionType.SelectDialogOption => $"Dialog: Select Option #{Param1}",
                    CustomActionType.WaitSeconds => $"Wait {WaitAfterMs}ms",
                    CustomActionType.WaitForIdle => "Wait until Player is Idle",
                    _ => TargetName
                };

                return $"[{ConditionDisplay}] -> {act}";
            }
        }
    }

    public class CustomScriptDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "My Custom Script";
        public string Description { get; set; } = "Created with Visual Script Builder";
        public string Author { get; set; } = "User";
        public string Version { get; set; } = "1.0.0";
        public string Category { get; set; } = "Custom";
        public int MinLoopDelayMs { get; set; } = 600;
        public int MaxLoopDelayMs { get; set; } = 1200;
        public string BankingOption { get; set; } = "Deposit All";
        public List<CustomScriptConfigField> ConfigFields { get; set; } = new();
        public List<CustomActionStep> Steps { get; set; } = new();

        public void ApplyConfigValues()
        {
            if (ConfigFields == null || Steps == null) return;
            foreach (var field in ConfigFields)
            {
                if (string.IsNullOrWhiteSpace(field.Value)) continue;
                string placeholder = "{" + field.Key + "}";
                foreach (var step in Steps)
                {
                    if (!string.IsNullOrEmpty(step.TargetName) && step.TargetName.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
                        step.TargetName = step.TargetName.Replace(placeholder, field.Value, StringComparison.OrdinalIgnoreCase);
                    if (!string.IsNullOrEmpty(step.Param1) && step.Param1.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
                        step.Param1 = step.Param1.Replace(placeholder, field.Value, StringComparison.OrdinalIgnoreCase);
                    if (!string.IsNullOrEmpty(step.Param2) && step.Param2.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
                        step.Param2 = step.Param2.Replace(placeholder, field.Value, StringComparison.OrdinalIgnoreCase);
                    if (!string.IsNullOrEmpty(step.ConditionArg) && step.ConditionArg.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
                        step.ConditionArg = step.ConditionArg.Replace(placeholder, field.Value, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }

    public class CustomScriptBot : Bot
    {
        public CustomScriptDefinition Definition { get; set; }
        private int _stepIndex = 0;

        public CustomScriptBot(CustomScriptDefinition definition)
        {
            Definition = definition;
            Name = definition.Name;
            Description = definition.Description;
            Author = definition.Author;
            Version = definition.Version;
            Category = definition.Category;
            StatusText = "Ready";
        }

        public override Task<bool> OnStartAsync()
        {
            _stepIndex = 0;
            Definition.ApplyConfigValues();
            StatusText = $"Starting '{Definition.Name}' with {Definition.Steps.Count} steps...";
            Log($"[CustomScript] Loaded '{Definition.Name}' ({Definition.Steps.Count} action steps)");
            return Task.FromResult(true);
        }

        public override async Task<int> OnLoopAsync(CancellationToken ct)
        {
            if (Definition.Steps == null || Definition.Steps.Count == 0)
            {
                StatusText = "No steps defined in script";
                return 1000;
            }

            bool executedAny = false;

            // Iterate through steps: evaluate enabled steps and execute the first matching step
            for (int i = 0; i < Definition.Steps.Count; i++)
            {
                if (ct.IsCancellationRequested) break;

                var step = Definition.Steps[i];
                if (!step.Enabled) continue;

                if (EvaluateCondition(step))
                {
                    _stepIndex = i;
                    StatusText = $"Step {i + 1}/{Definition.Steps.Count}: {step.Title}";

                    bool executed = await ExecuteStepAsync(step, ct);
                    if (executed)
                    {
                        executedAny = true;
                        Log($"[Action] Executed Step {i + 1}: {step.Summary}");

                        if (step.WaitAfterMs > 0)
                        {
                            await Task.Delay(step.WaitAfterMs, ct);
                        }

                        int delay = Random.Shared.Next(
                            Math.Max(300, Definition.MinLoopDelayMs),
                            Math.Max(Definition.MinLoopDelayMs + 100, Definition.MaxLoopDelayMs + 1));
                        return delay;
                    }
                }
            }

            if (!executedAny)
            {
                StatusText = "Searching for targets / waiting...";
            }

            return Random.Shared.Next(
                Math.Max(300, Definition.MinLoopDelayMs),
                Math.Max(Definition.MinLoopDelayMs + 100, Definition.MaxLoopDelayMs + 1));
        }

        public bool EvaluateCondition(CustomActionStep step)
        {
            switch (step.Condition)
            {
                case CustomConditionType.Always:
                    return true;

                case CustomConditionType.InventoryFull:
                    return BotApi.Inventory.IsFull;

                case CustomConditionType.InventoryNotFull:
                    return !BotApi.Inventory.IsFull;

                case CustomConditionType.InventoryHasItem:
                    return !string.IsNullOrWhiteSpace(step.ConditionArg) 
                        ? BotApi.Inventory.Contains(step.ConditionArg)
                        : BotApi.Inventory.TotalItems > 0;

                case CustomConditionType.InventoryDoesNotHaveItem:
                    return !string.IsNullOrWhiteSpace(step.ConditionArg) 
                        ? !BotApi.Inventory.Contains(step.ConditionArg)
                        : BotApi.Inventory.TotalItems == 0;

                case CustomConditionType.InventoryCountLessThan:
                {
                    string itemName = step.ConditionArg;
                    int count = 1;
                    if (step.ConditionArg.Contains(':') && int.TryParse(step.ConditionArg.Split(':')[1], out int c))
                    {
                        itemName = step.ConditionArg.Split(':')[0];
                        count = c;
                    }
                    return BotApi.Inventory.GetCount(itemName) < count;
                }

                case CustomConditionType.InventoryCountGreaterThan:
                {
                    string itemName = step.ConditionArg;
                    int count = 1;
                    if (step.ConditionArg.Contains(':') && int.TryParse(step.ConditionArg.Split(':')[1], out int c))
                    {
                        itemName = step.ConditionArg.Split(':')[0];
                        count = c;
                    }
                    return BotApi.Inventory.GetCount(itemName) > count;
                }

                case CustomConditionType.PlayerIsIdle:
                    var p = BotApi.Players.Local;
                    return !p.IsInteracting && (p.Animation <= 0 || p.Animation == 808 || p.Animation == 813);

                case CustomConditionType.PlayerIsNotIdle:
                    var pl = BotApi.Players.Local;
                    return pl.IsInteracting || pl.Animation > 0 || pl.IsMoving;

                case CustomConditionType.PlayerInCombat:
                    return BotApi.Players.Local.IsInteracting || BotApi.Players.Local.Animation == 422 || BotApi.Players.Local.Animation == 423 || BotApi.Players.Local.Animation == 401;

                case CustomConditionType.PlayerNotInCombat:
                    return !BotApi.Players.Local.IsInteracting;

                case CustomConditionType.TargetInCombat:
                    var targetNpc = BotApi.Npcs.Nearest(step.TargetName);
                    return targetNpc != null && targetNpc.IsInteracting;

                case CustomConditionType.HpBelowPercent:
                    if (int.TryParse(step.ConditionArg, out int hpThresh))
                    {
                        var player = BotApi.Players.Local;
                        int maxHp = player.MaxHp > 0 ? player.MaxHp : 99;
                        int curHp = player.CurrentHp > 0 ? player.CurrentHp : 99;
                        int pct = (curHp * 100) / maxHp;
                        return pct <= hpThresh;
                    }
                    return false;

                case CustomConditionType.PrayerBelow:
                    if (int.TryParse(step.ConditionArg, out int prayThresh))
                    {
                        return BotApi.Players.Local.CurrentPrayer <= prayThresh;
                    }
                    return false;

                case CustomConditionType.SpecialAttackAbove:
                    if (int.TryParse(step.ConditionArg, out int specThresh))
                    {
                        return BotApi.Combat.SpecialAttackEnergy >= specThresh;
                    }
                    return false;

                case CustomConditionType.RunEnergyBelow:
                    if (int.TryParse(step.ConditionArg, out int energyThresh))
                    {
                        return BotApi.Players.Local.RunEnergy <= energyThresh;
                    }
                    return false;

                case CustomConditionType.BankIsOpen:
                    return BotApi.Bank.IsOpen;

                case CustomConditionType.BankIsClosed:
                    return !BotApi.Bank.IsOpen;

                case CustomConditionType.DialogIsOpen:
                    return BotApi.Dialog.IsOpen;

                case CustomConditionType.GroundItemNearby:
                    return !string.IsNullOrWhiteSpace(step.ConditionArg)
                        ? BotApi.GroundItems.Nearest(step.ConditionArg) != null
                        : BotApi.GroundItems.Query().ToList().Count > 0;

                case CustomConditionType.Poisoned:
                    return BotApi.Players.Local.IsPoisoned;

                default:
                    return true;
            }
        }

        private async Task<bool> ExecuteStepAsync(CustomActionStep step, CancellationToken ct)
        {
            switch (step.ActionType)
            {
                case CustomActionType.ChopObject:
                {
                    var tree = !string.IsNullOrWhiteSpace(step.TargetName)
                        ? (BotApi.Objects.NearestTree(step.TargetName) ?? BotApi.Objects.Nearest(step.TargetName))
                        : BotApi.Objects.NearestTree();

                    if (tree == null)
                    {
                        return false;
                    }

                    Log($"[Chopper] Interacting with {tree.Name} at ({tree.WorldX}, {tree.WorldY})...");
                    return await tree.InteractAsync(step.ActionVerb, ct);
                }

                case CustomActionType.MineObject:
                case CustomActionType.ClickObject:
                {
                    var obj = BotApi.Objects.Nearest(step.TargetName);
                    if (obj == null)
                    {
                        return false;
                    }

                    Log($"[Object] Interacting with {obj.Name} ({step.ActionVerb})...");
                    return await obj.InteractAsync(step.ActionVerb, ct);
                }

                case CustomActionType.AttackNpc:
                {
                    var targetNames = step.TargetName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    NpcEntity? npc = null;

                    foreach (var name in targetNames)
                    {
                        string cleanName = name;
                        int parenIdx = cleanName.IndexOf('(');
                        if (parenIdx > 0) cleanName = cleanName.Substring(0, parenIdx).Trim();

                        npc = BotApi.Npcs.Query()
                            .Filter(n => n.Name.Equals(cleanName, StringComparison.OrdinalIgnoreCase) ||
                                         n.Name.StartsWith(cleanName, StringComparison.OrdinalIgnoreCase) ||
                                         n.Name.Contains(cleanName, StringComparison.OrdinalIgnoreCase))
                            .Nearest();

                        if (npc != null) break;
                    }

                    if (npc == null && targetNames.Length == 0)
                    {
                        npc = BotApi.Npcs.Query()
                            .Filter(n => n.Role != "Banker" && n.Role != "Shopkeeper" && n.Role != "Grand Exchange")
                            .Nearest();
                    }

                    if (npc == null)
                    {
                        return false;
                    }

                    var localPlayer = BotApi.Players.Local;
                    if (localPlayer.IsInteracting && localPlayer.Animation > 0 && localPlayer.Animation != 808 && localPlayer.Animation != 813)
                    {
                        return false;
                    }

                    string combatLvlStr = npc.CombatLevel.ToString();
                    string wxStr = npc.WorldX.ToString();
                    string wyStr = npc.WorldY.ToString();
                    Log($"[Combat] Attacking {npc.Name} (Lvl {combatLvlStr}) at ({wxStr}, {wyStr})...");
                    return await npc.InteractAsync(step.ActionVerb, ct);
                }

                case CustomActionType.TalkNpc:
                {
                    var npc = BotApi.Npcs.Nearest(step.TargetName) ?? BotApi.Npcs.Query().Named(step.TargetName).Nearest();
                    if (npc == null)
                    {
                        return false;
                    }

                    Log($"[NPC] Talking to {npc.Name} ({step.ActionVerb})...");
                    return await npc.InteractAsync(step.ActionVerb, ct);
                }

                case CustomActionType.DropItem:
                {
                    var item = BotApi.Inventory.Query().Named(step.TargetName).First();
                    if (item == null)
                    {
                        return false;
                    }

                    Log($"[Inventory] Dropping {item.Name} at slot {item.Slot}...");
                    return await item.DropAsync(ct);
                }

                case CustomActionType.DropAllOfItem:
                {
                    var items = BotApi.Inventory.Query().Named(step.TargetName).ToList();
                    if (items.Count == 0) return false;

                    Log($"[Inventory] Dropping {items.Count}x '{step.TargetName}'...");
                    foreach (var item in items)
                    {
                        if (ct.IsCancellationRequested) break;
                        await item.DropAsync(ct);
                        await Task.Delay(Random.Shared.Next(120, 240), ct);
                    }
                    return true;
                }

                case CustomActionType.DropAllExcept:
                {
                    var keepNames = step.TargetName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    Log($"[Inventory] Dropping all items except: {string.Join(", ", keepNames)}");
                    var items = BotApi.Inventory.Query().ToList();
                    foreach (var item in items)
                    {
                        if (ct.IsCancellationRequested) break;
                        if (!keepNames.Any(k => item.Name.Contains(k, StringComparison.OrdinalIgnoreCase)))
                        {
                            await item.DropAsync(ct);
                            await Task.Delay(Random.Shared.Next(150, 300), ct);
                        }
                    }
                    return true;
                }

                case CustomActionType.EatFood:
                {
                    var item = !string.IsNullOrWhiteSpace(step.TargetName)
                        ? BotApi.Inventory.Query().Named(step.TargetName).First()
                        : BotApi.Inventory.Query().Filter(i => i.Name.Contains("fish", StringComparison.OrdinalIgnoreCase) || 
                                                              i.Name.Contains("meat", StringComparison.OrdinalIgnoreCase) ||
                                                              i.Name.Contains("trout", StringComparison.OrdinalIgnoreCase) ||
                                                              i.Name.Contains("salmon", StringComparison.OrdinalIgnoreCase) ||
                                                              i.Name.Contains("lobster", StringComparison.OrdinalIgnoreCase) ||
                                                              i.Name.Contains("shark", StringComparison.OrdinalIgnoreCase) ||
                                                              i.Name.Contains("karambwan", StringComparison.OrdinalIgnoreCase) ||
                                                              i.Name.Contains("cake", StringComparison.OrdinalIgnoreCase) ||
                                                              i.Name.Contains("bread", StringComparison.OrdinalIgnoreCase) ||
                                                              i.Name.Contains("pie", StringComparison.OrdinalIgnoreCase) ||
                                                              i.Name.Contains("monkfish", StringComparison.OrdinalIgnoreCase) ||
                                                              i.Name.Contains("anglerfish", StringComparison.OrdinalIgnoreCase) ||
                                                              i.Name.Contains("manta", StringComparison.OrdinalIgnoreCase)).First();
                    if (item == null)
                    {
                        return false;
                    }

                    string itemName = item.Name;
                    Log($"[Food] Eating '{itemName}'...");
                    return await item.InteractAsync("Eat", ct);
                }

                case CustomActionType.DrinkPotion:
                {
                    string potBase = step.TargetName;
                    var item = BotApi.Inventory.Query().Filter(i => 
                        i.Name.StartsWith(potBase, StringComparison.OrdinalIgnoreCase) ||
                        i.Name.Contains(potBase, StringComparison.OrdinalIgnoreCase)).First();

                    if (item == null)
                    {
                        return false;
                    }

                    Log($"[Potion] Drinking '{item.Name}'...");
                    return await item.InteractAsync("Drink", ct);
                }

                case CustomActionType.TogglePrayer:
                {
                    Log($"[Prayer] Toggling prayer '{step.TargetName}'...");
                    return await BotApi.Prayers.ToggleAsync(step.TargetName, ct);
                }

                case CustomActionType.ToggleSpecialAttack:
                {
                    Log("[Combat] Activating Special Attack...");
                    return await BotApi.Combat.ToggleSpecialAttackAsync(ct);
                }

                case CustomActionType.EquipItem:
                {
                    var item = BotApi.Inventory.Query().Named(step.TargetName).First();
                    if (item == null) return false;

                    Log($"[Equipment] Equipping '{item.Name}'...");
                    return await item.InteractAsync("Wield", ct) || await item.InteractAsync("Wear", ct) || await item.InteractAsync("Equip", ct);
                }

                case CustomActionType.CleanHerb:
                {
                    var item = !string.IsNullOrWhiteSpace(step.TargetName)
                        ? BotApi.Inventory.Query().Named(step.TargetName).First()
                        : BotApi.Inventory.Query().Filter(i => i.Name.StartsWith("Grimy", StringComparison.OrdinalIgnoreCase)).First();

                    if (item == null) return false;

                    Log($"[Herblore] Cleaning '{item.Name}'...");
                    return await item.InteractAsync("Clean", ct);
                }

                case CustomActionType.UseItemOnItem:
                {
                    var item1 = BotApi.Inventory.Query().Named(step.TargetName).First();
                    var item2 = BotApi.Inventory.Query().Named(step.Param1).First();
                    if (item1 == null || item2 == null)
                    {
                        return false;
                    }

                    string name1 = item1.Name;
                    string name2 = item2.Name;
                    Log($"[Inventory] Using '{name1}' on '{name2}'...");
                    await item1.InteractAsync("Use", ct);
                    await Task.Delay(Random.Shared.Next(250, 450), ct);
                    return await item2.InteractAsync("Use", ct);
                }

                case CustomActionType.FletchItem:
                {
                    var knife = BotApi.Inventory.Query().Named("Knife").First();
                    var logItem = BotApi.Inventory.Query().Named(step.TargetName).First();
                    if (knife != null && logItem != null)
                    {
                        Log($"[Fletching] Using Knife on '{logItem.Name}'...");
                        await knife.InteractAsync("Use", ct);
                        await Task.Delay(300, ct);
                        await logItem.InteractAsync("Use", ct);
                        await Task.Delay(1000, ct);
                        await BotApi.Dialog.PressSpaceAsync(ct);
                        return true;
                    }
                    return false;
                }

                case CustomActionType.CookFood:
                {
                    var rawItem = BotApi.Inventory.Query().Named(step.TargetName).First();
                    var range = BotApi.Objects.Nearest("Range") ?? BotApi.Objects.Nearest("Cooking range") ?? BotApi.Objects.Nearest("Fire");
                    if (rawItem != null && range != null)
                    {
                        Log($"[Cooking] Using '{rawItem.Name}' on '{range.Name}'...");
                        await rawItem.InteractAsync("Use", ct);
                        await Task.Delay(300, ct);
                        await range.InteractAsync("Use", ct);
                        await Task.Delay(1000, ct);
                        await BotApi.Dialog.PressSpaceAsync(ct);
                        return true;
                    }
                    return false;
                }

                case CustomActionType.SmeltOre:
                {
                    var furnace = BotApi.Objects.Nearest("Furnace");
                    var ore = BotApi.Inventory.Query().Named(step.TargetName).First();
                    if (furnace != null && ore != null)
                    {
                        Log($"[Smithing] Using '{ore.Name}' on Furnace...");
                        await ore.InteractAsync("Use", ct);
                        await Task.Delay(300, ct);
                        await furnace.InteractAsync("Use", ct);
                        await Task.Delay(1000, ct);
                        await BotApi.Dialog.PressSpaceAsync(ct);
                        return true;
                    }
                    return false;
                }

                case CustomActionType.AlchItem:
                {
                    var item = BotApi.Inventory.Query().Named(step.TargetName).First();
                    if (item == null) return false;

                    Log($"[Magic] Casting High Alchemy on '{item.Name}'...");
                    return await BotApi.Magic.CastHighAlchemyAsync(item.Name, ct);
                }

                case CustomActionType.LootGroundItem:
                {
                    var groundItem = !string.IsNullOrWhiteSpace(step.TargetName)
                        ? (BotApi.GroundItems.Nearest(step.TargetName) ?? BotApi.GroundItems.Query().Named(step.TargetName).Nearest())
                        : BotApi.GroundItems.Nearest();

                    if (groundItem == null)
                    {
                        return false;
                    }

                    Log($"[Loot] Picking up {groundItem.Name} x{groundItem.Quantity} (dist: {groundItem.Distance:F1}m)...");
                    return await groundItem.TakeAsync(ct);
                }

                case CustomActionType.LootAllConfigured:
                {
                    var targetItems = step.TargetName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var groundItems = BotApi.GroundItems.Query().ToList();

                    foreach (var name in targetItems)
                    {
                        var match = groundItems.FirstOrDefault(g => g.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
                        if (match != null)
                        {
                            Log($"[Loot] Picking up '{match.Name}' x{match.Quantity}...");
                            return await match.TakeAsync(ct);
                        }
                    }
                    return false;
                }

                case CustomActionType.OpenNearestBank:
                {
                    if (BotApi.Bank.IsOpen)
                    {
                        Log("[Bank] Bank is already open.");
                        return true;
                    }

                    var bankObj = BotApi.Objects.NearestBank() 
                                  ?? BotApi.Objects.Nearest("Bank booth") 
                                  ?? BotApi.Objects.Nearest("Bank chest")
                                  ?? BotApi.Objects.Nearest("Grand Exchange booth");
                    if (bankObj != null)
                    {
                        Log($"[Bank] Interacting with {bankObj.Name}...");
                        await bankObj.InteractAsync("Bank", ct);
                        await Task.Delay(1200, ct);
                        return true;
                    }

                    var banker = BotApi.Npcs.Nearest("Banker") ?? BotApi.Npcs.Nearest("Bank");
                    if (banker != null)
                    {
                        Log($"[Bank] Interacting with banker {banker.Name}...");
                        await banker.InteractAsync("Bank", ct);
                        await Task.Delay(1200, ct);
                        return true;
                    }

                    Log("[Bank] No bank booth, chest, or banker found nearby.");
                    return false;
                }

                case CustomActionType.BankDepositAll:
                {
                    if (!BotApi.Bank.IsOpen) return false;
                    Log("[Bank] Depositing all inventory items...");
                    return await BotApi.Bank.DepositAllAsync(ct);
                }

                case CustomActionType.BankDepositAllExcept:
                {
                    if (!BotApi.Bank.IsOpen) return false;
                    var keepNames = step.TargetName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    Log($"[Bank] Depositing all except: {string.Join(", ", keepNames)}...");
                    return await BotApi.Bank.DepositAllExceptAsync(keepNames, ct);
                }

                case CustomActionType.BankDepositEquipment:
                {
                    if (!BotApi.Bank.IsOpen) return false;
                    Log("[Bank] Depositing worn equipment...");
                    return await BotApi.Bank.DepositEquipmentAsync(ct);
                }

                case CustomActionType.BankWithdrawItem:
                {
                    if (!BotApi.Bank.IsOpen) return false;
                    int qty = 1;
                    if (int.TryParse(step.Param1, out int q)) qty = q;
                    Log($"[Bank] Withdrawing {qty}x '{step.TargetName}'...");
                    return await BotApi.Bank.WithdrawAsync(step.TargetName, qty, ct);
                }

                case CustomActionType.BankWithdrawAll:
                {
                    if (!BotApi.Bank.IsOpen) return false;
                    Log($"[Bank] Withdrawing ALL '{step.TargetName}'...");
                    return await BotApi.Bank.WithdrawAllAsync(step.TargetName, ct);
                }

                case CustomActionType.BankWithdrawAllButOne:
                {
                    if (!BotApi.Bank.IsOpen) return false;
                    Log($"[Bank] Withdrawing All-But-1 '{step.TargetName}'...");
                    return await BotApi.Bank.WithdrawAllButOneAsync(step.TargetName, ct);
                }

                case CustomActionType.CloseBank:
                {
                    if (!BotApi.Bank.IsOpen) return true;
                    Log("[Bank] Closing bank interface...");
                    return await BotApi.Bank.CloseAsync(ct);
                }

                case CustomActionType.CastSpellOnItem:
                {
                    Log($"[Magic] Casting {step.TargetName} on {step.Param1}...");
                    return await BotApi.Magic.CastOnInventoryItemAsync(step.TargetName, step.Param1, ct);
                }

                case CustomActionType.CastTeleport:
                {
                    Log($"[Magic] Casting Teleport {step.TargetName}...");
                    return await BotApi.Magic.CastTeleportAsync(step.TargetName, ct);
                }

                case CustomActionType.RunAgilityObstacle:
                {
                    var obstacle = BotApi.Agility.NearestObstacle(step.TargetName);
                    if (obstacle == null)
                    {
                        var obj = BotApi.Objects.Nearest(step.TargetName);
                        if (obj != null)
                        {
                            Log($"[Agility] Interacting with {obj.Name} ({step.ActionVerb})...");
                            return await obj.InteractAsync(step.ActionVerb, ct);
                        }
                        return false;
                    }

                    Log($"[Agility] Traversing {obstacle.Name} at ({obstacle.WorldX}, {obstacle.WorldY})...");
                    return await obstacle.TraverseAsync(ct);
                }

                case CustomActionType.WalkToCoords:
                {
                    if (int.TryParse(step.Param1, out int x) && int.TryParse(step.Param2, out int y))
                    {
                        Log($"[Movement] Walking to ({x}, {y})...");
                        return await BotApi.Movement.WalkToAsync(x, y, ct);
                    }
                    return false;
                }

                case CustomActionType.WalkToBank:
                {
                    var bankObj = BotApi.Objects.NearestBank() ?? BotApi.Objects.Nearest("Bank booth") ?? BotApi.Objects.Nearest("Bank chest");
                    if (bankObj != null)
                    {
                        Log($"[Movement] Walking to nearest Bank ({bankObj.WorldX}, {bankObj.WorldY})...");
                        return await BotApi.Movement.WalkToAsync(bankObj.WorldX, bankObj.WorldY, ct);
                    }
                    return false;
                }

                case CustomActionType.ContinueDialog:
                {
                    Log("[Dialog] Pressing Continue / Space...");
                    return await BotApi.Dialog.PressSpaceAsync(ct);
                }

                case CustomActionType.SelectDialogOption:
                {
                    if (int.TryParse(step.Param1, out int opt))
                    {
                        Log($"[Dialog] Selecting option #{opt}...");
                        return await BotApi.Dialog.SelectOptionAsync(opt, ct);
                    }
                    return false;
                }

                case CustomActionType.WaitSeconds:
                {
                    int ms = step.WaitAfterMs > 0 ? step.WaitAfterMs : 1000;
                    Log($"[Wait] Waiting {ms}ms...");
                    await Task.Delay(ms, ct);
                    return true;
                }

                case CustomActionType.WaitForIdle:
                {
                    Log("[Wait] Waiting for player to become idle...");
                    var start = DateTime.UtcNow;
                    while ((DateTime.UtcNow - start).TotalSeconds < 15 && !ct.IsCancellationRequested)
                    {
                        var p = BotApi.Players.Local;
                        if (!p.IsInteracting && (p.Animation <= 0 || p.Animation == 808 || p.Animation == 813))
                        {
                            return true;
                        }
                        await Task.Delay(200, ct);
                    }
                    return true;
                }

                default:
                    return false;
            }
        }
    }

    public static class CustomScriptStorage
    {
        private static readonly string ScriptsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "custom_scripts");

        public static void EnsureDirectoryExists()
        {
            if (!Directory.Exists(ScriptsDirectory))
            {
                Directory.CreateDirectory(ScriptsDirectory);
            }
        }

        public static bool Save(CustomScriptDefinition def)
        {
            try
            {
                SaveScript(def);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string SaveScript(CustomScriptDefinition def)
        {
            EnsureDirectoryExists();
            string safeName = string.Join("_", def.Name.Split(Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(safeName)) safeName = "CustomScript";
            string filePath = Path.Combine(ScriptsDirectory, $"{safeName}.json");

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            };

            string json = JsonSerializer.Serialize(def, options);
            File.WriteAllText(filePath, json, Encoding.UTF8);
            return filePath;
        }

        public static List<CustomScriptDefinition> LoadAll() => LoadAllScripts();

        public static List<CustomScriptDefinition> LoadAllScripts()
        {
            EnsureDirectoryExists();
            var scripts = new List<CustomScriptDefinition>();

            foreach (var file in Directory.GetFiles(ScriptsDirectory, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file, Encoding.UTF8);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new JsonStringEnumConverter() }
                    };
                    var def = JsonSerializer.Deserialize<CustomScriptDefinition>(json, options);
                    if (def != null)
                    {
                        scripts.Add(def);
                    }
                }
                catch { }
            }

            return scripts;
        }

        public static bool Delete(string scriptIdOrName)
        {
            try
            {
                EnsureDirectoryExists();
                string safeName = string.Join("_", scriptIdOrName.Split(Path.GetInvalidFileNameChars()));
                string filePath = Path.Combine(ScriptsDirectory, $"{safeName}.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }

                foreach (var file in Directory.GetFiles(ScriptsDirectory, "*.json"))
                {
                    try
                    {
                        string json = File.ReadAllText(file, Encoding.UTF8);
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            Converters = { new JsonStringEnumConverter() }
                        };
                        var def = JsonSerializer.Deserialize<CustomScriptDefinition>(json, options);
                        if (def != null && (def.Id == scriptIdOrName || def.Name == scriptIdOrName))
                        {
                            File.Delete(file);
                            return true;
                        }
                    }
                    catch { }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static void DeleteScript(string scriptName) => Delete(scriptName);

        public static string ExportToCSharp(CustomScriptDefinition def)
        {
            return CustomScriptCodeGenerator.GenerateCSharpCode(def);
        }

        public static void AutoPopulateConfigFields(CustomScriptDefinition def)
        {
            if (def == null) return;
            if (def.ConfigFields == null) def.ConfigFields = new List<CustomScriptConfigField>();
        }

        public static void RegisterCustomScriptsWithRunner(ScriptRunner runner)
        {
            var scripts = LoadAllScripts();
            foreach (var script in scripts)
            {
                runner.RegisterBot(new CustomScriptBot(script));
            }
        }
    }

    public static class CustomScriptCodeGenerator
    {
        public static string GenerateCSharpCode(CustomScriptDefinition def)
        {
            var sb = new StringBuilder();
            string className = CleanIdentifier(def.Name) + "Bot";

            sb.AppendLine("// ===========================================================================");
            sb.AppendLine($"// AUTO-GENERATED BOT SCRIPT: {def.Name}");
            sb.AppendLine($"// Category: {def.Category} | Version: {def.Version} | Author: {def.Author}");
            sb.AppendLine($"// Description: {def.Description}");
            sb.AppendLine($"// Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss} via osrsmr Visual Script Studio");
            sb.AppendLine("// ===========================================================================");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using OsrsMr.Api;");
            sb.AppendLine("using OsrsMr.Api.Framework;");
            sb.AppendLine("using OsrsMr.Api.Input;");
            sb.AppendLine();
            sb.AppendLine("namespace OsrsMr.Scripts.Generated");
            sb.AppendLine("{");
            sb.AppendLine($"    public class {className} : Bot");
            sb.AppendLine("    {");
            sb.AppendLine($"        public {className}()");
            sb.AppendLine("        {");
            sb.AppendLine($"            Name = \"{EscapeString(def.Name)}\";");
            sb.AppendLine($"            Description = \"{EscapeString(def.Description)}\";");
            sb.AppendLine($"            Category = \"{EscapeString(def.Category)}\";");
            sb.AppendLine($"            Author = \"{EscapeString(def.Author)}\";");
            sb.AppendLine($"            Version = \"{EscapeString(def.Version)}\";");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public override async Task<int> OnLoopAsync(CancellationToken ct)");
            sb.AppendLine("        {");

            for (int i = 0; i < def.Steps.Count; i++)
            {
                var step = def.Steps[i];
                if (!step.Enabled) continue;

                sb.AppendLine($"            // Step {i + 1}: {step.Title}");
                string condCode = GenerateConditionCode(step);
                sb.AppendLine($"            if ({condCode})");
                sb.AppendLine("            {");
                sb.AppendLine($"                StatusText = \"{EscapeString(step.Title)}\";");
                sb.AppendLine($"                bool executed = await ExecuteStep_{i + 1}(ct);");
                sb.AppendLine("                if (executed)");
                sb.AppendLine("                {");
                if (step.WaitAfterMs > 0)
                {
                    sb.AppendLine($"                    await Task.Delay({step.WaitAfterMs}, ct);");
                }
                sb.AppendLine($"                    return Random.Shared.Next({def.MinLoopDelayMs}, {def.MaxLoopDelayMs});");
                sb.AppendLine("                }");
                sb.AppendLine("            }");
                sb.AppendLine();
            }

            sb.AppendLine("            StatusText = \"Idle / Looking for targets...\";");
            sb.AppendLine($"            return Random.Shared.Next({def.MinLoopDelayMs}, {def.MaxLoopDelayMs});");
            sb.AppendLine("        }");
            sb.AppendLine();

            for (int i = 0; i < def.Steps.Count; i++)
            {
                var step = def.Steps[i];
                sb.AppendLine($"        private async Task<bool> ExecuteStep_{i + 1}(CancellationToken ct)");
                sb.AppendLine("        {");
                sb.AppendLine(GenerateActionCode(step));
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateConditionCode(CustomActionStep step)
        {
            return step.Condition switch
            {
                CustomConditionType.Always => "true",
                CustomConditionType.InventoryFull => "BotApi.Inventory.IsFull",
                CustomConditionType.InventoryNotFull => "!BotApi.Inventory.IsFull",
                CustomConditionType.InventoryHasItem => $"BotApi.Inventory.Contains(\"{EscapeString(step.ConditionArg)}\")",
                CustomConditionType.InventoryDoesNotHaveItem => $"!BotApi.Inventory.Contains(\"{EscapeString(step.ConditionArg)}\")",
                CustomConditionType.PlayerIsIdle => "!BotApi.Players.Local.IsInteracting && BotApi.Players.Local.Animation <= 0",
                CustomConditionType.PlayerIsNotIdle => "BotApi.Players.Local.IsInteracting || BotApi.Players.Local.Animation > 0 || BotApi.Players.Local.IsMoving",
                CustomConditionType.PlayerInCombat => "BotApi.Players.Local.IsInteracting",
                CustomConditionType.PlayerNotInCombat => "!BotApi.Players.Local.IsInteracting",
                CustomConditionType.HpBelowPercent => $"((BotApi.Players.Local.CurrentHp * 100) / Math.Max(1, BotApi.Players.Local.MaxHp)) <= {step.ConditionArg}",
                CustomConditionType.PrayerBelow => $"BotApi.Players.Local.CurrentPrayer <= {step.ConditionArg}",
                CustomConditionType.SpecialAttackAbove => $"BotApi.Combat.SpecialAttackEnergy >= {step.ConditionArg}",
                CustomConditionType.RunEnergyBelow => $"BotApi.Players.Local.RunEnergy <= {step.ConditionArg}",
                CustomConditionType.BankIsOpen => "BotApi.Bank.IsOpen",
                CustomConditionType.BankIsClosed => "!BotApi.Bank.IsOpen",
                CustomConditionType.DialogIsOpen => "BotApi.Dialog.IsOpen",
                CustomConditionType.GroundItemNearby => $"BotApi.GroundItems.Nearest(\"{EscapeString(step.ConditionArg)}\") != null",
                CustomConditionType.Poisoned => "BotApi.Players.Local.IsPoisoned",
                _ => "true"
            };
        }

        private static string GenerateActionCode(CustomActionStep step)
        {
            var sb = new StringBuilder();
            switch (step.ActionType)
            {
                case CustomActionType.ChopObject:
                    sb.AppendLine($"            var tree = BotApi.Objects.NearestTree(\"{EscapeString(step.TargetName)}\") ?? BotApi.Objects.Nearest(\"{EscapeString(step.TargetName)}\");");
                    sb.AppendLine("            if (tree == null) return false;");
                    sb.AppendLine($"            return await tree.InteractAsync(\"{EscapeString(step.ActionVerb)}\", ct);");
                    break;

                case CustomActionType.MineObject:
                case CustomActionType.ClickObject:
                    sb.AppendLine($"            var obj = BotApi.Objects.Nearest(\"{EscapeString(step.TargetName)}\");");
                    sb.AppendLine("            if (obj == null) return false;");
                    sb.AppendLine($"            return await obj.InteractAsync(\"{EscapeString(step.ActionVerb)}\", ct);");
                    break;

                case CustomActionType.AttackNpc:
                    sb.AppendLine($"            var npc = BotApi.Npcs.Nearest(\"{EscapeString(step.TargetName)}\");");
                    sb.AppendLine("            if (npc == null) return false;");
                    sb.AppendLine($"            return await npc.InteractAsync(\"{EscapeString(step.ActionVerb)}\", ct);");
                    break;

                case CustomActionType.TalkNpc:
                    sb.AppendLine($"            var npc = BotApi.Npcs.Nearest(\"{EscapeString(step.TargetName)}\");");
                    sb.AppendLine("            if (npc == null) return false;");
                    sb.AppendLine($"            return await npc.InteractAsync(\"{EscapeString(step.ActionVerb)}\", ct);");
                    break;

                case CustomActionType.EatFood:
                    sb.AppendLine($"            var food = BotApi.Inventory.Query().Named(\"{EscapeString(step.TargetName)}\").First();");
                    sb.AppendLine("            if (food == null) return false;");
                    sb.AppendLine("            return await food.InteractAsync(\"Eat\", ct);");
                    break;

                case CustomActionType.DrinkPotion:
                    sb.AppendLine($"            var potion = BotApi.Inventory.Query().Filter(i => i.Name.Contains(\"{EscapeString(step.TargetName)}\")).First();");
                    sb.AppendLine("            if (potion == null) return false;");
                    sb.AppendLine("            return await potion.InteractAsync(\"Drink\", ct);");
                    break;

                case CustomActionType.TogglePrayer:
                    sb.AppendLine($"            return await BotApi.Prayers.ToggleAsync(\"{EscapeString(step.TargetName)}\", ct);");
                    break;

                case CustomActionType.ToggleSpecialAttack:
                    sb.AppendLine("            return await BotApi.Combat.ToggleSpecialAttackAsync(ct);");
                    break;

                case CustomActionType.DropItem:
                    sb.AppendLine($"            var item = BotApi.Inventory.Query().Named(\"{EscapeString(step.TargetName)}\").First();");
                    sb.AppendLine("            if (item == null) return false;");
                    sb.AppendLine("            return await item.DropAsync(ct);");
                    break;

                case CustomActionType.DropAllOfItem:
                    sb.AppendLine($"            var items = BotApi.Inventory.Query().Named(\"{EscapeString(step.TargetName)}\").ToList();");
                    sb.AppendLine("            if (items.Count == 0) return false;");
                    sb.AppendLine("            foreach (var item in items) { await item.DropAsync(ct); await Task.Delay(150, ct); }");
                    sb.AppendLine("            return true;");
                    break;

                case CustomActionType.LootGroundItem:
                    sb.AppendLine($"            var loot = BotApi.GroundItems.Nearest(\"{EscapeString(step.TargetName)}\");");
                    sb.AppendLine("            if (loot == null) return false;");
                    sb.AppendLine("            return await loot.TakeAsync(ct);");
                    break;

                case CustomActionType.OpenNearestBank:
                    sb.AppendLine("            if (BotApi.Bank.IsOpen) return true;");
                    sb.AppendLine("            var bank = BotApi.Objects.NearestBank() ?? BotApi.Objects.Nearest(\"Bank booth\");");
                    sb.AppendLine("            if (bank == null) return false;");
                    sb.AppendLine("            return await bank.InteractAsync(\"Bank\", ct);");
                    break;

                case CustomActionType.BankDepositAll:
                    sb.AppendLine("            if (!BotApi.Bank.IsOpen) return false;");
                    sb.AppendLine("            return await BotApi.Bank.DepositAllAsync(ct);");
                    break;

                case CustomActionType.BankDepositAllExcept:
                    sb.AppendLine($"            if (!BotApi.Bank.IsOpen) return false;");
                    sb.AppendLine($"            return await BotApi.Bank.DepositAllExceptAsync(new[] {{ \"{EscapeString(step.TargetName)}\" }}, ct);");
                    break;

                case CustomActionType.CloseBank:
                    sb.AppendLine("            if (!BotApi.Bank.IsOpen) return true;");
                    sb.AppendLine("            return await BotApi.Bank.CloseAsync(ct);");
                    break;

                case CustomActionType.AlchItem:
                    sb.AppendLine($"            return await BotApi.Magic.CastHighAlchemyAsync(\"{EscapeString(step.TargetName)}\", ct);");
                    break;

                case CustomActionType.ContinueDialog:
                    sb.AppendLine("            return await BotApi.Dialog.PressSpaceAsync(ct);");
                    break;

                case CustomActionType.SelectDialogOption:
                    sb.AppendLine($"            return await BotApi.Dialog.SelectOptionAsync({(int.TryParse(step.Param1, out int o) ? o : 1)}, ct);");
                    break;

                default:
                    sb.AppendLine($"            Log(\"Executing step: {EscapeString(step.Title)}\");");
                    sb.AppendLine("            return true;");
                    break;
            }
            return sb.ToString();
        }

        private static string CleanIdentifier(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            string res = sb.ToString();
            return string.IsNullOrEmpty(res) ? "Custom" : res;
        }

        private static string EscapeString(string s) => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
    }

    public static class AiScriptAssistant
    {
        public static void AutoPopulateConfigFields(CustomScriptDefinition def) => CustomScriptStorage.AutoPopulateConfigFields(def);

        public static string GetAiPromptTemplate(string scriptGoal = "Auto Fighter") => GenerateAiPrompt(scriptGoal);

        public static CustomScriptDefinition? ParseAiResponse(string jsonText) => ParseAiJson(jsonText);

        public static string GenerateAiPrompt(string scriptGoal = "Auto Fighter")
        {
            return $@"You are an expert Old School RuneScape (OSRS) bot script generator for the 'osrsmr' bot engine.
Generate a valid JSON custom script definition for the following user request:

REQUEST:
{scriptGoal}

The JSON MUST match the following C# schema structure:
{{
  ""name"": ""Script Title"",
  ""category"": ""Combat"" | ""Woodcutting"" | ""Mining"" | ""Fishing"" | ""Magic"" | ""Agility"" | ""Utility"",
  ""description"": ""What this script does..."",
  ""minLoopDelayMs"": 600,
  ""maxLoopDelayMs"": 1200,
  ""steps"": [
    {{
      ""title"": ""Step Name"",
      ""enabled"": true,
      ""actionType"": ""AttackNpc"" | ""ChopObject"" | ""MineObject"" | ""ClickObject"" | ""EatFood"" | ""DrinkPotion"" | ""TogglePrayer"" | ""ToggleSpecialAttack"" | ""DropItem"" | ""DropAllOfItem"" | ""DropAllExcept"" | ""LootGroundItem"" | ""OpenNearestBank"" | ""BankDepositAll"" | ""BankDepositAllExcept"" | ""BankWithdrawItem"" | ""CloseBank"" | ""AlchItem"" | ""RunAgilityObstacle"" | ""ContinueDialog"" | ""SelectDialogOption"" | ""WaitSeconds"" | ""WaitForIdle"",
      ""condition"": ""Always"" | ""InventoryFull"" | ""InventoryNotFull"" | ""InventoryHasItem"" | ""InventoryDoesNotHaveItem"" | ""HpBelowPercent"" | ""PrayerBelow"" | ""SpecialAttackAbove"" | ""RunEnergyBelow"" | ""PlayerIsIdle"" | ""PlayerInCombat"" | ""PlayerNotInCombat"" | ""BankIsOpen"" | ""BankIsClosed"" | ""DialogIsOpen"" | ""GroundItemNearby"",
      ""conditionArg"": ""Parameter for condition (e.g. 50 for HP %, 'Logs' for Has Item)"",
      ""targetName"": ""Tree"" | ""Goblin"" | ""Iron ore rock"" | ""Shark"" | ""Prayer potion"",
      ""actionVerb"": ""Attack"" | ""Chop down"" | ""Mine"" | ""Drink"" | ""Eat"" | ""Take"" | ""Bank"",
      ""param1"": ""Optional param (e.g. quantity, second item)"",
      ""param2"": """",
      ""waitAfterMs"": 1200,
      ""waitForAnimation"": true
    }}
  ]
}}

Only respond with the valid JSON block enclosed in ```json ... ```. No extra commentary.";
        }

        public static CustomScriptDefinition? ParseAiJson(string jsonText)
        {
            try
            {
                string cleaned = jsonText.Trim();
                if (cleaned.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                {
                    cleaned = cleaned.Substring(7);
                }
                else if (cleaned.StartsWith("```"))
                {
                    cleaned = cleaned.Substring(3);
                }
                if (cleaned.EndsWith("```"))
                {
                    cleaned = cleaned.Substring(0, cleaned.Length - 3);
                }
                cleaned = cleaned.Trim();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                };

                var def = JsonSerializer.Deserialize<CustomScriptDefinition>(cleaned, options);
                return def;
            }
            catch
            {
                return null;
            }
        }
    }

    public static class ScriptTemplates
    {
        public static List<CustomScriptDefinition> GetDefaultTemplates()
        {
            return new List<CustomScriptDefinition>
            {
                new CustomScriptDefinition
                {
                    Id = "template_power_chopper",
                    Name = "Visual Power Chopper",
                    Category = "Woodcutting",
                    Description = "Chops trees when inventory has space, drops logs when inventory is full.",
                    MinLoopDelayMs = 600,
                    MaxLoopDelayMs = 1200,
                    Steps = new List<CustomActionStep>
                    {
                        new CustomActionStep
                        {
                            Title = "Drop Logs When Full",
                            ActionType = CustomActionType.DropAllOfItem,
                            Condition = CustomConditionType.InventoryFull,
                            TargetName = "Logs",
                            ActionVerb = "Drop",
                            WaitAfterMs = 400
                        },
                        new CustomActionStep
                        {
                            Title = "Chop Nearest Tree",
                            ActionType = CustomActionType.ChopObject,
                            Condition = CustomConditionType.InventoryNotFull,
                            TargetName = "Tree",
                            ActionVerb = "Chop down",
                            WaitAfterMs = 1500
                        }
                    }
                },
                new CustomScriptDefinition
                {
                    Id = "template_woodcutter_banker",
                    Name = "Visual Woodcutter (With Banking)",
                    Category = "Woodcutting",
                    Description = "Chops trees until inventory is full, opens nearest bank, deposits all logs, and resumes.",
                    MinLoopDelayMs = 600,
                    MaxLoopDelayMs = 1200,
                    BankingOption = "Deposit All Except Tools",
                    Steps = new List<CustomActionStep>
                    {
                        new CustomActionStep
                        {
                            Title = "Close Bank If Open & Not Full",
                            ActionType = CustomActionType.CloseBank,
                            Condition = CustomConditionType.BankIsOpen,
                            TargetName = "Bank",
                            WaitAfterMs = 500
                        },
                        new CustomActionStep
                        {
                            Title = "Deposit Logs If Bank Is Open",
                            ActionType = CustomActionType.BankDepositAllExcept,
                            Condition = CustomConditionType.BankIsOpen,
                            TargetName = "axe",
                            WaitAfterMs = 800
                        },
                        new CustomActionStep
                        {
                            Title = "Open Bank When Inventory Full",
                            ActionType = CustomActionType.OpenNearestBank,
                            Condition = CustomConditionType.InventoryFull,
                            TargetName = "Bank booth",
                            ActionVerb = "Bank",
                            WaitAfterMs = 1500
                        },
                        new CustomActionStep
                        {
                            Title = "Chop Nearest Tree",
                            ActionType = CustomActionType.ChopObject,
                            Condition = CustomConditionType.InventoryNotFull,
                            TargetName = "Willow tree",
                            ActionVerb = "Chop down",
                            WaitAfterMs = 1500
                        }
                    }
                },
                new CustomScriptDefinition
                {
                    Id = "template_power_miner",
                    Name = "Visual Power Miner",
                    Category = "Mining",
                    Description = "Mines rocks until inventory is full, then drops all mined ore.",
                    MinLoopDelayMs = 600,
                    MaxLoopDelayMs = 1200,
                    Steps = new List<CustomActionStep>
                    {
                        new CustomActionStep
                        {
                            Title = "Drop Ore When Inventory Full",
                            ActionType = CustomActionType.DropAllExcept,
                            Condition = CustomConditionType.InventoryFull,
                            TargetName = "pickaxe",
                            ActionVerb = "Drop",
                            WaitAfterMs = 400
                        },
                        new CustomActionStep
                        {
                            Title = "Mine Nearest Rock",
                            ActionType = CustomActionType.MineObject,
                            Condition = CustomConditionType.InventoryNotFull,
                            TargetName = "Iron rocks",
                            ActionVerb = "Mine",
                            WaitAfterMs = 1400
                        }
                    }
                },
                new CustomScriptDefinition
                {
                    Id = "template_bank_miner",
                    Name = "Visual Miner (With Banking)",
                    Category = "Mining",
                    Description = "Mines ores, walks to nearest bank booth or chest, deposits all ore except pickaxe.",
                    MinLoopDelayMs = 600,
                    MaxLoopDelayMs = 1200,
                    Steps = new List<CustomActionStep>
                    {
                        new CustomActionStep
                        {
                            Title = "Deposit Ore When Bank Open",
                            ActionType = CustomActionType.BankDepositAllExcept,
                            Condition = CustomConditionType.BankIsOpen,
                            TargetName = "pickaxe",
                            WaitAfterMs = 800
                        },
                        new CustomActionStep
                        {
                            Title = "Close Bank After Deposit",
                            ActionType = CustomActionType.CloseBank,
                            Condition = CustomConditionType.BankIsOpen,
                            TargetName = "Bank",
                            WaitAfterMs = 500
                        },
                        new CustomActionStep
                        {
                            Title = "Open Bank When Full",
                            ActionType = CustomActionType.OpenNearestBank,
                            Condition = CustomConditionType.InventoryFull,
                            TargetName = "Bank booth",
                            ActionVerb = "Bank",
                            WaitAfterMs = 1500
                        },
                        new CustomActionStep
                        {
                            Title = "Mine Nearest Ore",
                            ActionType = CustomActionType.MineObject,
                            Condition = CustomConditionType.InventoryNotFull,
                            TargetName = "Iron rocks",
                            ActionVerb = "Mine",
                            WaitAfterMs = 1400
                        }
                    }
                },
                new CustomScriptDefinition
                {
                    Id = "template_auto_fighter",
                    Name = "Visual Auto Fighter (Combat + Food + Potions + Loot)",
                    Category = "Combat",
                    Description = "Attacks monsters, eats food when HP is low, drinks prayer/combat potions, and loots valuable drops.",
                    MinLoopDelayMs = 600,
                    MaxLoopDelayMs = 1200,
                    Steps = new List<CustomActionStep>
                    {
                        new CustomActionStep
                        {
                            Title = "Auto Eat Food When Low HP",
                            ActionType = CustomActionType.EatFood,
                            Condition = CustomConditionType.HpBelowPercent,
                            ConditionArg = "50",
                            TargetName = "Shark",
                            ActionVerb = "Eat",
                            WaitAfterMs = 600
                        },
                        new CustomActionStep
                        {
                            Title = "Drink Prayer Potion When Low",
                            ActionType = CustomActionType.DrinkPotion,
                            Condition = CustomConditionType.PrayerBelow,
                            ConditionArg = "20",
                            TargetName = "Prayer potion",
                            ActionVerb = "Drink",
                            WaitAfterMs = 600
                        },
                        new CustomActionStep
                        {
                            Title = "Activate Special Attack",
                            ActionType = CustomActionType.ToggleSpecialAttack,
                            Condition = CustomConditionType.SpecialAttackAbove,
                            ConditionArg = "50",
                            TargetName = "Special Attack",
                            WaitAfterMs = 300
                        },
                        new CustomActionStep
                        {
                            Title = "Loot Bones & Valuables",
                            ActionType = CustomActionType.LootGroundItem,
                            Condition = CustomConditionType.GroundItemNearby,
                            ConditionArg = "Bones",
                            TargetName = "Bones",
                            ActionVerb = "Take",
                            WaitAfterMs = 1000
                        },
                        new CustomActionStep
                        {
                            Title = "Loot Coins / Runes",
                            ActionType = CustomActionType.LootGroundItem,
                            Condition = CustomConditionType.GroundItemNearby,
                            ConditionArg = "Coins",
                            TargetName = "Coins",
                            ActionVerb = "Take",
                            WaitAfterMs = 1000
                        },
                        new CustomActionStep
                        {
                            Title = "Attack Target Monster",
                            ActionType = CustomActionType.AttackNpc,
                            Condition = CustomConditionType.PlayerNotInCombat,
                            TargetName = "Goblin",
                            ActionVerb = "Attack",
                            WaitAfterMs = 1200
                        }
                    }
                },
                new CustomScriptDefinition
                {
                    Id = "template_high_alcher",
                    Name = "Visual High Alcher",
                    Category = "Magic",
                    Description = "Repeatedly casts High Level Alchemy on configured items in inventory.",
                    MinLoopDelayMs = 2100,
                    MaxLoopDelayMs = 2500,
                    Steps = new List<CustomActionStep>
                    {
                        new CustomActionStep
                        {
                            Title = "Cast High Level Alchemy",
                            ActionType = CustomActionType.AlchItem,
                            Condition = CustomConditionType.InventoryHasItem,
                            ConditionArg = "Nature rune",
                            TargetName = "Yew longbow",
                            ActionVerb = "Cast",
                            WaitAfterMs = 2100
                        }
                    }
                },
                new CustomScriptDefinition
                {
                    Id = "template_rooftop_agility",
                    Name = "Visual Rooftop Agility Runner",
                    Category = "Agility",
                    Description = "Traverses rooftop obstacles, collects nearby Marks of Grace, and eats food when hurt.",
                    MinLoopDelayMs = 600,
                    MaxLoopDelayMs = 1200,
                    Steps = new List<CustomActionStep>
                    {
                        new CustomActionStep
                        {
                            Title = "Eat Food If Hurt",
                            ActionType = CustomActionType.EatFood,
                            Condition = CustomConditionType.HpBelowPercent,
                            ConditionArg = "40",
                            TargetName = "Cake",
                            ActionVerb = "Eat",
                            WaitAfterMs = 600
                        },
                        new CustomActionStep
                        {
                            Title = "Loot Mark of Grace",
                            ActionType = CustomActionType.LootGroundItem,
                            Condition = CustomConditionType.GroundItemNearby,
                            ConditionArg = "Mark of grace",
                            TargetName = "Mark of grace",
                            ActionVerb = "Take",
                            WaitAfterMs = 1200
                        },
                        new CustomActionStep
                        {
                            Title = "Traverse Nearest Agility Obstacle",
                            ActionType = CustomActionType.RunAgilityObstacle,
                            Condition = CustomConditionType.PlayerIsIdle,
                            TargetName = "Rough wall",
                            ActionVerb = "Climb",
                            WaitAfterMs = 1800
                        }
                    }
                },
                new CustomScriptDefinition
                {
                    Id = "template_auto_fisher",
                    Name = "Visual Auto Fisher & Cooker",
                    Category = "Fishing",
                    Description = "Catches fish, cooks raw catch on nearby fire/range, and drops cooked fish.",
                    MinLoopDelayMs = 600,
                    MaxLoopDelayMs = 1200,
                    Steps = new List<CustomActionStep>
                    {
                        new CustomActionStep
                        {
                            Title = "Drop Cooked Fish When Full",
                            ActionType = CustomActionType.DropAllOfItem,
                            Condition = CustomConditionType.InventoryFull,
                            TargetName = "Cooked trout",
                            ActionVerb = "Drop",
                            WaitAfterMs = 400
                        },
                        new CustomActionStep
                        {
                            Title = "Fish at Fishing Spot",
                            ActionType = CustomActionType.TalkNpc,
                            Condition = CustomConditionType.InventoryNotFull,
                            TargetName = "Fishing spot",
                            ActionVerb = "Lure",
                            WaitAfterMs = 2000
                        }
                    }
                },
                new CustomScriptDefinition
                {
                    Id = "template_loot_collector",
                    Name = "Visual Loot Collector",
                    Category = "Utility",
                    Description = "Continuously sweeps the ground for valuable drops and collects them.",
                    MinLoopDelayMs = 600,
                    MaxLoopDelayMs = 1000,
                    Steps = new List<CustomActionStep>
                    {
                        new CustomActionStep
                        {
                            Title = "Loot High Value Item",
                            ActionType = CustomActionType.LootGroundItem,
                            Condition = CustomConditionType.InventoryNotFull,
                            TargetName = "Rune scimitar",
                            ActionVerb = "Take",
                            WaitAfterMs = 1200
                        },
                        new CustomActionStep
                        {
                            Title = "Loot Coins",
                            ActionType = CustomActionType.LootGroundItem,
                            Condition = CustomConditionType.InventoryNotFull,
                            TargetName = "Coins",
                            ActionVerb = "Take",
                            WaitAfterMs = 1000
                        }
                    }
                }
            };
        }
    }
}
