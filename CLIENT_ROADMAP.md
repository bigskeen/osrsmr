# OSRS Client & Automation Engine Roadmap (RuneMate-Parity Architecture)

## 1. Executive Summary & Vision
Transform the `osrsmr` hybrid memory reader / agent into a full-featured, high-performance OSRS botting client with full entity detection, spatial math projection, an expressive LINQ Entity Query API, an event-driven scripting engine (Loop & Behavior Tree), humanized input simulation, and a modern WPF controller/overlay UI.

---

## 2. Architecture & System Milestones

### Milestone 1: Telemetry & Memory Parity (Agent & Packet Decoder)
- [x] **Ground Items & Stacks:** Extract item ID, quantity, world coordinates, and despawn ticks from `Tile.getGroundItems()`.
- [x] **Dynamic Widgets & Dialog Trees:** Crawl `client.getWidgetRoots()` and children; extract bounds `(x, y, w, h)`, visibility, actions, and text.
- [x] **Varps & Varbits:** Stream `client.getVarps()` and register key varbits (quest state, prayer state, spec energy, auto-retaliate).
- [x] **Projectiles & Graphics Objects:** Stream flying projectiles and spot animations (poison pools, falling rocks, hydra/olm attacks).
- [x] **Collision Matrix:** Extract 104x104 tile flags for local pathfinding.

### Milestone 2: 3D-to-2D Viewport & Spatial Math Engine
- [x] **Perspective Projection:** Port RuneLite's `Perspective.localToCanvas()` and camera matrix algorithms into C# (`Core/Spatial/Viewport.cs`).
- [x] **Tile & Model Polygon Bounds:** Generate 2D polygon screen bounds for tiles, NPCs, and scene objects.
- [x] **Click Geometry:** Point-in-polygon checks and randomized target point selection within visible entity bounds.
- [x] **A* Grid Pathfinder:** Local pathfinding over collision / coordinate space with waypoint generation (`Core/Spatial/Pathfinder.cs`).

### Milestone 3: RuneMate-Style Entity Query Engine (LINQ & Builder API)
- [x] **`EntityQuery<T>` Base:** Fluent builder supporting `.Named()`, `.WithinDistance()`, `.Filter()`, `.Nearest()`, `.SortedBy()`.
- [x] **Entity Query Implementations:**
  - `NpcQuery`: Filter by name, animation, combat level, interacting target.
  - `GameObjectQuery`: Filter by name, actions (`"Mine"`, `"Chop down"`, `"Bank"`), distance.
  - `GroundItemQuery`: Filter by name, high alch value, stack size, distance.
  - `InventoryQuery` / `BankQuery`: Filter by name, item ID, quantity, slot.
  - `WidgetQuery`: Filter by root/child ID, visible status, text, actions (`Core/Queries/WidgetQuery.cs`).

### Milestone 4: Scripting Framework & Lifecycle Engine
- [x] **Script Base Models:**
  - `LoopScript`: Periodic loop execution (`onStart()`, `onLoop() -> delayMs`, `onStop()`).
  - `TaskScript` / `TreeTask`: Priority task lists and behavior tree execution (`Validate()`, `Execute()`).
- [x] **Event Bus & Listeners:**
  - Strongly typed game events (`TickEvent`, `HitsplatEvent`, `InventoryChangedEvent`, `AnimationChangedEvent`, `ChatMessageEvent`).
- [x] **Script Manifest & Settings:**
  - `[ScriptManifest]` metadata attribute (Name, Author, Version, Description, Category).
  - Dynamic settings binding for script configuration dialogs.

### Milestone 5: Natural Human Simulation & Interaction
- [x] **WindMouse / Bézier Curve Generator:** Natural curved mouse movements with acceleration, deceleration, gravitational pull, and micro-jitters.
- [x] **Reaction Delays & Antiban:** Log-normal / Gaussian randomized delays, micro-breaks, and camera pitch/yaw rotations (`Core/Input/Antiban.cs`).
- [x] **Win32 Input Router:** Send `WM_MOUSEMOVE`, `WM_LBUTTONDOWN`, `WM_LBUTTONUP`, and `WM_KEYDOWN`/`WM_KEYUP` to client window handle (`hWnd`).
- [x] **Menu Interaction Engine:** Support left-click default actions and right-click -> menu option selection.

### Milestone 6: WPF Controller UI, Dashboard & Paint Overlay
- [x] **Bot Controller Panel:** Start, Pause, Stop, Reload bot scripts with script selector (Miner, Woodcutter, Combat Fighter) and real-time log viewer.
- [x] **Analytics Dashboard:** Runtime, XP/hour, gold/hour, items collected, active state/task.
- [x] **Transparent Paint Overlay:** Click-through DirectX / WPF canvas snapped over game window to render:
  - Entity bounds, pathfinding lines, destination markers, mouse trails, and script custom HUDs (`OverlayWindow.xaml`).
- [x] **Starter Script Catalog:** Comprehensive sample scripts covering Mining, Woodcutting, and Combat automation.
- [x] **Account & Profile Switcher:** Profile management, break scheduler, and auto-pause triggers (`Core/Profiles/`, `Core/Scripting/BreakHandler.cs`).

### Milestone 7: High-Level Game Interaction Framework & Controllers
- [x] **Entity Interaction API (`Core/Interaction/EntityInteractionExtensions.cs`):**
  - Declarative `.InteractAsync()`, `.ClickAsync()`, and `.TakeAsync()` on NPCs, Game Objects, Ground Items, Widgets, and Inventory slots via viewport projection.
- [x] **Condition & Synchronization Engine (`Core/Scripting/Condition.cs`):**
  - Polling condition wait primitives (`Condition.WaitAsync`, `Condition.Wait`, `Condition.WaitForPlayerIdleAsync`, `Condition.SleepAsync`).
- [x] **Banking Controller (`Core/Interaction/Bank.cs`):**
  - Automated detection, opening, deposit-all, deposit-except, and withdrawal routines.
- [x] **Inventory Manager (`Core/Interaction/InventoryActions.cs`):**
  - Full/empty checks, counting, fast dropping (`DropAllAsync`, `DropAllExceptAsync`), and item consumption.
- [x] **Movement & Navigation Controller (`Core/Interaction/Movement.cs`):**
  - World coordinate canvas click-walking, circular minimap compass navigation, path following, and run energy toggling.
- [x] **Camera Controller (`Core/Interaction/Camera.cs`):**
  - Directional rotation (`TurnToAsync(worldX, worldY)`, `TurnToAsync(npc/obj)`), yaw alignment, and pitch adjustments.
- [x] **Combat & Vitals Controller (`Core/Interaction/Combat.cs`):**
  - Auto-eat food, prayer restore drinking, special attack activation, quick prayers, and auto-retaliate toggling.

### Milestone 8: Dynamic Script Discovery & WebWalking Engine
- [x] **Dynamic Script Loader & Plugin Manager (`Core/Scripting/ScriptLoader.cs`):**
  - Runtime reflection and discovery of bot scripts from built-in assemblies and external `.dll` files dropped in the `Scripts/` folder.
  - Hot-reloading scripts without client restarts via the `🔄 Reload` UI button.
- [x] **Global WebWalking & Landmark Navigation (`Core/Spatial/WebWalker.cs`):**
  - Multi-tile global path slicing, stepping stone generation, and automatic navigation to major banks and cities (`WorldLocations.GrandExchange`, `VarrockWestBank`, `EdgevilleBank`, `LumbridgeCastle`, etc.).
  - Automated `WalkToNearestBankAsync()` routine.

### Milestone 9: Combat, Magic, Prayers, Equipment & Live Canvas Paint HUD
- [x] **Equipment Query & Controller (`Core/Queries/EquipmentQuery.cs`, `Core/Interaction/Equipment.cs`):**
  - Slot querying (Head, Cape, Amulet, Weapon, Body, Shield, Legs, Gloves, Boots, Ring, Ammo), equipment checks, equipping, and unequipping routines.
- [x] **Projectile & Spot Animation Engine (`Core/Queries/ProjectileQuery.cs`):**
  - Projectile tracking, target cycle monitoring, and player targeting detection for PvM and bossing scripts.
- [x] **Prayers & Protection System (`Core/Interaction/Prayers.cs`):**
  - Standard & overhead protection prayers activation, quick prayers toggling, and prayer points monitoring.
- [x] **Magic & Spellcasting Framework (`Core/Interaction/Magic.cs`):**
  - Combat spells, teleportation spells, High Alchemy, and targeted spell interactions on NPCs and items.
- [x] **Grand Exchange Trading Manager (`Core/Interaction/GrandExchange.cs`):**
  - Detection, opening, collecting all offers, and trading routines.
- [x] **Real-Time Script Canvas Paint (`Core/Scripting/BotScript.cs`, `OverlayWindow.xaml.cs`):**
  - `BotScript.OnPaint(DrawingContext dc)` integration hooked into the transparent HUD overlay for rendering real-time script metrics and visual indicators over the game window.

### Milestone 10: Dynamic Script Configuration Engine & Interactive Automation Modules
- [x] **Declarative Script Settings (`Core/Scripting/ScriptSettingAttribute.cs`):**
  - Annotated property configuration with labels, descriptions, default values, and ordering.
- [x] **Interactive Bot Controller Settings Panel (`MainWindow.xaml`, `MainWindow.xaml.cs`):**
  - Dynamic WPF UI generation for active script options (text inputs, checkboxes, numeric boxes) binding directly to script instances prior to execution.
- [x] **Dialog & Conversation Manager (`Core/Interaction/Dialogs.cs`):**
  - Automated detection of continue prompts, multi-choice selection by option text or numeric shortcut index, and automated dialog chain resolution.
- [x] **Expanded Starter Automation Suite (`Core/Scripting/StarterScripts.cs`):**
  - Added `Auto Fisher` (customizable interaction verbs, auto-dropping/banking) and `Auto High Alcher` (automated high alchemy with anti-ban variance).

### Milestone 11: Rich Script Customization, Enum & Dropdown Binding, and Random Event Handling
- [x] **Random Event NPC Engine (`Core/Interaction/RandomEvents.cs`):**
  - Automated detection of 30+ OSRS random event NPCs (Genie, Mysterious Old Man, Sandwich Lady, Dr Jekyll, Dunce, Postie Pete, Swarms, etc.) with customizable policies (`Dismiss`, `Ignore`, `RunAway`).
- [x] **Global Bank Destinations (`Core/Spatial/WebWalker.cs`):**
  - `BankLocation` enum and `WebWalker.WalkToBankAsync(BankLocation)` covering Nearest, Grand Exchange, Varrock West/East, Falador East/West, Edgeville, Draynor, Al Kharid, Seers' Village, Catherby, Ardougne South, and Lumbridge Castle.
- [x] **Dynamic WPF Settings UI Generation (`MainWindow.xaml.cs`):**
  - Full support for `Enum` properties and `string[] Options` yielding styled `ComboBox` dropdowns, booleans as `CheckBox`, numbers as validated `TextBox`, and strings as inputs.
- [x] **Fully Configured Starter Scripts (`Core/Scripting/StarterScripts.cs`):**
  - **Auto Miner:** Configurable Rock Type selector, Mining Method (DropWhenFull vs BankOres), Bank Destination, and Random Event handling policy.
  - **Auto Woodcutter:** Configurable Tree Type selector, Chopping Method (DropWhenFull vs BankLogs), Bank Destination, and Random Event handling.
  - **Auto Fighter:** Configurable Target NPC name, Eat-at-HP% threshold, Food Name, Special Attack toggle, and Random Event handling.
  - **Auto Fisher:** Configurable Fishing Action verb, Method (DropWhenFull vs BankFish), Bank Destination, and Random Event handling.
  - **Auto High Alcher:** Target Item name and Random Event handling.

### Milestone 12: Bank PIN Automation, Comprehensive Tool/Food Catalogs & Complete Minigames Suite
- [x] **Bank PIN Automation (`Core/Interaction/BankPin.cs`):**
  - Detection of Group 213 Bank PIN interface, shuffled digit button identification, and automated entering of profile Bank PINs with humanized delay curves.
  - Profile manager persistence and UI configuration for account Bank PINs.
- [x] **Comprehensive Food Catalog (`Core/Data/FoodCatalog.cs`):**
  - Metadata for all OSRS foods (Fish, Meats, Breads, Pies, Pizzas, Cakes, Potatoes, Stews, Saradomin brews, Combo foods) with heal amounts and smart consumption methods.
- [x] **Comprehensive Tool & Weapon Catalog (`Core/Data/ToolCatalog.cs`):**
  - Complete definitions for all Pickaxes (Bronze to 3rd Age, Crystal, Infernal, Trailblazer) and Woodcutting Axes with tier scores and requirement checks.
  - Full skilling tools and minigame equipment registry.
- [x] **Complete Minigame Automation Engine & Script Suite (`Core/Minigames/`, `Core/Scripting/MinigameScripts.cs`):**
  - **Wintertodt AIO:** Chops roots, fletches kindling, feeds braziers, fixes/lights braziers, dodges snowfall damage, heals pyromancers, and auto-eats food.
  - **Tempoross AIO:** Harpoon fishing, fish cooking, cannon ammunition loading, wave tethering, and spirit pool attacks.
  - **Guardians of the Rift AIO:** Huge remains mining, essence crafting at workbench, active elemental/catalytic portal entry, altar rune binding, and Great Guardian power charging.
  - **Pest Control AIO:** Novice/Intermediate/Veteran lander boarding, unshielded portal attacks, spinner clearing, and Void Knight defense.
  - **Nightmare Zone AIO:** Overload drinking, absorption management, 1 HP guzzling/prayer flicking, and power-up collection.
  - **Barrows AIO:** Mound digging, crypt searching, brother protection prayer routing, tunnel puzzle door solving, and chest looting.
  - **Blast Furnace, Tithe Farm, & Fishing Trawler Controllers:** Complete interaction routines for all major OSRS minigames.

### Milestone 13: Dedicated Pop-Out Script Setup & Configuration Window
- [x] **Pop-Out Setup UI (`ScriptConfigWindow.xaml`, `ScriptConfigWindow.xaml.cs`):**
  - Dedicated modal/dialog window popping out upon script selection or via the `⚙️ Setup Script` / `🗗 Pop-out Setup Window` toolbar buttons.
  - Dynamic dark-themed form generation with high-contrast dropdowns (`ComboBox`), text fields, checkboxes, and categorized settings groups.
  - Full script metadata header (Name, Version, Author, Category badge, Description).
  - Integrated setup action buttons: `▶ Start Bot Now`, `💾 Save & Apply`, `↺ Reset Defaults`, and `✖ Close`.

### Milestone 14: Skill Status, Experience Tracker & Time-To-Level (TTL) Engine
- [x] **Accurate OSRS Experience Engine (`Core/Data/ExperienceTable.cs`):**
  - Precalculated XP table for standard (1-99) and virtual (1-126) levels.
  - Level-for-XP and XP-for-level reverse lookups.
  - Calculation of XP remaining to next level and progress percentage (0-100%).
  - Time-To-Level (TTL) mathematical estimation based on current XP/hour.
- [x] **Skill & Session Tracking System (`Core/Data/SkillTracker.cs`):**
  - Live reactive MVVM tracking for all 23 canonical skills + Total / Overall.
  - Metrics: Current Level, Boosted Level, Current XP, Session XP Gained, XP/Hour, % Progress, and TTL.
  - Category classification (Combat, Gathering, Artisan, Support).
  - Reset session trigger and live periodic rate calculation.
- [x] **Skills & Experience UI Hub (`MainWindow.xaml`, `MainWindow.xaml.cs`):**
  - Rich skill cards with skill icons, boosted/real level indicators, next level targets.
  - Horizontal progress bars with percentage text and XP remaining badges.
  - Live session header with Total Level, Total XP, Session Gained, and Overall XP/hr.
  - Category filtering dropdown and one-click XP rates reset button.
- [x] **Transparent Overlay HUD Integration (`OverlayWindow.xaml`, `OverlayWindow.xaml.cs`):**
  - Real-time rendering of active training skills with level, XP gained, XP/hr rate, progress bar, XP remaining, and formatted TTL.

### Milestone 15: Active Buffs, Potion Timers & Status Effects Tracking System
- [x] **Deep Memory & Varbit Buff Extraction (`BytecodeAgent.java`):**
  - Live querying of Varbits and Varplayers for Stamina potion (Varbit 25), Antifire / Super Antifire (Varbits 3981, 6101), Overload for NMZ and CoX (Varbits 3955, 5418), Divine Potions (Varbits 8429-8433), Imbued Heart / Saturated Heart cooldown (Varbit 5440 / Varp 1243), Prayer Enhance (Varbit 5451), and Charge spell (Varbit 272).
  - Accurate Poison and Venom state decoding via Varp 102 with damage calculation and poison/venom immunity countdown tracking.
- [x] **Reactive Status Domain Model & Decoder (`Core/GameState.cs`, `Core/PacketDecoder.cs`):**
  - `StatusEffectsSnapshot` model with typed properties (`HasStamina`, `HasAntifire`, `HasOverload`, `HasDivine`, `IsImbuedHeartReady`, `HasPrayerEnhance`, `HasCharge`, `HasImmunity`) and formatted mm:ss tick duration calculations.
  - Span-based parser decoding `BUFF_*` and `POISON_*` telemetry streams.
- [x] **WPF Status Hub & Combat Dashboard (`MainWindow.xaml`, `MainWindow.xaml.cs`):**
  - Dedicated "Active Buffs & Status Timers" card grid under Combat & Slayer tab with live colored indicators and duration badges for all potions and status states.
- [x] **Transparent In-Game HUD Overlay (`OverlayWindow.xaml`, `OverlayWindow.xaml.cs`):**
  - Dynamic colored pill badges rendered directly over the RuneLite client window indicating active potion durations, heart cooldowns, and poison/venom alerts.

### Milestone 16: Grand Exchange Slot & Secondary Storage Containers Tracker
- [x] **Live Grand Exchange & Varbit Container Extraction (`BytecodeAgent.java`):**
  - Real-time extraction of all 8 Grand Exchange offer slots (`client.getGrandExchangeOffers()`) with item ID, item name, price, total quantity, transacted quantity, and spent gold.
  - Varbit & container tracking for Rune Pouch (4 slots, types, quantities), Gem Bag (Sapphires, Emeralds, Rubies, Diamonds, Dragonstones), Essence Pouches (Small, Medium, Large, Giant, Colossal), and Looting Bag container (516).
- [x] **Packet Decoding & Storage Domain Models (`Core/GameState.cs`, `Core/PacketDecoder.cs`):**
  - `GrandExchangeOfferSnapshot`, `RunePouchSlotSnapshot`, `GemBagSnapshot`, and `EssencePouchesSnapshot` state models with high-performance span tokenizing.
  - Indexed streaming parsers for `GE_SLOT[i]`, `RUNE_POUCH[i]`, `LOOTING_BAG[i]`, `GEM_BAG`, and `ESSENCE_POUCHES`.
- [x] **Interactive Grand Exchange & Containers Dashboard (`MainWindow.xaml`, `MainWindow.xaml.cs`):**
  - Dedicated Grand Exchange tab featuring interactive 8-slot card grid with color-coded status badges (Buying, Bought, Selling, Sold, Cancelled), item names, unit prices, transacted progress bars, and total gold transacted.
  - Dedicated Containers & Pouches tab displaying 4-slot Rune Pouch grid, Gem Bag counter with color-coded gem quantities, Essence Pouches status with total pure essence calculation, and live Looting Bag inventory list.

### Milestone 17: Production Skilling Automation Suite & High-Level Interaction Primitives
- [x] **Comprehensive Skilling Automation Suite (`Core/Scripting/SkillingScripts.cs`):**
  - **Auto Fletcher AIO:** Log cutting (Normal to Redwood), bow stringing (Shortbows & Longbows), dart & arrow tipping with automatic knife/feather banking and make-all dialog handling.
  - **Auto Cooker AIO:** Cooking on standard Ranges, Fires, or Rogues' Den Fire with burnt food tracking, bank webwalking, and success rate analytics.
  - **Auto Smelter & Cannonballer:** Ores to metal bars (Bronze to Runite) and steel bars to cannonballs (4x per bar) with automated ammo mould / coal ratio management at Edgeville, Al Kharid, and Falador furnaces.
  - **Auto Herblore & Cleaner:** Fast grimy herb cleaning, vial of water + clean herb unfinished potion mixing, and secondary ingredient finishing with rate metrics.
  - **Auto Rooftop Agility AIO:** Multi-course agility state machine (Gnome Stronghold, Draynor, Varrock, Canifis, Falador, Seers' Village) with automatic Mark of Grace ground item looting, low HP eating, and lap counting.
  - **Auto Pickpocket & Master Farmer:** Pickpocketing Master Farmers, Ardougne Knights, Guards, and Elves with stun animation recovery, coin pouch auto-opening, dodgy necklace auto-equipping, and health protection.
- [x] **Enhanced High-Level Controller Primitives:**
  - `InventoryActions.UseItemOnItemAsync()` & `InventoryActions.UseItemOnGameObjectAsync()` for skilling combining.
  - `Dialogs.IsMakeInterfaceOpen()` & `Dialogs.ConfirmMakeAllAsync()` for skilling menus.
  - `Combat.GetHealthPercent()` for dynamic threshold eating.
- [x] **Expanded Script Categories & UI Theme Badges:**
  - Integrated `ScriptCategory.Fletching`, `Herblore`, `Runecrafting`, and `Prayer` with custom category icons and themed badge color palettes across the bot catalog and pop-out configuration dialog.

### Milestone 18: Bossing & High-Level PvM Suite, Clue Scroll & Quest Engines
- [x] **Advanced PvM & Bossing Controller Engine (`Core/Interaction/CombatPvM.cs`, `Core/Bossing/`):**
  - **Multi-Item Fast Gear Swapping:** Rapid sequential equipping of specialized Magic, Ranged, and Melee gear sets (`CombatPvM.EquipGearSetAsync`).
  - **Offensive Combat Prayers:** Dynamic activation of Piety, Rigour, and Augury synchronized with weapon combat styles.
  - **Projectile-Reactive Defense:** Dynamic projectile detection for boss attacks (Zulrah, Vorkath, Jad) switching overhead prayers on incoming ticks.
  - **Special Attack Swapper:** Automated spec weapon swap, spec toggle, and primary weapon re-equip.
- [x] **Autonomous Boss Controllers:**
  - **Zulrah Controller (`Core/Bossing/ZulrahController.cs`):** Phase detection (Range Green, Magic Blue, Melee Red), toxic venom cloud avoidance, recoil maintenance, and venom curing.
  - **Vorkath Controller (`Core/Bossing/VorkathController.cs`):** Lethal fireball projectile evasion, acid pool safe tile stepping, and one-tick Zombified Spawn Crumble Undead cast.
  - **Giant Mole Controller (`Core/Bossing/GiantMoleController.cs`):** Dharok 1-HP guzzling, spade cavern digging, and stamina/prayer upkeep.
  - **Dagannoth Kings Controller (`Core/Bossing/DagannothKingsController.cs`):** Tribrid gear swapping and overhead prayer matching for Prime, Supreme, and Rex.
- [x] **Clue Scroll Solver & Quest Engine (`Core/Clues/`, `Core/Questing/`):**
  - `ClueScrollSolver`: Nautical degrees/minutes coordinate-to-world conversion against Observatory origin, emote clue gear verification, and spade digging.
  - `QuestHelperEngine`: Quest step registry, required item validation, and automated NPC conversation progression.
- [x] **Production Bossing, Clue & Quest Bot Suite (`Core/Scripting/BossingScripts.cs`, `Core/Scripting/QuestAndClueScripts.cs`):**
  - **Auto Zulrah AIO, Auto Vorkath AIO, Auto Giant Mole AIO, Auto Dagannoth Kings AIO, Auto Clue Solver AIO, and Auto Cook's Assistant.**
- [x] **Catalog & Category Integration:** Added `ScriptCategory.Bossing` and `ScriptCategory.Clues` with dedicated theme styling.

### Milestone 19: Wilderness & Slayer Engine, PK Evasion, Ground Loot Valuation & Script Studio IDE
- [x] **Wilderness & Player-Killer (PK) Safety System (`Core/Wilderness/WildernessManager.cs`):**
  - **Wilderness Level Math:** Precise calculation of wilderness level from world coordinates (`(worldY - 3520) / 8 + 1`) and underground dungeon depths.
  - **Combat Level Bracket Validation:** Threat filtering checking if surrounding players can attack or be attacked based on wilderness level.
  - **Hostile PK Threat Detection:** Scans nearby players for skulled indicators and combat weaponry (Whips, Godswords, Claws, Bows, Staves, Ballistas).
  - **Instant Emergency Escape:** Automated Level 30/20 Wilderness teleports (Royal seed pod, Amulet of glory, Ring of wealth, Teletabs) and emergency sprint south towards the Wilderness Ditch with Protect Item prayer.
  - **Reactive Defensive Overheads:** Projectile & gear-reactive prayer switching to mitigate damage and teleblock durations.
- [x] **Slayer Task Master & Gear Safety Engine (`Core/Slayer/SlayerManager.cs`):**
  - **Finishing Blow Items:** Automated finishing blows for Rockslugs (Bag of salt), Gargoyles (Rock hammer), Mutated Zygomites (Fungicide spray), and Desert Lizards (Ice coolers).
  - **Protective Gear Validation:** Automated verification of required protective gear (Slayer helmet, Earmuffs, Mirror shield, Nosepeg, Insulated boots, Spiny helmet).
- [x] **Ground Loot Valuation & Priority Manager (`Core/Data/LootManager.cs`, `Core/Interaction/Looting.cs`):**
  - **Comprehensive Valuation Catalog:** Estimated GP pricing database for bones, dragonhides, runes, grimy herbs, high-tier seeds, keys, and high-alch rune/dragon gear.
  - **Smart Ground Looting:** Configurable minimum GP value thresholds, guaranteed looting of rare/untradeable drops (Clue scrolls, Brimstone keys, Larran's keys, Champion scrolls), and automatic inventory space creation by eating food or dropping low-value junk.
- [x] **In-Client C# Script Studio & Code Editor (`ScriptStudioWindow.xaml`, `ScriptStudioWindow.xaml.cs`, `Core/Scripting/CustomScriptTemplates.cs`):**
  - Built-in Script Studio window with code editor, syntax templates (Basic LoopScript, Skilling & Gathering, Combat Fighter), line/char analytics, and live script catalog reload.
- [x] **Production Wilderness & Slayer Bot Scripts (`Core/Scripting/WildernessAndSlayerScripts.cs`):**
  - **Auto Slayer AIO:** Universal task battler with finishing blow execution, special attack chaining, food eating, prayer restoration, and ground loot valuation.
  - **Auto Wilderness Green Dragons:** West/East dragons killer with active antifire upkeep, PK threat evasion, and Edgeville banking.
  - **Auto Chaos Druids AIO:** Herb/rune/seed collector with auto-eating and banking.
- [x] **Script Category Badges:** Added `ScriptCategory.Slayer` (💀) and `ScriptCategory.Wilderness` (☠️) with customized theme palettes.
