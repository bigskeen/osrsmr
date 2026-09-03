using System;
using System.Collections.Generic;
using System.Linq;

namespace OsrsMr.Core
{
    public static class PacketDecoder
    {
        private static readonly int TreePrefix = ("Tree".GetHashCode() & 0x7FFF) * 1000;
        private static readonly int BankPrefix = ("Bank".GetHashCode() & 0x7FFF) * 1000;
        private static readonly int ShopPrefix = ("Shop".GetHashCode() & 0x7FFF) * 1000;
        private static readonly int AltarPrefix = ("Altar".GetHashCode() & 0x7FFF) * 1000;
        private static readonly int RockPrefix = ("Rock".GetHashCode() & 0x7FFF) * 1000;

        public static void Decode(GameState state, string key, string value)
        {
            state.LastUpdated = DateTime.UtcNow;

            switch (key)
            {
                case "PLAYER_NAME":
                    state.Player.Name = value;
                    return;

                case "COMBAT_LEVEL":
                    if (int.TryParse(value, out int cb)) state.Player.CombatLevel = cb;
                    return;

                case "HP":
                {
                    int slash = value.IndexOf('/');
                    if (slash != -1 &&
                        int.TryParse(value.AsSpan(0, slash), out int cur) &&
                        int.TryParse(value.AsSpan(slash + 1), out int max))
                    {
                        state.Player.CurrentHp = cur;
                        state.Player.MaxHp = max;
                    }
                    return;
                }

                case "PRAYER":
                {
                    int slash = value.IndexOf('/');
                    if (slash != -1 &&
                        int.TryParse(value.AsSpan(0, slash), out int cur) &&
                        int.TryParse(value.AsSpan(slash + 1), out int max))
                    {
                        state.Player.CurrentPrayer = cur;
                        state.Player.MaxPrayer = max;
                    }
                    return;
                }

                case "RUN_ENERGY":
                    if (int.TryParse(value, out int energy)) state.Player.Energy = energy;
                    return;

                case "WEIGHT":
                    if (int.TryParse(value, out int weight)) state.Player.Weight = weight;
                    return;

                case "SPECIAL_ATTACK":
                    if (int.TryParse(value, out int spec)) state.Player.SpecPercent = spec;
                    return;

                case "SPECIAL_ATTACK_ACTIVE":
                    state.Player.IsSpecActive = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                    return;

                case "MAGIC_SPELLBOOK":
                    state.Player.Spellbook = value;
                    return;

                case "AUTOCAST_SPELL":
                    state.Player.AutocastSpell = value;
                    return;

                case "ACTIVE_TAB":
                    state.Player.ActiveTab = value;
                    return;

                case "ANIMATION":
                    if (int.TryParse(value, out int anim)) state.Player.Animation = anim;
                    return;

                case "POSE_ANIMATION":
                    if (int.TryParse(value, out int poseAnim)) state.Player.PoseAnimation = poseAnim;
                    return;

                case "WORLD_LOCATION":
                {
                    ReadOnlySpan<char> span = value.AsSpan();
                    int comma1 = span.IndexOf(',');
                    if (comma1 != -1)
                    {
                        var part0 = span.Slice(0, comma1).Trim();
                        var remainder = span.Slice(comma1 + 1);
                        int comma2 = remainder.IndexOf(',');
                        if (comma2 != -1)
                        {
                            var part1 = remainder.Slice(0, comma2).Trim();
                            var part2 = remainder.Slice(comma2 + 1).Trim();
                            if (int.TryParse(part0, out int x) && int.TryParse(part1, out int y))
                            {
                                state.Player.WorldX = x;
                                state.Player.WorldY = y;
                                if (int.TryParse(part2, out int plane))
                                    state.Player.Plane = plane;
                            }
                        }
                        else
                        {
                            var part1 = remainder.Trim();
                            if (int.TryParse(part0, out int x) && int.TryParse(part1, out int y))
                            {
                                state.Player.WorldX = x;
                                state.Player.WorldY = y;
                            }
                        }
                    }
                    return;
                }

                case "PLAYER_X":
                    if (int.TryParse(value, out int px)) state.Player.WorldX = px;
                    return;

                case "PLAYER_Y":
                    if (int.TryParse(value, out int py)) state.Player.WorldY = py;
                    return;

                case "PLAYER_PLANE":
                case "PLANE":
                    if (int.TryParse(value, out int pPlane)) state.Player.Plane = pPlane;
                    return;

                case "TOWN":
                case "REGION_NAME":
                case "LOCATION_NAME":
                    state.Player.Town = value;
                    state.Player.Location = value;
                    return;

                case "LOCATION":
                    state.Player.Location = value;
                    return;

                case "REGION_ID":
                    if (int.TryParse(value, out int rId)) state.Player.RegionId = rId;
                    return;

                case "INTERACTING":
                {
                    bool isClean = !string.IsNullOrEmpty(value) && value != "None" && value != "Unknown" && !value.Equals("boolean", StringComparison.OrdinalIgnoreCase) && !value.Equals("true", StringComparison.OrdinalIgnoreCase) && !value.Equals("false", StringComparison.OrdinalIgnoreCase);
                    state.Player.IsInteracting = isClean;
                    state.Player.InteractingName = isClean ? value : "None";
                    return;
                }

                case "COMBAT_TARGET":
                {
                    bool isClean = !string.IsNullOrEmpty(value) && value != "None" && value != "Unknown" && !value.Equals("boolean", StringComparison.OrdinalIgnoreCase) && !value.Equals("true", StringComparison.OrdinalIgnoreCase) && !value.Equals("false", StringComparison.OrdinalIgnoreCase);
                    state.Player.CombatTarget = isClean ? value : "None";
                    return;
                }

                case "INTERACTING_TYPE":
                    state.Player.InteractingType = value;
                    return;

                case "INTERACTING_ID":
                    if (int.TryParse(value, out int intId)) state.Player.InteractingId = intId;
                    return;

                case "COMBAT_TARGET_LEVEL":
                    if (int.TryParse(value, out int targetLvl)) state.Player.TargetCombatLevel = targetLvl;
                    return;

                case "COMBAT_TARGET_HP":
                    state.Player.TargetHealth = value;
                    return;

                case "COMBAT_TARGET_DISTANCE":
                    if (int.TryParse(value, out int tDist)) state.Player.TargetDistance = tDist;
                    return;

                case "COMBAT_ENEMY_PRAYER":
                case "ENEMY_PRAYER":
                    state.Player.EnemyPrayer = value;
                    return;

                case "COMBAT_ENEMY_STYLE":
                case "ENEMY_ATTACK_STYLE":
                    state.Player.EnemyAttackStyle = value;
                    return;

                case "COMBAT_ENEMY_WEAPON":
                case "ENEMY_WEAPON":
                    state.Player.EnemyWeapon = value;
                    return;

                case "COMBAT_ENEMY_GEAR":
                case "ENEMY_GEAR":
                case "ENEMY_EQUIPMENT":
                    state.Player.EnemyGear = value;
                    return;

                case "COMBAT_ENEMY_ANIMATION":
                case "ENEMY_ANIMATION":
                case "TARGET_ANIMATION":
                    if (int.TryParse(value, out int ea)) state.Player.EnemyAnimation = ea;
                    return;

                case "COMBAT_ENEMY_POSE":
                case "ENEMY_POSE":
                    if (int.TryParse(value, out int ep)) state.Player.EnemyPoseAnimation = ep;
                    return;

                case "IS_MOVING":
                    state.Player.IsMoving = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                    return;

                case "IS_IDLE":
                    state.Player.IsIdle = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                    return;

                case "IS_INSTANCED":
                case "IN_INSTANCE":
                {
                    bool isInst = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                    state.Player.IsInstanced = isInst;
                    state.IsInstanced = isInst;
                    return;
                }

                case "VENGEANCE_ACTIVE":
                    state.Player.IsVengeanceActive = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                    return;

                case "WILDERNESS_LEVEL":
                    if (int.TryParse(value, out int wl)) state.Player.WildernessLevel = wl;
                    return;

                case "IN_WILDERNESS":
                    state.Player.IsInWilderness = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                    return;

                case "COMBAT_UNDER_ATTACK":
                case "UNDER_ATTACK":
                    state.Player.IsUnderAttack = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                    return;

                case "IN_COMBAT":
                case "IS_IN_COMBAT":
                {
                    bool inCombat = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                    state.Player.IsInCombat = inCombat;
                    if (!inCombat)
                    {
                        state.Player.CombatTarget = "None";
                        state.Player.TargetCombatLevel = 0;
                        state.Player.TargetHealth = "None";
                        state.Player.TargetDistance = 0;
                        state.Player.EnemyPrayer = "None";
                        state.Player.EnemyAttackStyle = "None";
                        state.Player.EnemyWeapon = "None";
                        state.Player.EnemyGear = "None";
                        state.Player.EnemyAnimation = -1;
                        state.Player.EnemyPoseAnimation = -1;
                        state.Player.IsUnderAttack = false;
                        state.Player.UnderAttackBy = "None";
                        state.AttackingEnemies.Clear();
                        state.EnemyEquipment.Clear();
                    }
                    return;
                }

                case "IS_FIGHTING":
                    state.Player.IsFighting = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                    return;

                case "IS_ATTACKING":
                    state.Player.IsAttacking = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                    return;

                case "ATTACKED_BY":
                case "UNDER_ATTACK_BY":
                    state.Player.UnderAttackBy = value;
                    return;

                case "ATTACKING_ENEMIES_COUNT":
                    if (int.TryParse(value, out int aec) && aec == 0)
                    {
                        state.AttackingEnemies.Clear();
                    }
                    return;

                case "INVENTORY_CLEAR":
                    state.Inventory.Clear();
                    return;

                case "TOTAL_NPCS":
                    if (int.TryParse(value, out int totalNpcs))
                    {
                        foreach (var k in state.Npcs.Keys)
                        {
                            if (k >= totalNpcs) state.Npcs.TryRemove(k, out _);
                        }
                    }
                    return;

                case "TOTAL_PLAYERS":
                case "TOTAL_NEARBY_PLAYERS":
                    if (int.TryParse(value, out int totalPlayers))
                    {
                        foreach (var k in state.NearbyPlayers.Keys)
                        {
                            if (k >= totalPlayers) state.NearbyPlayers.TryRemove(k, out _);
                        }
                    }
                    return;

                case "TOTAL_TREES":
                    if (int.TryParse(value, out int totalTrees))
                    {
                        int prefix = TreePrefix;
                        foreach (var k in state.Objects.Keys)
                        {
                            if (k >= prefix + totalTrees && k < prefix + 1000) state.Objects.TryRemove(k, out _);
                        }
                    }
                    return;

                case "TOTAL_BANKS":
                    if (int.TryParse(value, out int totalBanks))
                    {
                        int prefix = BankPrefix;
                        foreach (var k in state.Objects.Keys)
                        {
                            if (k >= prefix + totalBanks && k < prefix + 1000) state.Objects.TryRemove(k, out _);
                        }
                    }
                    return;

                case "TOTAL_SHOPS":
                    if (int.TryParse(value, out int totalShops))
                    {
                        int prefix = ShopPrefix;
                        foreach (var k in state.Objects.Keys)
                        {
                            if (k >= prefix + totalShops && k < prefix + 1000) state.Objects.TryRemove(k, out _);
                        }
                    }
                    return;

                case "TOTAL_ALTARS":
                    if (int.TryParse(value, out int totalAltars))
                    {
                        int prefix = AltarPrefix;
                        foreach (var k in state.Objects.Keys)
                        {
                            if (k >= prefix + totalAltars && k < prefix + 1000) state.Objects.TryRemove(k, out _);
                        }
                    }
                    return;

                case "TOTAL_ROCKS":
                    if (int.TryParse(value, out int totalRocks))
                    {
                        int prefix = RockPrefix;
                        foreach (var k in state.Objects.Keys)
                        {
                            if (k >= prefix + totalRocks && k < prefix + 1000) state.Objects.TryRemove(k, out _);
                        }
                    }
                    return;

                case "TOTAL_GROUND_ITEMS":
                    if (int.TryParse(value, out int totalGround) && totalGround == 0)
                    {
                        state.GroundItems.Clear();
                    }
                    return;

                case "BANK_TOTAL_ITEMS":
                case "TOTAL_BANK_ITEMS":
                    if (int.TryParse(value, out int totalBank))
                    {
                        if (totalBank > 0) state.IsBankOpen = true;
                        foreach (var k in state.Bank.Keys)
                        {
                            if (k >= totalBank) state.Bank.TryRemove(k, out _);
                        }
                    }
                    return;

                case "SHOP_TOTAL_ITEMS":
                case "TOTAL_SHOP_ITEMS":
                    if (int.TryParse(value, out int totalShop))
                    {
                        if (totalShop > 0) state.IsShopOpen = true;
                        foreach (var k in state.Shop.Keys)
                        {
                            if (k >= totalShop) state.Shop.TryRemove(k, out _);
                        }
                    }
                    return;

                case "GEM_BAG":
                {
                    var tokenizer = new SpanTokenizer(value.AsSpan());
                    if (tokenizer.TryGetNext(out var p0) && int.TryParse(p0, out int s) &&
                        tokenizer.TryGetNext(out var p1) && int.TryParse(p1, out int e) &&
                        tokenizer.TryGetNext(out var p2) && int.TryParse(p2, out int r) &&
                        tokenizer.TryGetNext(out var p3) && int.TryParse(p3, out int d) &&
                        tokenizer.TryGetNext(out var p4) && int.TryParse(p4, out int ds))
                    {
                        state.GemBag.Sapphires = s;
                        state.GemBag.Emeralds = e;
                        state.GemBag.Rubies = r;
                        state.GemBag.Diamonds = d;
                        state.GemBag.Dragonstones = ds;
                    }
                    return;
                }

                case "ESSENCE_POUCHES":
                {
                    var tokenizer = new SpanTokenizer(value.AsSpan());
                    if (tokenizer.TryGetNext(out var p0) && int.TryParse(p0, out int sm) &&
                        tokenizer.TryGetNext(out var p1) && int.TryParse(p1, out int md) &&
                        tokenizer.TryGetNext(out var p2) && int.TryParse(p2, out int lg) &&
                        tokenizer.TryGetNext(out var p3) && int.TryParse(p3, out int gt) &&
                        tokenizer.TryGetNext(out var p4) && int.TryParse(p4, out int col))
                    {
                        state.EssencePouches.Small = sm;
                        state.EssencePouches.Medium = md;
                        state.EssencePouches.Large = lg;
                        state.EssencePouches.Giant = gt;
                        state.EssencePouches.Colossal = col;
                    }
                    return;
                }

                case "DIALOG_OPEN":
                    state.Dialog.IsOpen = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                    return;

                case "DIALOG_TYPE":
                    state.Dialog.Type = value;
                    return;

                case "DIALOG_SPEAKER":
                    state.Dialog.Speaker = value;
                    return;

                case "DIALOG_TEXT":
                    state.Dialog.Text = value;
                    return;

                case "DIALOG_OPTIONS":
                {
                    var options = new List<string>();
                    ReadOnlySpan<char> span = value.AsSpan();
                    while (!span.IsEmpty)
                    {
                        int sepIdx = span.IndexOf(" | ".AsSpan());
                        if (sepIdx == -1)
                        {
                            var opt = span.Trim();
                            if (!opt.IsEmpty) options.Add(opt.ToString());
                            break;
                        }
                        var optSegment = span.Slice(0, sepIdx).Trim();
                        if (!optSegment.IsEmpty) options.Add(optSegment.ToString());
                        span = span.Slice(sepIdx + 3);
                    }
                    state.Dialog.Options = options;
                    return;
                }

                case "SLAYER_TASK":
                    state.Slayer.TaskName = value;
                    return;

                case "SLAYER_REMAINING":
                case "SLAYER_COUNT":
                    if (int.TryParse(value, out int rem)) state.Slayer.AmountRemaining = rem;
                    return;

                case "SLAYER_MASTER":
                case "SLAYER_MASTER_NEARBY":
                    state.Slayer.Master = value;
                    return;

                case "SLAYER_POINTS":
                    if (int.TryParse(value, out int pts)) state.Slayer.Points = pts;
                    return;

                case "SLAYER_STREAK":
                    if (int.TryParse(value, out int strk)) state.Slayer.Streak = strk;
                    return;

                case "MINIGAME_ACTIVE":
                    state.Minigame.IsActive = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                    return;

                case "MINIGAME_NAME":
                    state.Minigame.Name = value;
                    return;

                case "MINIGAME_STATUS":
                    state.Minigame.Status = value;
                    return;

                case "MINIGAME_POINTS":
                    state.Minigame.Points = value;
                    return;

                case "MINIGAME_EXTRA":
                    state.Minigame.Extra = value;
                    return;

                case "AGILITY_COURSE":
                    state.Agility.CurrentCourse = value;
                    return;

                case "AGILITY_COURSE_LEVEL":
                    if (int.TryParse(value, out int lvl)) state.Agility.CourseLevelReq = lvl;
                    return;

                case "MARKS_OF_GRACE_COUNT":
                    if (int.TryParse(value, out int mog)) state.Agility.MarksOfGraceNearby = mog;
                    return;

                case "BANK_OPEN":
                case "IS_BANK_OPEN":
                {
                    bool isOpen = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                    state.IsBankOpen = isOpen;
                    if (!isOpen) state.Bank.Clear();
                    return;
                }

                case "SHOP_OPEN":
                case "IS_SHOP_OPEN":
                {
                    bool isOpen = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                    state.IsShopOpen = isOpen;
                    if (!isOpen) state.Shop.Clear();
                    return;
                }

                case "GE_OPEN":
                case "IS_GE_OPEN":
                {
                    bool isOpen = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                    state.IsGrandExchangeOpen = isOpen;
                    return;
                }

                case "CURRENT_BANK":
                case "BANK_LOCATION":
                    state.CurrentBank = value;
                    return;

                case "NEAREST_BANK":
                    state.NearestBank = value;
                    return;

                case "NEAREST_BANK_DIST":
                    if (int.TryParse(value, out int nbd)) state.NearestBankDistance = nbd;
                    return;

                case "IN_BANK":
                    state.InBank = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                    return;

                case "CURRENT_SHOP":
                case "SHOP_LOCATION":
                    state.CurrentShop = value;
                    state.ShopLocation = value;
                    state.ShopName = value;
                    return;

                case "NEAREST_SHOP":
                    state.NearestShop = value;
                    return;

                case "NEAREST_SHOP_DIST":
                    if (int.TryParse(value, out int nsd)) state.NearestShopDistance = nsd;
                    return;

                case "IN_SHOP":
                    state.InShop = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                    return;

                case "SHOP_NAME":
                    state.ShopName = value;
                    return;

                // Game Lifecycle & World
                case "GAME_TICK":
                    if (int.TryParse(value, out int gTick))
                    {
                        if (state.GameTick != gTick)
                        {
                            state.GameTick = gTick;
                            OsrsMr.Core.Scripting.EventBus.Publish(new OsrsMr.Core.Scripting.TickEvent(gTick));
                        }
                    }
                    return;

                case "WORLD":
                    if (int.TryParse(value, out int wNum)) state.WorldNumber = wNum;
                    return;

                case "ENGINE_STATE":
                    state.EngineState = value;
                    return;

                // Camera & Viewport
                case "CAMERA_PITCH":
                    if (int.TryParse(value, out int cPitch)) state.Camera.Pitch = cPitch;
                    return;

                case "CAMERA_YAW":
                    if (int.TryParse(value, out int cYaw)) state.Camera.Yaw = cYaw;
                    return;

                case "CAMERA_ZOOM":
                    if (int.TryParse(value, out int cZoom)) state.Camera.Zoom = cZoom;
                    return;

                case "CAMERA_SCALE":
                    if (int.TryParse(value, out int cScale)) state.Camera.Scale = cScale;
                    return;

                case "CAMERA_POS":
                {
                    var tokenizer = new SpanTokenizer(value.AsSpan());
                    if (tokenizer.TryGetNext(out var pX) && int.TryParse(pX, out int cx) &&
                        tokenizer.TryGetNext(out var pY) && int.TryParse(pY, out int cy) &&
                        tokenizer.TryGetNext(out var pZ) && int.TryParse(pZ, out int cz))
                    {
                        state.Camera.X = cx;
                        state.Camera.Y = cy;
                        state.Camera.Z = cz;
                    }
                    return;
                }

                case "CANVAS_SIZE":
                {
                    var tokenizer = new SpanTokenizer(value.AsSpan());
                    if (tokenizer.TryGetNext(out var pW) && int.TryParse(pW, out int cw) &&
                        tokenizer.TryGetNext(out var pH) && int.TryParse(pH, out int ch))
                    {
                        state.Camera.CanvasWidth = cw;
                        state.Camera.CanvasHeight = ch;
                    }
                    return;
                }

                case "VIEWPORT_BOUNDS":
                {
                    var tokenizer = new SpanTokenizer(value.AsSpan());
                    if (tokenizer.TryGetNext(out var pW) && int.TryParse(pW, out int vw) &&
                        tokenizer.TryGetNext(out var pH) && int.TryParse(pH, out int vh) &&
                        tokenizer.TryGetNext(out var pX) && int.TryParse(pX, out int vx) &&
                        tokenizer.TryGetNext(out var pY) && int.TryParse(pY, out int vy))
                    {
                        state.Camera.ViewportWidth = vw;
                        state.Camera.ViewportHeight = vh;
                        state.Camera.ViewportOffsetX = vx;
                        state.Camera.ViewportOffsetY = vy;
                    }
                    return;
                }

                // Buffs & Status Timers
                case "STATUS_POISON":
                {
                    var tokenizer = new SpanTokenizer(value.AsSpan());
                    if (tokenizer.TryGetNext(out var pP) && tokenizer.TryGetNext(out var pD))
                    {
                        state.StatusEffects.IsPoisoned = pP.Equals("true", StringComparison.OrdinalIgnoreCase);
                        if (int.TryParse(pD, out int pd)) state.StatusEffects.PoisonDamage = pd;
                    }
                    return;
                }

                case "STATUS_VENOM":
                {
                    var tokenizer = new SpanTokenizer(value.AsSpan());
                    if (tokenizer.TryGetNext(out var pV) && tokenizer.TryGetNext(out var pD))
                    {
                        state.StatusEffects.IsEnvenomed = pV.Equals("true", StringComparison.OrdinalIgnoreCase);
                        if (int.TryParse(pD, out int vd)) state.StatusEffects.VenomDamage = vd;
                    }
                    return;
                }

                case "STATUS_ANTIFIRE":
                {
                    var tokenizer = new SpanTokenizer(value.AsSpan());
                    if (tokenizer.TryGetNext(out var pT) && tokenizer.TryGetNext(out var pS))
                    {
                        if (int.TryParse(pT, out int ticks)) state.StatusEffects.AntifireTicks = ticks;
                        state.StatusEffects.IsSuperAntifire = pS.Equals("true", StringComparison.OrdinalIgnoreCase);
                    }
                    return;
                }

                case "STATUS_STAMINA":
                case "BUFF_STAMINA":
                    if (int.TryParse(value, out int sTicks)) state.StatusEffects.StaminaTicks = sTicks;
                    return;

                case "BUFF_ANTIFIRE":
                    if (int.TryParse(value, out int afTicks)) state.StatusEffects.AntifireTicks = afTicks;
                    return;

                case "BUFF_SUPER_ANTIFIRE":
                    if (int.TryParse(value, out int safTicks)) state.StatusEffects.SuperAntifireTicks = safTicks;
                    return;

                case "BUFF_OVERLOAD":
                    if (int.TryParse(value, out int ovlTicks)) state.StatusEffects.OverloadTicks = ovlTicks;
                    return;

                case "BUFF_DIVINE":
                    if (int.TryParse(value, out int divTicks)) state.StatusEffects.DivineTicks = divTicks;
                    return;

                case "BUFF_IMBUED_HEART":
                    if (int.TryParse(value, out int heartTicks)) state.StatusEffects.ImbuedHeartCooldownTicks = heartTicks;
                    return;

                case "BUFF_PRAYER_ENHANCE":
                    if (int.TryParse(value, out int peTicks)) state.StatusEffects.PrayerEnhanceTicks = peTicks;
                    return;

                case "BUFF_CHARGE":
                    if (int.TryParse(value, out int chgTicks)) state.StatusEffects.ChargeTicks = chgTicks;
                    return;

                case "POISON_STATE":
                    if (value.Equals("Poisoned", StringComparison.OrdinalIgnoreCase))
                    {
                        state.StatusEffects.IsPoisoned = true;
                        state.StatusEffects.IsEnvenomed = false;
                    }
                    else if (value.Equals("Venomed", StringComparison.OrdinalIgnoreCase))
                    {
                        state.StatusEffects.IsPoisoned = false;
                        state.StatusEffects.IsEnvenomed = true;
                    }
                    else
                    {
                        state.StatusEffects.IsPoisoned = false;
                        state.StatusEffects.IsEnvenomed = false;
                    }
                    return;

                case "POISON_DAMAGE":
                    if (int.TryParse(value, out int pDmg))
                    {
                        if (state.StatusEffects.IsEnvenomed) state.StatusEffects.VenomDamage = pDmg;
                        else state.StatusEffects.PoisonDamage = pDmg;
                    }
                    return;

                case "POISON_IMMUNITY_TICKS":
                case "STATUS_IMMUNITY_VENOM":
                    if (int.TryParse(value, out int vImm))
                    {
                        state.StatusEffects.VenomImmunityTicks = vImm;
                        state.StatusEffects.PoisonImmunityTicks = vImm;
                    }
                    return;

                case "AUTO_RETALIATE":
                    state.StatusEffects.AutoRetaliate = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                    return;

                case "RUN_MODE":
                    state.StatusEffects.RunEnabled = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                    return;

                // Active Prayers
                case "ACTIVE_PRAYERS":
                {
                    state.ActivePrayers.Active.Clear();
                    if (!value.Equals("None", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(value))
                    {
                        var tokenizer = new SpanTokenizer(value.AsSpan());
                        while (tokenizer.TryGetNext(out var prayerSpan))
                        {
                            if (!prayerSpan.IsEmpty && !prayerSpan.Equals("None", StringComparison.OrdinalIgnoreCase))
                            {
                                state.ActivePrayers.Active.Add(prayerSpan.ToString());
                            }
                        }
                    }
                    return;
                }

                // Projectiles & Graphics Objects
                case "PROJECTILE":
                {
                    var tokenizer = new SpanTokenizer(value.AsSpan());
                    if (tokenizer.TryGetNext(out var pId) && int.TryParse(pId, out int projId))
                    {
                        int sx = tokenizer.TryGetNext(out var psx) && int.TryParse(psx, out int x1) ? x1 : 0;
                        int sy = tokenizer.TryGetNext(out var psy) && int.TryParse(psy, out int y1) ? y1 : 0;
                        int tx = tokenizer.TryGetNext(out var ptx) && int.TryParse(ptx, out int x2) ? x2 : 0;
                        int ty = tokenizer.TryGetNext(out var pty) && int.TryParse(pty, out int y2) ? y2 : 0;
                        int tIdx = tokenizer.TryGetNext(out var pti) && int.TryParse(pti, out int ti) ? ti : -1;
                        int plane = tokenizer.TryGetNext(out var ppl) && int.TryParse(ppl, out int pl) ? pl : 0;
                        int remCycles = tokenizer.TryGetNext(out var prc) && int.TryParse(prc, out int rc) ? rc : 0;
                        int endCycle = tokenizer.TryGetNext(out var pec) && int.TryParse(pec, out int ec) ? ec : 0;

                        state.Projectiles[projId] = new ProjectileSnapshot
                        {
                            Id = projId,
                            StartX = sx,
                            StartY = sy,
                            TargetX = tx,
                            TargetY = ty,
                            TargetIndex = tIdx,
                            Plane = plane,
                            RemainingCycles = remCycles,
                            EndCycle = endCycle
                        };
                    }
                    return;
                }

                case "GRAPHICS_OBJECT":
                {
                    var tokenizer = new SpanTokenizer(value.AsSpan());
                    if (tokenizer.TryGetNext(out var pId) && int.TryParse(pId, out int goId))
                    {
                        int wx = tokenizer.TryGetNext(out var pwx) && int.TryParse(pwx, out int x) ? x : 0;
                        int wy = tokenizer.TryGetNext(out var pwy) && int.TryParse(pwy, out int y) ? y : 0;
                        int plane = tokenizer.TryGetNext(out var ppl) && int.TryParse(ppl, out int pl) ? pl : 0;
                        int startCycle = tokenizer.TryGetNext(out var psc) && int.TryParse(psc, out int sc) ? sc : 0;
                        int level = tokenizer.TryGetNext(out var plv) && int.TryParse(plv, out int lv) ? lv : 0;

                        string keyGo = $"{goId}_{wx}_{wy}_{plane}";
                        state.GraphicsObjects[keyGo] = new GraphicsObjectSnapshot
                        {
                            Id = goId,
                            WorldX = wx,
                            WorldY = wy,
                            Plane = plane,
                            StartCycle = startCycle,
                            Level = level
                        };
                    }
                    return;
                }

                case "EQUIPMENT_BONUSES":
                {
                    var tokenizer = new SpanTokenizer(value.AsSpan());
                    if (tokenizer.TryGetNext(out var p0) && int.TryParse(p0, out int aStab) &&
                        tokenizer.TryGetNext(out var p1) && int.TryParse(p1, out int aSlash) &&
                        tokenizer.TryGetNext(out var p2) && int.TryParse(p2, out int aCrush) &&
                        tokenizer.TryGetNext(out var p3) && int.TryParse(p3, out int aMagic) &&
                        tokenizer.TryGetNext(out var p4) && int.TryParse(p4, out int aRange) &&
                        tokenizer.TryGetNext(out var p5) && int.TryParse(p5, out int dStab) &&
                        tokenizer.TryGetNext(out var p6) && int.TryParse(p6, out int dSlash) &&
                        tokenizer.TryGetNext(out var p7) && int.TryParse(p7, out int dCrush) &&
                        tokenizer.TryGetNext(out var p8) && int.TryParse(p8, out int dMagic) &&
                        tokenizer.TryGetNext(out var p9) && int.TryParse(p9, out int dRange) &&
                        tokenizer.TryGetNext(out var p10) && int.TryParse(p10, out int mStr) &&
                        tokenizer.TryGetNext(out var p11) && int.TryParse(p11, out int rStr) &&
                        tokenizer.TryGetNext(out var p12) && int.TryParse(p12, out int mDmg) &&
                        tokenizer.TryGetNext(out var p13) && int.TryParse(p13, out int pray))
                    {
                        state.EquipmentBonuses.AttackStab = aStab;
                        state.EquipmentBonuses.AttackSlash = aSlash;
                        state.EquipmentBonuses.AttackCrush = aCrush;
                        state.EquipmentBonuses.AttackMagic = aMagic;
                        state.EquipmentBonuses.AttackRange = aRange;
                        state.EquipmentBonuses.DefenceStab = dStab;
                        state.EquipmentBonuses.DefenceSlash = dSlash;
                        state.EquipmentBonuses.DefenceCrush = dCrush;
                        state.EquipmentBonuses.DefenceMagic = dMagic;
                        state.EquipmentBonuses.DefenceRange = dRange;
                        state.EquipmentBonuses.MeleeStrength = mStr;
                        state.EquipmentBonuses.RangedStrength = rStr;
                        state.EquipmentBonuses.MagicDamage = mDmg;
                        state.EquipmentBonuses.PrayerBonus = pray;
                    }
                    return;
                }

                case "MENU_ENTRY":
                {
                    var tokenizer = new SpanTokenizer(value.AsSpan());
                    if (tokenizer.TryGetNext(out var pIdx) && int.TryParse(pIdx, out int mIdx) &&
                        tokenizer.TryGetNext(out var pOpt) &&
                        tokenizer.TryGetNext(out var pTgt))
                    {
                        int id = tokenizer.TryGetNext(out var pid) && int.TryParse(pid, out int mid) ? mid : 0;
                        int opc = tokenizer.TryGetNext(out var pop) && int.TryParse(pop, out int mop) ? mop : 0;
                        int p0 = tokenizer.TryGetNext(out var pp0) && int.TryParse(pp0, out int mp0) ? mp0 : 0;
                        int p1 = tokenizer.TryGetNext(out var pp1) && int.TryParse(pp1, out int mp1) ? mp1 : 0;

                        if (mIdx == 0) state.MenuEntries.Clear();

                        state.MenuEntries.Add(new MenuEntrySnapshot
                        {
                            Index = mIdx,
                            Option = pOpt.ToString(),
                            Target = pTgt.ToString(),
                            Identifier = id,
                            Opcode = opc,
                            Param0 = p0,
                            Param1 = p1
                        });
                    }
                    return;
                }
            }

            // Fast Indexed / Prefix Keys
            int openBracket = key.IndexOf('[');
            if (openBracket != -1 && key.EndsWith(']'))
            {
                ReadOnlySpan<char> prefix = key.AsSpan(0, openBracket);
                int closeBracket = key.Length - 1;
                int slot = int.TryParse(key.AsSpan(openBracket + 1, closeBracket - openBracket - 1), out int idx) ? idx : -1;

                if (slot != -1)
                {
                    if (prefix.Equals("INV", StringComparison.OrdinalIgnoreCase) || prefix.Equals("ITEM", StringComparison.OrdinalIgnoreCase))
                    {
                        var tokenizer = new SpanTokenizer(value.AsSpan());
                        if (tokenizer.TryGetNext(out var p0))
                        {
                            if (p0.Equals("EMPTY", StringComparison.OrdinalIgnoreCase) || p0.SequenceEqual("0") || p0.SequenceEqual("-1") || p0.IsEmpty)
                            {
                                state.Inventory.TryRemove(slot, out _);
                            }
                            else if (tokenizer.TryGetNext(out var p1))
                            {
                                if (tokenizer.TryGetNext(out var p2))
                                {
                                    if (int.TryParse(p0, out int id) && int.TryParse(p2, out int qty))
                                    {
                                        if (id <= 0 || qty <= 0 || id == 65535)
                                        {
                                            state.Inventory.TryRemove(slot, out _);
                                        }
                                        else
                                        {
                                            string nameStr = p1.ToString();
                                            ItemDatabase.RegisterItem(id, nameStr);
                                            if (state.Inventory.TryGetValue(slot, out var existing))
                                            {
                                                existing.Id = id;
                                                existing.Name = UpdateString(existing.Name, p1);
                                                existing.Quantity = qty;
                                            }
                                            else
                                            {
                                                state.Inventory[slot] = new ItemSnapshot { Slot = slot, Id = id, Name = nameStr, Quantity = qty };
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    int qty2 = int.TryParse(p1, out int q) ? q : 1;
                                    int parsedId = int.TryParse(p0, out int pid) ? pid : 0;
                                    if (parsedId <= 0 && (p0.SequenceEqual("0") || p0.SequenceEqual("-1") || p0.IsEmpty || qty2 <= 0 || p0.Equals("EMPTY", StringComparison.OrdinalIgnoreCase)))
                                    {
                                        state.Inventory.TryRemove(slot, out _);
                                    }
                                    else if (parsedId > 0 && parsedId != 65535)
                                    {
                                        string nameStr = ItemDatabase.GetItemName(parsedId);
                                        if (string.IsNullOrEmpty(nameStr)) nameStr = $"Item #{parsedId}";
                                        if (state.Inventory.TryGetValue(slot, out var existing))
                                        {
                                            existing.Id = parsedId;
                                            existing.Name = nameStr;
                                            existing.Quantity = qty2;
                                        }
                                        else
                                        {
                                            state.Inventory[slot] = new ItemSnapshot { Slot = slot, Id = parsedId, Name = nameStr, Quantity = qty2 };
                                        }
                                    }
                                    else if (state.Inventory.TryGetValue(slot, out var existing))
                                    {
                                        existing.Id = parsedId;
                                        existing.Name = UpdateString(existing.Name, p0);
                                        existing.Quantity = qty2;
                                    }
                                    else
                                    {
                                        state.Inventory[slot] = new ItemSnapshot { Slot = slot, Id = parsedId, Name = p0.ToString(), Quantity = qty2 };
                                    }
                                }
                            }
                            else
                            {
                                state.Inventory.TryRemove(slot, out _);
                            }
                        }
                        else
                        {
                            state.Inventory.TryRemove(slot, out _);
                        }
                        return;
                    }

                    if (prefix.Equals("EQUIP", StringComparison.OrdinalIgnoreCase))
                    {
                        var tokenizer = new SpanTokenizer(value.AsSpan());
                        if (tokenizer.TryGetNext(out var p0))
                        {
                            if (p0.Equals("EMPTY", StringComparison.OrdinalIgnoreCase) || p0.SequenceEqual("0") || p0.SequenceEqual("-1") || p0.IsEmpty)
                            {
                                state.Equipment.TryRemove(slot, out _);
                            }
                            else if (tokenizer.TryGetNext(out var p1))
                            {
                                if (tokenizer.TryGetNext(out var p2))
                                {
                                    if (int.TryParse(p0, out int id) && int.TryParse(p2, out int qty))
                                    {
                                        if (id <= 0 || qty <= 0 || id == 65535)
                                        {
                                            state.Equipment.TryRemove(slot, out _);
                                        }
                                        else
                                        {
                                            string nameStr = p1.ToString();
                                            ItemDatabase.RegisterItem(id, nameStr);
                                            if (state.Equipment.TryGetValue(slot, out var existing))
                                            {
                                                existing.Id = id;
                                                existing.Name = UpdateString(existing.Name, p1);
                                                existing.Quantity = qty;
                                            }
                                            else
                                            {
                                                state.Equipment[slot] = new ItemSnapshot { Slot = slot, Id = id, Name = nameStr, Quantity = qty };
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    int qty2 = int.TryParse(p1, out int q) ? q : 1;
                                    int parsedId = int.TryParse(p0, out int pid) ? pid : 0;
                                    if (parsedId <= 0 && (p0.SequenceEqual("0") || p0.SequenceEqual("-1") || p0.IsEmpty || qty2 <= 0 || p0.Equals("EMPTY", StringComparison.OrdinalIgnoreCase)))
                                    {
                                        state.Equipment.TryRemove(slot, out _);
                                    }
                                    else if (parsedId > 0 && parsedId != 65535)
                                    {
                                        string nameStr = ItemDatabase.GetItemName(parsedId);
                                        if (string.IsNullOrEmpty(nameStr)) nameStr = $"Item #{parsedId}";
                                        if (state.Equipment.TryGetValue(slot, out var existing))
                                        {
                                            existing.Id = parsedId;
                                            existing.Name = nameStr;
                                            existing.Quantity = qty2;
                                        }
                                        else
                                        {
                                            state.Equipment[slot] = new ItemSnapshot { Slot = slot, Id = parsedId, Name = nameStr, Quantity = qty2 };
                                        }
                                    }
                                    else if (state.Equipment.TryGetValue(slot, out var existing))
                                    {
                                        existing.Id = parsedId;
                                        existing.Name = UpdateString(existing.Name, p0);
                                        existing.Quantity = qty2;
                                    }
                                    else
                                    {
                                        state.Equipment[slot] = new ItemSnapshot { Slot = slot, Id = parsedId, Name = p0.ToString(), Quantity = qty2 };
                                    }
                                }
                            }
                            else
                            {
                                state.Equipment.TryRemove(slot, out _);
                            }
                        }
                        else
                        {
                            state.Equipment.TryRemove(slot, out _);
                        }
                        return;
                    }

                    if (prefix.Equals("ENEMY_EQUIP", StringComparison.OrdinalIgnoreCase))
                    {
                        var tokenizer = new SpanTokenizer(value.AsSpan());
                        if (tokenizer.TryGetNext(out var p0))
                        {
                            if (tokenizer.TryGetNext(out var p1))
                            {
                                int qty = tokenizer.TryGetNext(out var p2) && int.TryParse(p2, out int q) ? q : 1;
                                if (int.TryParse(p0, out int id) && id > 0)
                                {
                                    if (state.EnemyEquipment.TryGetValue(slot, out var existing))
                                    {
                                        existing.Id = id;
                                        existing.Name = UpdateString(existing.Name, p1);
                                        existing.Quantity = qty;
                                    }
                                    else
                                    {
                                        state.EnemyEquipment[slot] = new ItemSnapshot { Slot = slot, Id = id, Name = p1.ToString(), Quantity = qty };
                                    }
                                    if (slot == 3)
                                    {
                                        state.Player.EnemyWeapon = p1.ToString();
                                    }
                                }
                                else
                                {
                                    state.EnemyEquipment.TryRemove(slot, out _);
                                }
                            }
                            else if (p0.Equals("EMPTY", StringComparison.OrdinalIgnoreCase) || p0.SequenceEqual("0") || p0.SequenceEqual("-1") || p0.IsEmpty)
                            {
                                state.EnemyEquipment.TryRemove(slot, out _);
                            }
                        }
                        return;
                    }

                    if (prefix.Equals("NPC", StringComparison.OrdinalIgnoreCase))
                    {
                        var tokenizer = new SpanTokenizer(value.AsSpan());
                        if (tokenizer.TryGetNext(out var pId) && int.TryParse(pId, out int id) &&
                            tokenizer.TryGetNext(out var pName))
                        {
                            int cbLvl = 0;
                            int dst = 0;
                            int wx = 0;
                            int wy = 0;
                            int nAnim = -1;
                            ReadOnlySpan<char> pHealth = "100%".AsSpan();
                            ReadOnlySpan<char> pRole = "Enemy".AsSpan();
                            bool interacting = false;

                            if (tokenizer.TryGetNext(out var p3))
                            {
                                if (tokenizer.TryGetNext(out var p4))
                                {
                                    if (tokenizer.TryGetNext(out var p5))
                                    {
                                        if (tokenizer.TryGetNext(out var p6))
                                        {
                                            if (tokenizer.TryGetNext(out var p7))
                                            {
                                                if (tokenizer.TryGetNext(out var p8))
                                                {
                                                    if (tokenizer.TryGetNext(out var p9))
                                                    {
                                                        if (tokenizer.TryGetNext(out var p10))
                                                        {
                                                            // Standard PK format: <id>,<name>,<hp%>,<worldX>,<worldY>,<plane>,<dist>,<inCombat>,<anim>,<targetingMe>
                                                            pHealth = p3;
                                                            if (int.TryParse(p4, out int x)) wx = x;
                                                            if (int.TryParse(p5, out int y)) wy = y;
                                                            if (int.TryParse(p7, out int d)) dst = d;
                                                            if (int.TryParse(p9, out int a)) nAnim = a;
                                                            interacting = p10.SequenceEqual("1") || p10.Equals("True", StringComparison.OrdinalIgnoreCase);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            if (state.Npcs.TryGetValue(slot, out var existing))
                            {
                                existing.Id = id;
                                existing.Name = UpdateString(existing.Name, pName);
                                existing.CombatLevel = cbLvl;
                                existing.Distance = dst;
                                existing.WorldX = wx;
                                existing.WorldY = wy;
                                existing.Animation = nAnim;
                                existing.Health = UpdateString(existing.Health, pHealth);
                                existing.Role = UpdateString(existing.Role, pRole);
                                existing.IsInteractingWithMe = interacting;
                            }
                            else
                            {
                                state.Npcs[slot] = new NpcSnapshot
                                {
                                    Index = slot,
                                    Id = id,
                                    Name = pName.ToString(),
                                    CombatLevel = cbLvl,
                                    Distance = dst,
                                    WorldX = wx,
                                    WorldY = wy,
                                    Animation = nAnim,
                                    Health = pHealth.ToString(),
                                    Role = pRole.ToString(),
                                    IsInteractingWithMe = interacting
                                };
                            }
                        }
                        return;
                    }

                    if (prefix.Equals("NEARBY_PLAYER", StringComparison.OrdinalIgnoreCase) || prefix.Equals("PLAYER", StringComparison.OrdinalIgnoreCase))
                    {
                        var tokenizer = new SpanTokenizer(value.AsSpan());
                        if (tokenizer.TryGetNext(out var p1))
                        {
                            int pid = slot + 1;
                            ReadOnlySpan<char> pName = p1;
                            int pCb = 3;
                            int pDist = 0;

                            if (int.TryParse(p1, out int parsedPid))
                            {
                                // Format: <pid>,<name>,<dist>,<cbLvl>
                                pid = parsedPid;
                                if (tokenizer.TryGetNext(out var p2Name))
                                {
                                    pName = p2Name;
                                    if (tokenizer.TryGetNext(out var pD) && int.TryParse(pD, out int d)) pDist = d;
                                    if (tokenizer.TryGetNext(out var pC) && int.TryParse(pC, out int c)) pCb = c;
                                }
                            }
                            else if (tokenizer.TryGetNext(out var p2Cb))
                            {
                                // Format: <name>,<cbLvl>,<worldX>,<worldY>,<plane>,<dist>,<inCombat>,<anim>,<interacting>
                                pName = p1;
                                if (int.TryParse(p2Cb, out int c)) pCb = c;
                                if (tokenizer.TryGetNext(out var pX) &&
                                    tokenizer.TryGetNext(out var pY) &&
                                    tokenizer.TryGetNext(out var pPlane) &&
                                    tokenizer.TryGetNext(out var pD) && int.TryParse(pD, out int d))
                                {
                                    pDist = d;
                                }
                            }

                            if (state.NearbyPlayers.TryGetValue(slot, out var existing))
                            {
                                existing.Id = pid;
                                existing.Name = UpdateString(existing.Name, pName);
                                existing.Distance = pDist;
                                existing.CombatLevel = pCb;
                            }
                            else
                            {
                                state.NearbyPlayers[slot] = new NearbyPlayerSnapshot
                                {
                                    Index = slot,
                                    Id = pid,
                                    Name = pName.ToString(),
                                    Distance = pDist,
                                    CombatLevel = pCb
                                };
                            }
                        }
                        return;
                    }

                    if (prefix.Equals("TREE", StringComparison.OrdinalIgnoreCase) ||
                        prefix.Equals("BANK_OBJ", StringComparison.OrdinalIgnoreCase) ||
                        prefix.Equals("SHOP_OBJ", StringComparison.OrdinalIgnoreCase) ||
                        prefix.Equals("ALTAR_OBJ", StringComparison.OrdinalIgnoreCase) ||
                        prefix.Equals("ROCK_OBJ", StringComparison.OrdinalIgnoreCase) ||
                        prefix.Equals("SCENE_OBJECT", StringComparison.OrdinalIgnoreCase))
                    {
                        var tokenizer = new SpanTokenizer(value.AsSpan());
                        if (tokenizer.TryGetNext(out var pId) && int.TryParse(pId, out int id) &&
                            tokenizer.TryGetNext(out var pName))
                        {
                            string category = prefix.Equals("TREE", StringComparison.OrdinalIgnoreCase) ? "Tree" :
                                              prefix.Equals("BANK_OBJ", StringComparison.OrdinalIgnoreCase) ? "Bank" :
                                              prefix.Equals("SHOP_OBJ", StringComparison.OrdinalIgnoreCase) ? "Shop" :
                                              prefix.Equals("ALTAR_OBJ", StringComparison.OrdinalIgnoreCase) ? "Altar" :
                                              prefix.Equals("ROCK_OBJ", StringComparison.OrdinalIgnoreCase) ? "Rock" : "Object";
                            int dist = tokenizer.TryGetNext(out var pD) && int.TryParse(pD, out int d) ? d : 0;
                            int wx = tokenizer.TryGetNext(out var pX) && int.TryParse(pX, out int x) ? x : 0;
                            int wy = tokenizer.TryGetNext(out var pY) && int.TryParse(pY, out int y) ? y : 0;
                            ReadOnlySpan<char> pSt = prefix.Equals("TREE", StringComparison.OrdinalIgnoreCase) && tokenizer.TryGetNext(out var st) ? st : "Available".AsSpan();

                            int catPrefix = category switch
                            {
                                "Tree" => TreePrefix,
                                "Bank" => BankPrefix,
                                "Shop" => ShopPrefix,
                                "Altar" => AltarPrefix,
                                "Rock" => RockPrefix,
                                _ => (category.GetHashCode() & 0x7FFF) * 1000
                            };
                            int globalKey = catPrefix + slot;

                            if (state.Objects.TryGetValue(globalKey, out var existing))
                            {
                                existing.Id = id;
                                existing.Name = UpdateString(existing.Name, pName);
                                existing.Category = category;
                                existing.Status = UpdateString(existing.Status, pSt);
                                existing.Distance = dist;
                                existing.WorldX = wx;
                                existing.WorldY = wy;
                            }
                            else
                            {
                                state.Objects[globalKey] = new SceneObjectSnapshot
                                {
                                    Id = id,
                                    Name = pName.ToString(),
                                    Category = category,
                                    Status = pSt.ToString(),
                                    Distance = dist,
                                    WorldX = wx,
                                    WorldY = wy
                                };
                            }
                        }
                        return;
                    }

                    if (prefix.Equals("SHORTCUT", StringComparison.OrdinalIgnoreCase))
                    {
                        var tokenizer = new SpanTokenizer(value.AsSpan());
                        if (tokenizer.TryGetNext(out var pId) && int.TryParse(pId, out int id) &&
                            tokenizer.TryGetNext(out var pName) &&
                            tokenizer.TryGetNext(out var pReq))
                        {
                            int dist = tokenizer.TryGetNext(out var pD) && int.TryParse(pD, out int d) ? d : 0;
                            int wx = tokenizer.TryGetNext(out var pX) && int.TryParse(pX, out int x) ? x : 0;
                            int wy = tokenizer.TryGetNext(out var pY) && int.TryParse(pY, out int y) ? y : 0;

                            if (state.Shortcuts.TryGetValue(slot, out var existing))
                            {
                                existing.Id = id;
                                existing.Name = UpdateString(existing.Name, pName);
                                existing.Category = "Shortcut";
                                existing.RequiredLevel = UpdateString(existing.RequiredLevel, pReq);
                                existing.Distance = dist;
                                existing.WorldX = wx;
                                existing.WorldY = wy;
                            }
                            else
                            {
                                state.Shortcuts[slot] = new SceneObjectSnapshot
                                {
                                    Id = id,
                                    Name = pName.ToString(),
                                    Category = "Shortcut",
                                    RequiredLevel = pReq.ToString(),
                                    Distance = dist,
                                    WorldX = wx,
                                    WorldY = wy
                                };
                            }
                        }
                        return;
                    }

                    if (prefix.Equals("AGILITY_OBSTACLE", StringComparison.OrdinalIgnoreCase))
                    {
                        var tokenizer = new SpanTokenizer(value.AsSpan());
                        if (tokenizer.TryGetNext(out var pId) && int.TryParse(pId, out int id) &&
                            tokenizer.TryGetNext(out var pName) &&
                            tokenizer.TryGetNext(out var pStatus))
                        {
                            int dist = tokenizer.TryGetNext(out var pD) && int.TryParse(pD, out int d) ? d : 0;
                            int wx = tokenizer.TryGetNext(out var pX) && int.TryParse(pX, out int x) ? x : 0;
                            int wy = tokenizer.TryGetNext(out var pY) && int.TryParse(pY, out int y) ? y : 0;

                            if (state.AgilityObstacles.TryGetValue(slot, out var existing))
                            {
                                existing.Id = id;
                                existing.Name = UpdateString(existing.Name, pName);
                                existing.Category = "AgilityObstacle";
                                existing.Status = UpdateString(existing.Status, pStatus);
                                existing.Distance = dist;
                                existing.WorldX = wx;
                                existing.WorldY = wy;
                            }
                            else
                            {
                                state.AgilityObstacles[slot] = new SceneObjectSnapshot
                                {
                                    Id = id,
                                    Name = pName.ToString(),
                                    Category = "AgilityObstacle",
                                    Status = pStatus.ToString(),
                                    Distance = dist,
                                    WorldX = wx,
                                    WorldY = wy
                                };
                            }
                        }
                        return;
                    }

                    if (prefix.Equals("FISHING_SPOT", StringComparison.OrdinalIgnoreCase))
                    {
                        var tokenizer = new SpanTokenizer(value.AsSpan());
                        if (tokenizer.TryGetNext(out var pId) && int.TryParse(pId, out int id) &&
                            tokenizer.TryGetNext(out var pName) &&
                            tokenizer.TryGetNext(out var pType))
                        {
                            int dist = tokenizer.TryGetNext(out var pD) && int.TryParse(pD, out int d) ? d : 0;
                            int wx = tokenizer.TryGetNext(out var pX) && int.TryParse(pX, out int x) ? x : 0;
                            int wy = tokenizer.TryGetNext(out var pY) && int.TryParse(pY, out int y) ? y : 0;

                            if (state.FishingSpots.TryGetValue(slot, out var existing))
                            {
                                existing.Id = id;
                                existing.Name = UpdateString(existing.Name, pName);
                                existing.SpotType = UpdateString(existing.SpotType, pType);
                                existing.Distance = dist;
                                existing.WorldX = wx;
                                existing.WorldY = wy;
                            }
                            else
                            {
                                state.FishingSpots[slot] = new FishingSpotSnapshot
                                {
                                    Id = id,
                                    Name = pName.ToString(),
                                    SpotType = pType.ToString(),
                                    Distance = dist,
                                    WorldX = wx,
                                    WorldY = wy
                                };
                            }
                        }
                        return;
                    }

                    if (prefix.Equals("GROUND_ITEM", StringComparison.OrdinalIgnoreCase))
                    {
                        var tokenizer = new SpanTokenizer(value.AsSpan());
                        if (tokenizer.TryGetNext(out var pId) && int.TryParse(pId, out int id) &&
                            tokenizer.TryGetNext(out var pName))
                        {
                            int qty = tokenizer.TryGetNext(out var pQ) && int.TryParse(pQ, out int q) ? q : 1;
                            int dist = tokenizer.TryGetNext(out var pD) && int.TryParse(pD, out int d) ? d : 0;
                            int wx = tokenizer.TryGetNext(out var pX) && int.TryParse(pX, out int x) ? x : 0;
                            int wy = tokenizer.TryGetNext(out var pY) && int.TryParse(pY, out int y) ? y : 0;
                            string gKey = $"{id}_{wx}_{wy}";

                            if (state.GroundItems.TryGetValue(gKey, out var existing))
                            {
                                existing.Id = id;
                                existing.Name = UpdateString(existing.Name, pName);
                                existing.Quantity = qty;
                                existing.Distance = dist;
                                existing.WorldX = wx;
                                existing.WorldY = wy;
                            }
                            else
                            {
                                state.GroundItems[gKey] = new GroundItemSnapshot
                                {
                                    Id = id,
                                    Name = pName.ToString(),
                                    Quantity = qty,
                                    Distance = dist,
                                    WorldX = wx,
                                    WorldY = wy
                                };
                            }
                        }
                        return;
                    }

                    if (prefix.Equals("BANK_ITEM", StringComparison.OrdinalIgnoreCase) || prefix.Equals("BANK", StringComparison.OrdinalIgnoreCase))
                    {
                        ReadOnlySpan<char> span = value.AsSpan();
                        int comma1 = span.IndexOf(',');
                        if (comma1 != -1 && int.TryParse(span.Slice(0, comma1).Trim(), out int id))
                        {
                            var remainder = span.Slice(comma1 + 1);
                            int lastComma = remainder.LastIndexOf(',');
                            ReadOnlySpan<char> nameSpan = lastComma != -1 ? remainder.Slice(0, lastComma).Trim() : remainder.Trim();
                            int qty = lastComma != -1 && int.TryParse(remainder.Slice(lastComma + 1).Trim(), out int q) ? q : 1;
                            string nameStr = nameSpan.ToString();
                            ItemDatabase.RegisterItem(id, nameStr);

                            if (state.Bank.TryGetValue(slot, out var existing))
                            {
                                existing.Id = id;
                                existing.Name = UpdateString(existing.Name, nameSpan);
                                existing.Quantity = qty;
                            }
                            else
                            {
                                state.Bank[slot] = new ItemSnapshot { Slot = slot, Id = id, Name = nameStr, Quantity = qty };
                            }
                            state.IsBankOpen = true;
                        }
                        return;
                    }

                    if (prefix.Equals("SHOP_ITEM", StringComparison.OrdinalIgnoreCase) || prefix.Equals("SHOP", StringComparison.OrdinalIgnoreCase))
                    {
                        ReadOnlySpan<char> span = value.AsSpan();
                        int comma1 = span.IndexOf(',');
                        if (comma1 != -1 && int.TryParse(span.Slice(0, comma1).Trim(), out int id))
                        {
                            var remainder = span.Slice(comma1 + 1);
                            int lastComma = remainder.LastIndexOf(',');
                            ReadOnlySpan<char> nameSpan = lastComma != -1 ? remainder.Slice(0, lastComma).Trim() : remainder.Trim();
                            int qty = lastComma != -1 && int.TryParse(remainder.Slice(lastComma + 1).Trim(), out int q) ? q : 1;
                            string nameStr = nameSpan.ToString();
                            ItemDatabase.RegisterItem(id, nameStr);

                            if (state.Shop.TryGetValue(slot, out var existing))
                            {
                                existing.Id = id;
                                existing.Name = UpdateString(existing.Name, nameSpan);
                                existing.Quantity = qty;
                            }
                            else
                            {
                                state.Shop[slot] = new ItemSnapshot { Slot = slot, Id = id, Name = nameStr, Quantity = qty };
                            }
                            state.IsShopOpen = true;
                        }
                        return;
                    }

                    if (prefix.Equals("GE_SLOT", StringComparison.OrdinalIgnoreCase) || prefix.Equals("GE", StringComparison.OrdinalIgnoreCase))
                    {
                        var tokenizer = new SpanTokenizer(value.AsSpan());
                        if (tokenizer.TryGetNext(out var pState) &&
                            tokenizer.TryGetNext(out var pId) && int.TryParse(pId, out int itemId) &&
                            tokenizer.TryGetNext(out var pName) &&
                            tokenizer.TryGetNext(out var pPrice) && int.TryParse(pPrice, out int price) &&
                            tokenizer.TryGetNext(out var pTot) && int.TryParse(pTot, out int totQty) &&
                            tokenizer.TryGetNext(out var pTrans) && int.TryParse(pTrans, out int qtyTrans) &&
                            tokenizer.TryGetNext(out var pSpent) && int.TryParse(pSpent, out int spent))
                        {
                            if (state.GrandExchangeOffers.TryGetValue(slot, out var existing))
                            {
                                existing.Slot = slot;
                                existing.State = UpdateString(existing.State, pState);
                                existing.ItemId = itemId;
                                existing.ItemName = UpdateString(existing.ItemName, pName);
                                existing.Price = price;
                                existing.TotalQuantity = totQty;
                                existing.QuantityTransferred = qtyTrans;
                                existing.Spent = spent;
                            }
                            else
                            {
                                state.GrandExchangeOffers[slot] = new GrandExchangeOfferSnapshot
                                {
                                    Slot = slot,
                                    State = pState.ToString(),
                                    ItemId = itemId,
                                    ItemName = pName.ToString(),
                                    Price = price,
                                    TotalQuantity = totQty,
                                    QuantityTransferred = qtyTrans,
                                    Spent = spent
                                };
                            }
                        }
                        return;
                    }

                    if (prefix.Equals("RUNE_POUCH", StringComparison.OrdinalIgnoreCase))
                    {
                        var tokenizer = new SpanTokenizer(value.AsSpan());
                        if (tokenizer.TryGetNext(out var pId) && int.TryParse(pId, out int runeId) &&
                            tokenizer.TryGetNext(out var pName) &&
                            tokenizer.TryGetNext(out var pQty) && int.TryParse(pQty, out int qty))
                        {
                            if (state.RunePouch.TryGetValue(slot, out var existing))
                            {
                                existing.Slot = slot;
                                existing.RuneId = runeId;
                                existing.RuneName = UpdateString(existing.RuneName, pName);
                                existing.Quantity = qty;
                            }
                            else
                            {
                                state.RunePouch[slot] = new RunePouchSlotSnapshot
                                {
                                    Slot = slot,
                                    RuneId = runeId,
                                    RuneName = pName.ToString(),
                                    Quantity = qty
                                };
                            }
                        }
                        return;
                    }

                    if (prefix.Equals("LOOTING_BAG", StringComparison.OrdinalIgnoreCase))
                    {
                        var tokenizer = new SpanTokenizer(value.AsSpan());
                        if (tokenizer.TryGetNext(out var p0))
                        {
                            if (tokenizer.TryGetNext(out var p1))
                            {
                                if (tokenizer.TryGetNext(out var p2))
                                {
                                    if (int.TryParse(p0, out int id) && int.TryParse(p2, out int qty))
                                    {
                                        if (id <= 0 || qty <= 0)
                                        {
                                            state.LootingBag.TryRemove(slot, out _);
                                        }
                                        else if (state.LootingBag.TryGetValue(slot, out var existing))
                                        {
                                            existing.Id = id;
                                            existing.Name = UpdateString(existing.Name, p1);
                                            existing.Quantity = qty;
                                        }
                                        else
                                        {
                                            state.LootingBag[slot] = new ItemSnapshot { Slot = slot, Id = id, Name = p1.ToString(), Quantity = qty };
                                        }
                                    }
                                }
                            }
                            else if (p0.Equals("EMPTY", StringComparison.OrdinalIgnoreCase) || p0.SequenceEqual("0") || p0.SequenceEqual("-1") || p0.IsEmpty)
                            {
                                state.LootingBag.TryRemove(slot, out _);
                            }
                        }
                        return;
                    }

                    if (prefix.Equals("ATTACKING_ENEMY", StringComparison.OrdinalIgnoreCase))
                    {
                        var tokenizer = new SpanTokenizer(value.AsSpan());
                        if (tokenizer.TryGetNext(out var pName))
                        {
                            int enemyCb = tokenizer.TryGetNext(out var pCb) && int.TryParse(pCb, out int parsedEnemyCb) ? parsedEnemyCb : 0;
                            ReadOnlySpan<char> health = tokenizer.TryGetNext(out var ph) ? ph : "100%".AsSpan();
                            int dist = tokenizer.TryGetNext(out var pDist) && int.TryParse(pDist, out int parsedDist) ? parsedDist : 0;
                            ReadOnlySpan<char> prayer = tokenizer.TryGetNext(out var pPrayer) ? pPrayer : "None".AsSpan();
                            ReadOnlySpan<char> attackStyle = tokenizer.TryGetNext(out var pStyle) ? pStyle : "Melee".AsSpan();

                            if (state.AttackingEnemies.TryGetValue(slot, out var existing))
                            {
                                existing.Name = UpdateString(existing.Name, pName);
                                existing.CombatLevel = enemyCb;
                                existing.Health = UpdateString(existing.Health, health);
                                existing.Distance = dist;
                                existing.Prayer = UpdateString(existing.Prayer, prayer);
                                existing.AttackStyle = UpdateString(existing.AttackStyle, attackStyle);
                            }
                            else
                            {
                                state.AttackingEnemies[slot] = new AttackingEnemySnapshot
                                {
                                    Index = slot,
                                    Name = pName.ToString(),
                                    CombatLevel = enemyCb,
                                    Health = health.ToString(),
                                    Distance = dist,
                                    Prayer = prayer.ToString(),
                                    AttackStyle = attackStyle.ToString()
                                };
                            }
                        }
                        return;
                    }

                    if (prefix.Equals("VARBIT", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(value, out int vbVal)) state.Varbits[slot] = vbVal;
                        return;
                    }

                    if (prefix.Equals("VARP", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(value, out int vpVal)) state.Varps[slot] = vpVal;
                        return;
                    }

                    if (prefix.Equals("WIDGET", StringComparison.OrdinalIgnoreCase))
                    {
                        var tokenizer = new SpanTokenizer(value.AsSpan());
                        if (tokenizer.TryGetNext(out var pId) && int.TryParse(pId, out int wId))
                        {
                            int pGroup = (wId >> 16) & 0xFFFF;
                            int pChild = wId & 0xFFFF;
                            ReadOnlySpan<char> text = tokenizer.TryGetNext(out var pT) ? pT : ReadOnlySpan<char>.Empty;
                            bool hidden = tokenizer.TryGetNext(out var pH) && bool.TryParse(pH, out bool isHid) ? isHid : false;
                            int bx = tokenizer.TryGetNext(out var pBx) && int.TryParse(pBx, out int bxVal) ? bxVal : 0;
                            int by = tokenizer.TryGetNext(out var pBy) && int.TryParse(pBy, out int byVal) ? byVal : 0;
                            int bw = tokenizer.TryGetNext(out var pBw) && int.TryParse(pBw, out int bwVal) ? bwVal : 0;
                            int bh = tokenizer.TryGetNext(out var pBh) && int.TryParse(pBh, out int bhVal) ? bhVal : 0;
                            int itemId = tokenizer.TryGetNext(out var pIt) && int.TryParse(pIt, out int itm) ? itm : -1;
                            int itemQty = tokenizer.TryGetNext(out var pIq) && int.TryParse(pIq, out int itq) ? itq : 0;

                            if (state.Widgets.TryGetValue(wId, out var existing))
                            {
                                existing.GroupId = pGroup;
                                existing.ChildId = pChild;
                                existing.Text = UpdateString(existing.Text, text);
                                existing.IsHidden = hidden;
                                existing.BoundsX = bx;
                                existing.BoundsY = by;
                                existing.BoundsWidth = bw;
                                existing.BoundsHeight = bh;
                                existing.ItemId = itemId;
                                existing.ItemQuantity = itemQty;
                            }
                            else
                            {
                                state.Widgets[wId] = new WidgetSnapshot
                                {
                                    Id = wId,
                                    GroupId = pGroup,
                                    ChildId = pChild,
                                    Text = text.ToString(),
                                    IsHidden = hidden,
                                    BoundsX = bx,
                                    BoundsY = by,
                                    BoundsWidth = bw,
                                    BoundsHeight = bh,
                                    ItemId = itemId,
                                    ItemQuantity = itemQty
                                };
                            }
                        }
                        return;
                    }
                }
            }

            // Total Level & Total XP telemetry
            if (key.Equals("TOTAL_LEVEL", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(value, out int totalLvl))
                {
                    state.Player.TotalLevel = totalLvl;
                    Data.SkillTrackerEngine.Instance.TotalLevel = totalLvl;
                }
                return;
            }

            if (key.Equals("TOTAL_XP", StringComparison.OrdinalIgnoreCase))
            {
                if (long.TryParse(value, out long totalXp))
                {
                    state.Player.TotalExperience = totalXp;
                    Data.SkillTrackerEngine.Instance.TotalXp = totalXp;
                }
                return;
            }

            // Prayer telemetry: PRAYER[Thick Skin]: Active or Inactive
            if (key.StartsWith("PRAYER[", StringComparison.OrdinalIgnoreCase))
            {
                int closeBracket = key.IndexOf(']');
                if (closeBracket != -1)
                {
                    string prayerName = key.Substring(7, closeBracket - 7).Trim();
                    bool isActive = value.Equals("Active", StringComparison.OrdinalIgnoreCase) || 
                                   value.Equals("True", StringComparison.OrdinalIgnoreCase) || 
                                   value.Equals("1");
                    if (isActive)
                    {
                        if (!state.ActivePrayers.Active.Contains(prayerName))
                        {
                            state.ActivePrayers.Active.Add(prayerName);
                        }
                    }
                    else
                    {
                        state.ActivePrayers.Active.Remove(prayerName);
                    }
                }
                return;
            }

            // Skill XP telemetry: SKILL_XP[Attack]: 13034431
            if (key.StartsWith("SKILL_XP[", StringComparison.OrdinalIgnoreCase))
            {
                int closeBracket = key.IndexOf(']');
                if (closeBracket != -1)
                {
                    ReadOnlySpan<char> skillSpan = key.AsSpan(9, closeBracket - 9);
                    string skillName = GetCanonicalSkillName(skillSpan);
                    if (int.TryParse(value, out int xp))
                    {
                        if (state.Skills.TryGetValue(skillName, out var existing))
                        {
                            existing.Experience = xp;
                        }
                        else
                        {
                            state.Skills[skillName] = new SkillSnapshot { Experience = xp };
                        }
                        Data.SkillTrackerEngine.Instance.UpdateSkillXp(skillName, xp);
                    }
                }
                return;
            }

            // Skill Level telemetry: SKILL[Attack]: 99/99
            if (key.StartsWith("SKILL[", StringComparison.OrdinalIgnoreCase))
            {
                int closeBracket = key.IndexOf(']');
                if (closeBracket != -1)
                {
                    ReadOnlySpan<char> skillSpan = key.AsSpan(6, closeBracket - 6);
                    string skillName = GetCanonicalSkillName(skillSpan);
                    ReadOnlySpan<char> valSpan = value.AsSpan();
                    int slash = valSpan.IndexOf('/');
                    if (slash != -1)
                    {
                        var curSpan = valSpan.Slice(0, slash).Trim();
                        var maxSpan = valSpan.Slice(slash + 1).Trim();
                        if (int.TryParse(curSpan, out int cur) && int.TryParse(maxSpan, out int max))
                        {
                            if (state.Skills.TryGetValue(skillName, out var existing))
                            {
                                existing.BoostedLevel = cur;
                                existing.Level = max;
                            }
                            else
                            {
                                state.Skills[skillName] = new SkillSnapshot { BoostedLevel = cur, Level = max };
                            }
                            Data.SkillTrackerEngine.Instance.UpdateSkillLevels(skillName, cur, max);
                        }
                    }
                    else if (int.TryParse(valSpan, out int lvl))
                    {
                        if (state.Skills.TryGetValue(skillName, out var existing))
                        {
                            existing.BoostedLevel = lvl;
                            existing.Level = lvl;
                        }
                        else
                        {
                            state.Skills[skillName] = new SkillSnapshot { BoostedLevel = lvl, Level = lvl };
                        }
                        Data.SkillTrackerEngine.Instance.UpdateSkillLevels(skillName, lvl, lvl);
                    }
                }
                return;
            }

            // Legacy Skill telemetry: SKILL_Attack: 99/99 or 99/99/13034431
            if (key.StartsWith("SKILL_", StringComparison.OrdinalIgnoreCase))
            {
                ReadOnlySpan<char> skillSpan = key.AsSpan(6);
                string skillName = GetCanonicalSkillName(skillSpan);
                ReadOnlySpan<char> valSpan = value.AsSpan();
                int slash1 = valSpan.IndexOf('/');
                if (slash1 != -1)
                {
                    var curSpan = valSpan.Slice(0, slash1).Trim();
                    var remSpan = valSpan.Slice(slash1 + 1);
                    int slash2 = remSpan.IndexOf('/');
                    if (slash2 != -1)
                    {
                        var maxSpan = remSpan.Slice(0, slash2).Trim();
                        var xpSpan = remSpan.Slice(slash2 + 1).Trim();
                        if (int.TryParse(curSpan, out int cur) && int.TryParse(maxSpan, out int max))
                        {
                            int xp = int.TryParse(xpSpan, out int x) ? x : 0;
                            if (state.Skills.TryGetValue(skillName, out var existing))
                            {
                                existing.BoostedLevel = cur;
                                existing.Level = max;
                                existing.Experience = xp;
                            }
                            else
                            {
                                state.Skills[skillName] = new SkillSnapshot { BoostedLevel = cur, Level = max, Experience = xp };
                            }
                            Data.SkillTrackerEngine.Instance.UpdateSkillLevels(skillName, cur, max);
                            if (xp > 0) Data.SkillTrackerEngine.Instance.UpdateSkillXp(skillName, xp);
                        }
                    }
                    else
                    {
                        var maxSpan = remSpan.Trim();
                        if (int.TryParse(curSpan, out int cur) && int.TryParse(maxSpan, out int max))
                        {
                            if (state.Skills.TryGetValue(skillName, out var existing))
                            {
                                existing.BoostedLevel = cur;
                                existing.Level = max;
                            }
                            else
                            {
                                state.Skills[skillName] = new SkillSnapshot { BoostedLevel = cur, Level = max, Experience = 0 };
                            }
                            Data.SkillTrackerEngine.Instance.UpdateSkillLevels(skillName, cur, max);
                        }
                    }
                }
                return;
            }

            if (key.StartsWith("GE_OFFER_"))
            {
                if (int.TryParse(key.AsSpan(9), out int slot))
                {
                    var tokenizer = new SpanTokenizer(value.AsSpan());
                    if (tokenizer.TryGetNext(out var pState) &&
                        tokenizer.TryGetNext(out var pId) && int.TryParse(pId, out int itemId) &&
                        tokenizer.TryGetNext(out var pName) &&
                        tokenizer.TryGetNext(out var pPrice) && int.TryParse(pPrice, out int price) &&
                        tokenizer.TryGetNext(out var pTot) && int.TryParse(pTot, out int totQty) &&
                        tokenizer.TryGetNext(out var pTrans) && int.TryParse(pTrans, out int qtyTrans) &&
                        tokenizer.TryGetNext(out var pSpent) && int.TryParse(pSpent, out int spent))
                    {
                        state.GrandExchangeOffers[slot] = new GrandExchangeOfferSnapshot
                        {
                            Slot = slot,
                            State = pState.ToString(),
                            ItemId = itemId,
                            ItemName = pName.ToString(),
                            Price = price,
                            TotalQuantity = totQty,
                            QuantityTransferred = qtyTrans,
                            Spent = spent
                        };
                    }
                }
                return;
            }

            if (key.StartsWith("RUNE_POUCH_SLOT_"))
            {
                if (int.TryParse(key.AsSpan(16), out int slot))
                {
                    var tokenizer = new SpanTokenizer(value.AsSpan());
                    if (tokenizer.TryGetNext(out var pId) && int.TryParse(pId, out int runeId) &&
                        tokenizer.TryGetNext(out var pName) &&
                        tokenizer.TryGetNext(out var pQty) && int.TryParse(pQty, out int qty))
                    {
                        state.RunePouch[slot] = new RunePouchSlotSnapshot
                        {
                            Slot = slot,
                            RuneId = runeId,
                            RuneName = pName.ToString(),
                            Quantity = qty
                        };
                    }
                }
                return;
            }
        }

        private static string UpdateString(string? current, ReadOnlySpan<char> span)
        {
            if (current != null && span.SequenceEqual(current.AsSpan()))
            {
                return current;
            }
            return span.ToString();
        }

        private static string GetCanonicalSkillName(ReadOnlySpan<char> span)
        {
            if (span.Equals("ATTACK", StringComparison.OrdinalIgnoreCase)) return "Attack";
            if (span.Equals("DEFENCE", StringComparison.OrdinalIgnoreCase) || span.Equals("DEFENSE", StringComparison.OrdinalIgnoreCase)) return "Defence";
            if (span.Equals("STRENGTH", StringComparison.OrdinalIgnoreCase)) return "Strength";
            if (span.Equals("HITPOINTS", StringComparison.OrdinalIgnoreCase) || span.Equals("HP", StringComparison.OrdinalIgnoreCase)) return "Hitpoints";
            if (span.Equals("RANGED", StringComparison.OrdinalIgnoreCase) || span.Equals("RANGE", StringComparison.OrdinalIgnoreCase)) return "Ranged";
            if (span.Equals("PRAYER", StringComparison.OrdinalIgnoreCase)) return "Prayer";
            if (span.Equals("MAGIC", StringComparison.OrdinalIgnoreCase)) return "Magic";
            if (span.Equals("COOKING", StringComparison.OrdinalIgnoreCase)) return "Cooking";
            if (span.Equals("WOODCUTTING", StringComparison.OrdinalIgnoreCase)) return "Woodcutting";
            if (span.Equals("FLETCHING", StringComparison.OrdinalIgnoreCase)) return "Fletching";
            if (span.Equals("FISHING", StringComparison.OrdinalIgnoreCase)) return "Fishing";
            if (span.Equals("FIREMAKING", StringComparison.OrdinalIgnoreCase)) return "Firemaking";
            if (span.Equals("CRAFTING", StringComparison.OrdinalIgnoreCase)) return "Crafting";
            if (span.Equals("SMITHING", StringComparison.OrdinalIgnoreCase)) return "Smithing";
            if (span.Equals("MINING", StringComparison.OrdinalIgnoreCase)) return "Mining";
            if (span.Equals("HERBLORE", StringComparison.OrdinalIgnoreCase)) return "Herblore";
            if (span.Equals("AGILITY", StringComparison.OrdinalIgnoreCase)) return "Agility";
            if (span.Equals("THIEVING", StringComparison.OrdinalIgnoreCase)) return "Thieving";
            if (span.Equals("SLAYER", StringComparison.OrdinalIgnoreCase)) return "Slayer";
            if (span.Equals("FARMING", StringComparison.OrdinalIgnoreCase)) return "Farming";
            if (span.Equals("RUNECRAFT", StringComparison.OrdinalIgnoreCase) || span.Equals("RUNECRAFTING", StringComparison.OrdinalIgnoreCase)) return "Runecraft";
            if (span.Equals("HUNTER", StringComparison.OrdinalIgnoreCase)) return "Hunter";
            if (span.Equals("CONSTRUCTION", StringComparison.OrdinalIgnoreCase)) return "Construction";
            if (span.Equals("TOTAL", StringComparison.OrdinalIgnoreCase) || span.Equals("OVERALL", StringComparison.OrdinalIgnoreCase)) return "Overall";
            return span.ToString();
        }

        private ref struct SpanTokenizer
        {
            private ReadOnlySpan<char> _remaining;
            private readonly char _separator;

            public SpanTokenizer(ReadOnlySpan<char> span, char separator = ',')
            {
                _remaining = span;
                _separator = separator;
            }

            public bool TryGetNext(out ReadOnlySpan<char> segment)
            {
                if (_remaining.IsEmpty)
                {
                    segment = default;
                    return false;
                }

                int idx = _remaining.IndexOf(_separator);
                if (idx == -1)
                {
                    segment = _remaining.Trim();
                    _remaining = default;
                    return true;
                }

                segment = _remaining.Slice(0, idx).Trim();
                _remaining = _remaining.Slice(idx + 1);
                return true;
            }
        }

        private static int ExtractIndex(ReadOnlySpan<char> key)
        {
            int open = key.IndexOf('[');
            int close = key.IndexOf(']');
            if (open != -1 && close > open + 1 && int.TryParse(key.Slice(open + 1, close - open - 1), out int idx))
                return idx;
            return -1;
        }
    }
}
