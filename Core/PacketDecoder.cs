using System;
using System.Collections.Generic;
using System.Linq;

namespace OsrsMr.Core
{
    public static class PacketDecoder
    {
        public static void Decode(GameState state, string key, string value)
        {
            state.LastUpdated = DateTime.UtcNow;

            if (key == "PLAYER_NAME") state.Player.Name = value;
            else if (key == "COMBAT_LEVEL" && int.TryParse(value, out int cb)) state.Player.CombatLevel = cb;
            else if (key == "HP")
            {
                var parts = value.Split('/');
                if (parts.Length == 2 && int.TryParse(parts[0], out int cur) && int.TryParse(parts[1], out int max))
                {
                    state.Player.CurrentHp = cur;
                    state.Player.MaxHp = max;
                }
            }
            else if (key == "PRAYER")
            {
                var parts = value.Split('/');
                if (parts.Length == 2 && int.TryParse(parts[0], out int cur) && int.TryParse(parts[1], out int max))
                {
                    state.Player.CurrentPrayer = cur;
                    state.Player.MaxPrayer = max;
                }
            }
            else if (key == "RUN_ENERGY" && int.TryParse(value, out int energy)) state.Player.Energy = energy;
            else if (key == "WEIGHT" && int.TryParse(value, out int weight)) state.Player.Weight = weight;
            else if (key == "SPECIAL_ATTACK" && int.TryParse(value, out int spec)) state.Player.SpecPercent = spec;
            else if (key == "SPECIAL_ATTACK_ACTIVE") state.Player.IsSpecActive = value.Equals("True", StringComparison.OrdinalIgnoreCase);
            else if (key == "MAGIC_SPELLBOOK") state.Player.Spellbook = value;
            else if (key == "AUTOCAST_SPELL") state.Player.AutocastSpell = value;
            else if (key == "ACTIVE_TAB") state.Player.ActiveTab = value;
            else if (key == "ANIMATION" && int.TryParse(value, out int anim)) state.Player.Animation = anim;
            else if (key == "WORLD_LOCATION")
            {
                var parts = value.Split(',');
                if (parts.Length >= 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                {
                    state.Player.WorldX = x;
                    state.Player.WorldY = y;
                    if (parts.Length >= 3 && int.TryParse(parts[2], out int plane))
                        state.Player.Plane = plane;
                }
            }
            else if (key == "INTERACTING")
            {
                state.Player.IsInteracting = !string.IsNullOrEmpty(value) && value != "None";
                state.Player.InteractingName = value;
            }
            // Skills
            else if (key.StartsWith("SKILL_"))
            {
                string skillName = key.Substring(6);
                var parts = value.Split('/');
                if (parts.Length >= 2 && int.TryParse(parts[0], out int cur) && int.TryParse(parts[1], out int max))
                {
                    int xp = parts.Length >= 3 && int.TryParse(parts[2], out int x) ? x : 0;
                    state.Skills[skillName] = new SkillSnapshot { BoostedLevel = cur, Level = max, Experience = xp };
                }
            }
            // Inventory
            else if (key.StartsWith("INV[") || key.StartsWith("ITEM["))
            {
                int slot = ExtractIndex(key);
                if (slot != -1)
                {
                    var parts = value.Split(',');
                    if (parts.Length >= 3 && int.TryParse(parts[0], out int id) && int.TryParse(parts[2], out int qty))
                    {
                        state.Inventory[slot] = new ItemSnapshot { Slot = slot, Id = id, Name = parts[1].Trim(), Quantity = qty };
                    }
                    else if (parts.Length >= 2)
                    {
                        string name = parts[0].Trim();
                        int qty2 = int.TryParse(parts[1], out int q) ? q : 1;
                        int parsedId = int.TryParse(name, out int pid) ? pid : 0;
                        state.Inventory[slot] = new ItemSnapshot { Slot = slot, Id = parsedId, Name = name, Quantity = qty2 };
                    }
                }
            }
            else if (key == "INVENTORY_CLEAR")
            {
                state.Inventory.Clear();
            }
            // Equipment
            else if (key.StartsWith("EQUIP["))
            {
                int slot = ExtractIndex(key);
                if (slot != -1)
                {
                    var parts = value.Split(',');
                    if (parts.Length >= 3 && int.TryParse(parts[0], out int id) && int.TryParse(parts[2], out int qty))
                    {
                        state.Equipment[slot] = new ItemSnapshot { Slot = slot, Id = id, Name = parts[1].Trim(), Quantity = qty };
                    }
                    else if (parts.Length >= 2)
                    {
                        string name = parts[0].Trim();
                        int qty2 = int.TryParse(parts[1], out int q) ? q : 1;
                        int parsedId = int.TryParse(name, out int pid) ? pid : 0;
                        state.Equipment[slot] = new ItemSnapshot { Slot = slot, Id = parsedId, Name = name, Quantity = qty2 };
                    }
                }
            }
            // NPCs
            else if (key.StartsWith("NPC["))
            {
                int idx = ExtractIndex(key);
                if (idx != -1)
                {
                    // id,name,combatLvl,dist,worldX,worldY,anim,hp,maxHp,role
                    var parts = value.Split(',');
                    if (parts.Length >= 4 && int.TryParse(parts[0], out int id))
                    {
                        var npc = new NpcSnapshot
                        {
                            Index = idx,
                            Id = id,
                            Name = parts[1].Trim(),
                            CombatLevel = int.TryParse(parts[2], out int cbLvl) ? cbLvl : 0,
                            Distance = int.TryParse(parts[3], out int dst) ? dst : 0,
                            WorldX = parts.Length > 4 && int.TryParse(parts[4], out int wx) ? wx : 0,
                            WorldY = parts.Length > 5 && int.TryParse(parts[5], out int wy) ? wy : 0,
                            Animation = parts.Length > 6 && int.TryParse(parts[6], out int a) ? a : -1,
                            Role = parts.Length > 9 ? parts[9].Trim() : "NPC"
                        };
                        state.Npcs[idx] = npc;
                    }
                }
            }
            else if (key == "TOTAL_NPCS" && int.TryParse(value, out int totalNpcs))
            {
                var keysToRemove = state.Npcs.Keys.Where(k => k >= totalNpcs).ToList();
                foreach (var k in keysToRemove) state.Npcs.TryRemove(k, out _);
            }
            // Scene Objects (Trees, Banks, Rocks, Stores, Altars, etc.)
            else if (key.StartsWith("TREE[") || key.StartsWith("SCENE_OBJECT[") || key.StartsWith("BANK_OBJ[") || 
                     key.StartsWith("SHOP_OBJ[") || key.StartsWith("ALTAR_OBJ[") || key.StartsWith("ROCK_OBJ["))
            {
                int idx = ExtractIndex(key);
                if (idx != -1)
                {
                    var parts = value.Split(',');
                    if (parts.Length >= 4 && int.TryParse(parts[0], out int id))
                    {
                        string name = parts[1].Trim();
                        string category = key.StartsWith("TREE") ? "Tree" :
                                          key.StartsWith("BANK_OBJ") ? "Bank" :
                                          key.StartsWith("SHOP_OBJ") ? "Shop" :
                                          key.StartsWith("ALTAR_OBJ") ? "Altar" :
                                          key.StartsWith("ROCK_OBJ") ? "Rock" : "Object";
                        string status = key.StartsWith("TREE") && parts.Length >= 3 ? parts[2].Trim() : "Available";
                        int dist = key.StartsWith("TREE") && parts.Length >= 4 ? (int.TryParse(parts[3], out int d1) ? d1 : 0) :
                                   (int.TryParse(parts[2], out int d2) ? d2 : 0);
                        int wx = key.StartsWith("TREE") && parts.Length >= 5 ? (int.TryParse(parts[4], out int x1) ? x1 : 0) :
                                 (parts.Length >= 4 && int.TryParse(parts[3], out int x2) ? x2 : 0);
                        int wy = key.StartsWith("TREE") && parts.Length >= 6 ? (int.TryParse(parts[5], out int y1) ? y1 : 0) :
                                 (parts.Length >= 5 && int.TryParse(parts[4], out int y2) ? y2 : 0);

                        var obj = new SceneObjectSnapshot
                        {
                            Id = id,
                            Name = name,
                            Category = category,
                            Status = status,
                            Distance = dist,
                            WorldX = wx,
                            WorldY = wy
                        };
                        state.Objects[idx] = obj;
                    }
                }
            }
            // Shortcuts
            else if (key.StartsWith("SHORTCUT["))
            {
                int idx = ExtractIndex(key);
                if (idx != -1)
                {
                    var parts = value.Split(',');
                    if (parts.Length >= 6 && int.TryParse(parts[0], out int id))
                    {
                        state.Shortcuts[idx] = new SceneObjectSnapshot
                        {
                            Id = id,
                            Name = parts[1].Trim(),
                            Category = "Shortcut",
                            RequiredLevel = parts[2].Trim(),
                            Distance = int.TryParse(parts[3], out int d) ? d : 0,
                            WorldX = int.TryParse(parts[4], out int wx) ? wx : 0,
                            WorldY = int.TryParse(parts[5], out int wy) ? wy : 0
                        };
                    }
                }
            }
            // Agility Obstacles
            else if (key.StartsWith("AGILITY_OBSTACLE["))
            {
                int idx = ExtractIndex(key);
                if (idx != -1)
                {
                    var parts = value.Split(',');
                    if (parts.Length >= 6 && int.TryParse(parts[0], out int id))
                    {
                        state.AgilityObstacles[idx] = new SceneObjectSnapshot
                        {
                            Id = id,
                            Name = parts[1].Trim(),
                            Category = "AgilityObstacle",
                            Status = parts[2].Trim(), // Course Name
                            Distance = int.TryParse(parts[3], out int d) ? d : 0,
                            WorldX = int.TryParse(parts[4], out int wx) ? wx : 0,
                            WorldY = int.TryParse(parts[5], out int wy) ? wy : 0
                        };
                    }
                }
            }
            // Fishing Spots
            else if (key.StartsWith("FISHING_SPOT["))
            {
                int idx = ExtractIndex(key);
                if (idx != -1)
                {
                    var parts = value.Split(',');
                    if (parts.Length >= 6 && int.TryParse(parts[0], out int id))
                    {
                        state.FishingSpots[idx] = new FishingSpotSnapshot
                        {
                            Id = id,
                            Name = parts[1].Trim(),
                            SpotType = parts[2].Trim(),
                            Distance = int.TryParse(parts[3], out int d) ? d : 0,
                            WorldX = int.TryParse(parts[4], out int wx) ? wx : 0,
                            WorldY = int.TryParse(parts[5], out int wy) ? wy : 0
                        };
                    }
                }
            }
            // Ground Items
            else if (key.StartsWith("GROUND_ITEM["))
            {
                int idx = ExtractIndex(key);
                if (idx != -1)
                {
                    var parts = value.Split(',');
                    if (parts.Length >= 4 && int.TryParse(parts[0], out int id))
                    {
                        string name = parts[1].Trim();
                        int qty = int.TryParse(parts[2], out int q) ? q : 1;
                        int dist = int.TryParse(parts[3], out int d) ? d : 0;
                        int wx = parts.Length > 4 && int.TryParse(parts[4], out int x) ? x : 0;
                        int wy = parts.Length > 5 && int.TryParse(parts[5], out int y) ? y : 0;
                        state.GroundItems[$"{id}_{wx}_{wy}"] = new GroundItemSnapshot
                        {
                            Id = id,
                            Name = name,
                            Quantity = qty,
                            Distance = dist,
                            WorldX = wx,
                            WorldY = wy
                        };
                    }
                }
            }
            else if (key == "TOTAL_GROUND_ITEMS" && int.TryParse(value, out int totalGround))
            {
                if (totalGround == 0)
                {
                    state.GroundItems.Clear();
                }
            }
            // Bank & Shop Items
            else if (key.StartsWith("BANK_ITEM["))
            {
                int slot = ExtractIndex(key);
                if (slot != -1)
                {
                    var parts = value.Split(',');
                    if (parts.Length >= 3 && int.TryParse(parts[0], out int id) && int.TryParse(parts[2], out int qty))
                    {
                        state.Bank[slot] = new ItemSnapshot { Slot = slot, Id = id, Name = parts[1].Trim(), Quantity = qty };
                    }
                }
            }
            else if (key.StartsWith("SHOP_ITEM["))
            {
                int slot = ExtractIndex(key);
                if (slot != -1)
                {
                    var parts = value.Split(',');
                    if (parts.Length >= 3 && int.TryParse(parts[0], out int id) && int.TryParse(parts[2], out int qty))
                    {
                        state.Shop[slot] = new ItemSnapshot { Slot = slot, Id = id, Name = parts[1].Trim(), Quantity = qty };
                    }
                }
            }
            // Dialog
            else if (key == "DIALOG_OPEN") state.Dialog.IsOpen = value.Equals("True", StringComparison.OrdinalIgnoreCase);
            else if (key == "DIALOG_TYPE") state.Dialog.Type = value;
            else if (key == "DIALOG_SPEAKER") state.Dialog.Speaker = value;
            else if (key == "DIALOG_TEXT") state.Dialog.Text = value;
            else if (key == "DIALOG_OPTIONS")
            {
                state.Dialog.Options = value.Split(new[] { " | " }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }
            // Slayer
            else if (key == "SLAYER_TASK") state.Slayer.TaskName = value;
            else if (key == "SLAYER_REMAINING" && int.TryParse(value, out int rem)) state.Slayer.AmountRemaining = rem;
            else if (key == "SLAYER_MASTER") state.Slayer.Master = value;
            else if (key == "SLAYER_POINTS" && int.TryParse(value, out int pts)) state.Slayer.Points = pts;
            // Minigames
            else if (key == "MINIGAME_ACTIVE") state.Minigame.IsActive = value.Equals("True", StringComparison.OrdinalIgnoreCase);
            else if (key == "MINIGAME_NAME") state.Minigame.Name = value;
            else if (key == "MINIGAME_STATUS") state.Minigame.Status = value;
            else if (key == "MINIGAME_POINTS") state.Minigame.Points = value;
            else if (key == "MINIGAME_EXTRA") state.Minigame.Extra = value;
            // Agility
            else if (key == "AGILITY_COURSE") state.Agility.CurrentCourse = value;
            else if (key == "AGILITY_COURSE_LEVEL" && int.TryParse(value, out int lvl)) state.Agility.CourseLevelReq = lvl;
            else if (key == "MARKS_OF_GRACE_COUNT" && int.TryParse(value, out int mog)) state.Agility.MarksOfGraceNearby = mog;
            // Bank & Shop
            else if (key == "BANK_OPEN") state.IsBankOpen = value.Equals("True", StringComparison.OrdinalIgnoreCase);
            else if (key == "SHOP_OPEN") state.IsShopOpen = value.Equals("True", StringComparison.OrdinalIgnoreCase);
        }

        private static int ExtractIndex(string key)
        {
            int open = key.IndexOf('[');
            int close = key.IndexOf(']');
            if (open != -1 && close != -1 && int.TryParse(key.Substring(open + 1, close - open - 1), out int idx))
                return idx;
            return -1;
        }
    }
}
