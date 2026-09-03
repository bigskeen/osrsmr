# OSRS Bridge & Automation Engine — Complete Developer & Reverse Engineering Handbook
**Last Updated:** August 2026  
**Target Environment:** RuneLite / OSRS Modern Desktop Client (64-bit HotSpot JVM / Java 11+), Windows x64  
**Host Application:** C# .NET 9.0 WPF (`osrsmr`)  
**Repository Architecture:** Hybrid Java Bytecode Agent + C# High-Performance Automation & Telemetry Host  

---

## Table of Contents
1. [System Architecture & Data Flow](#1-system-architecture--data-flow)
2. [JVM Bytecode Agent & Reflection Engine](#2-jvm-bytecode-agent--reflection-engine)
   - 2.1 [Dynamic Attach Mechanism & ClassLoader Discovery](#21-dynamic-attach-mechanism--classloader-discovery)
   - 2.2 [Hierarchical Reflection Walker & Method Resolution](#22-hierarchical-reflection-walker--method-resolution)
   - 2.3 [Agent Lifecycle & Threading Model](#23-agent-lifecycle--threading-model)
3. [Reverse Engineering & Telemetry Extraction Pipeline](#3-reverse-engineering--telemetry-extraction-pipeline)
   - 3.1 [Player State, Coordinates & Vitals](#31-player-state-coordinates--vitals)
   - 3.2 [Combat State Detection & Sticky Decay Filter](#32-combat-state-detection--sticky-decay-filter)
   - 3.3 [Scene Graph Traversal (104x104 Matrix) & Entity Classification](#33-scene-graph-traversal-104x104-matrix--entity-classification)
   - 3.4 [World Objects & ID Resolution Hierarchy](#34-world-objects--id-resolution-hierarchy)
   - 3.5 [Container & Widget Scraping (Inventory, Bank, Shop, GE, Dialogs)](#35-container--widget-scraping-inventory-bank-shop-ge-dialogs)
4. [IPC Protocol & Comprehensive Packet Specification](#4-ipc-protocol--comprehensive-packet-specification)
   - 4.1 [Transport & Framing Architecture](#41-transport--framing-architecture)
   - 4.2 [Complete Telemetry Packet Dictionary](#42-complete-telemetry-packet-dictionary)
5. [C# .NET 9 Core Engine & State Management](#5-c-net-9-core-engine--state-management)
   - 5.1 [BrainEngine TCP Listener](#51-brainengine-tcp-listener)
   - 5.2 [High-Performance Span-Based PacketDecoder](#52-high-performance-span-based-packetdecoder)
   - 5.3 [Thread-Safe GameState Domain Model](#53-thread-safe-gamestate-domain-model)
6. [Human Input Simulation & Anti-Ban Physics](#6-human-input-simulation--anti-ban-physics)
   - 6.1 [Natural Bézier Curve Mouse Trajectories](#61-natural-bzier-curve-mouse-trajectories)
   - 6.2 [Gaussian & Log-Normal Reaction Models](#62-gaussian--log-normal-reaction-models)
   - 6.3 [Target Interaction Points & Micro-Overshoots](#63-target-interaction-points--micro-overshoots)
   - 6.4 [Keyboard, Camera & Widget Interaction](#64-keyboard-camera--widget-interaction)
7. [Scripting Framework & Visual Node Automation](#7-scripting-framework--visual-node-automation)
   - 7.1 [BotFramework & FSM Execution Model](#71-botframework--fsm-execution-model)
   - 7.2 [CustomScriptEngine & JSON Schema](#72-customscriptengine--json-schema)
   - 7.3 [Action & Condition Type Reference](#73-action--condition-type-reference)
   - 7.4 [Starter Bot Implementations](#74-starter-bot-implementations)
8. [Build, Packaging & Deployment Pipeline](#8-build-packaging--deployment-pipeline)
   - 8.1 [Prerequisites](#81-prerequisites)
   - 8.2 [Automated Java Agent Build](#82-automated-java-agent-build)
   - 8.3 [.NET 9 WPF Client Build](#83-net-9-wpf-client-build)
   - 8.4 [Single-Click Full Build Script](#84-single-click-full-build-script)
9. [Troubleshooting & Reverse Engineering Reference](#9-troubleshooting--reverse-engineering-reference)

---

## 1. System Architecture & Data Flow

The platform utilizes an **out-of-process architecture** designed for high throughput telemetry and maximum detection resistance. Rather than executing user bot code directly inside the game's JVM process (which creates injection vectors and heap footprints susceptible to heuristic detection), the system runs the entire decision engine and input generator in a native 64-bit Windows C# .NET 9 application. 

A lightweight, non-invasive Java Bytecode Agent is attached dynamically to RuneLite's JVM. The agent reads raw game memory structures via safe reflection, normalizes the data, and broadcasts an ASCII/UTF-8 telemetry stream over local TCP (`127.0.0.1:43594`) directly into the C# `BrainEngine`.

```
==================================================================================================
                                    SYSTEM ARCHITECTURE FLOW
==================================================================================================

 +---------------------------------------------------------------------------------------------+
 |                                RUNELITE / OSRS CLIENT (JVM)                                 |
 |                                                                                             |
 |   +-------------------------------------------------------------------------------------+   |
 |   |                             Java Agent (agent.jar)                                  |   |
 |   |                                                                                     |   |
 |   |   [Thread Group Discovery]       --> Locates RuneLite Client instance & Injector    |   |
 |   |   [Recursive Reflection Walker]  --> Resolves obfuscated/inherited methods & fields   |   |
 |   |   [High-Speed Telemetry Loop]    --> 150ms Player/Vitals/Skills/Combat Heartbeat       |   |
 |   |   [Scene Graph Scanner (Async)]  --> 1000ms 104x104 Tile Matrix (Chebyshev Sorting)    |   |
 |   |   [Container / Widget Crawler]   --> Bank, Shop, GE, Inventory, Equipment & Dialogs   |   |
 |   |                                                                                     |   |
 |   |   [TCP Streaming Server]         --> Direct Local Socket (127.0.0.1:43594)              |   |
 |   +-------------------------------------------------------------------------------------+   |
 +-----------------------------------------------|---------------------------------------------+
                                                 | Raw ASCII Telemetry Line Stream (UTF-8)
                                                 v
 +---------------------------------------------------------------------------------------------+
 |                                  C# .NET 9 HOST APPLICATION                                 |
 |                                                                                             |
 |   +-------------------------------------------------------------------------------------+   |
 |   |                 BrainEngine (Asynchronous TCP Stream Consumer)                      |   |
 |   +-------------------------------------------------------------------------------------+   |
 |                                               |
 |                                               v
 |   +-------------------------------------------------------------------------------------+   |
 |   |                 PacketDecoder (High-Performance Span-Based Parser)                  |   |
 |   +-------------------------------------------------------------------------------------+   |
 |                                               |
 |                                               v
 |   +-------------------------------------------------------------------------------------+   |
 |   |                     GameState (Thread-Safe Reactive Domain Model)                   |   |
 |   +-------------------------------------------------------------------------------------+   |
 |                                       |                       |
 |                   +-------------------+                       +-------------------+
 |                   v                                                               v
 |   +-------------------------------+                               +-------------------------------+
 |   |      WPF User Interface       |                               |      Bot & Decision Engine    |
 |   |  - Dashboard Badges           |                               |  - BotFramework / FSM State   |
 |   |  - Real-Time Inventory Visual |                               |  - CustomScriptEngine (JSON)  |
 |   |  - Mini-Radar Scene Map       |                               |  - Entity Query Engine (LINQ) |
 |   |  - Script Runner & Logs       |                               |  - HumanInput Bézier Physics  |
 |   +-------------------------------+                               +-------------------------------+
 +---------------------------------------------------------------------------------------------+
```

---

## 2. JVM Bytecode Agent & Reflection Engine

### 2.1 Dynamic Attach Mechanism & ClassLoader Discovery
The agent is designed to attach to an already running RuneLite or vanilla OSRS process via the standard JVM Attach API (`com.sun.tools.attach.VirtualMachine.attach(pid)`), implemented in `com.osrsmr.attach.AttachHelper`.

#### The ClassLoader Isolation Problem
RuneLite loads plugin classes and the game API via segregated child classloaders (`PluginClassLoader`, `RuneLiteClassLoader`). Calling standard `Class.forName("net.runelite.api.Client")` from the agent thread fails because the agent executes inside the system `AppClassLoader` which has no direct visibility into RuneLite's child class tree.

#### The Discovery Algorithm
`BytecodeAgent` resolves the active client instance dynamically by traversing the root thread group hierarchy:
1. Obtain the root `ThreadGroup` via `Thread.currentThread().getThreadGroup()`.
2. Enumerate all active threads and collect every distinct `ClassLoader` instance attached to running threads.
3. For each classloader, attempt to load `net.runelite.api.Client` or locate classes implementing the client interface.
4. If direct lookup is blocked by security managers, search the loaded classes and static field holders for the RuneLite `Injector` (Guice injector singleton) or active canvas parent components.
5. Cache the resolved `Client` instance in `private static volatile Object runeLiteClient`.

### 2.2 Hierarchical Reflection Walker & Method Resolution
RuneLite’s API and the underlying vanilla client employ extensive interface inheritance (e.g. `NPC` -> `Actor` -> `Renderable` -> `Node`). Standard Java reflection `Class.getMethod()` throws `NoSuchMethodException` when called on proxy classes or concrete implementations when the method signature was declared in a super-interface.

#### Recursive Lookup Implementation
`BytecodeAgent` implements recursive reflection lookup with multi-level caching via `ConcurrentHashMap<String, Method> METHOD_CACHE`:

```java
public static Method findMethod(Class<?> clazz, String name, Class<?>... paramTypes) {
    if (clazz == null) return null;
    String key = clazz.getName() + "#" + name;
    Method cached = METHOD_CACHE.get(key);
    if (cached != null) return cached == NULL_METHOD_MARKER ? null : cached;

    // 1. Check declared methods on current class
    try {
        Method m = clazz.getDeclaredMethod(name, paramTypes);
        m.setAccessible(true);
        METHOD_CACHE.put(key, m);
        return m;
    } catch (Throwable ignored) {}

    // 2. Check public methods (including superclasses)
    try {
        Method m = clazz.getMethod(name, paramTypes);
        m.setAccessible(true);
        METHOD_CACHE.put(key, m);
        return m;
    } catch (Throwable ignored) {}

    // 3. Recurse up inheritance hierarchy
    Method m = findMethod(clazz.getSuperclass(), name, paramTypes);
    if (m != null) {
        METHOD_CACHE.put(key, m);
        return m;
    }

    // 4. Recurse across all implemented interfaces
    for (Class<?> iface : clazz.getInterfaces()) {
        m = findMethod(iface, name, paramTypes);
        if (m != null) {
            METHOD_CACHE.put(key, m);
            return m;
        }
    }
    
    METHOD_CACHE.put(key, NULL_METHOD_MARKER);
    return null;
}
```

### 2.3 Agent Lifecycle & Threading Model
- **`agentmain(String args, Instrumentation inst)` / `premain`:** Entry points. Initializes singleton structures, allocates telemetry buffers, and spawns the background worker daemon.
- **Heartbeat Thread:** Executes on a strict **150ms tick cycle**. Gathers player coordinates, animation state, health/prayer/energy vitals, 24 skill levels/XP, equipped items, 28 inventory slots, and combat telemetry.
- **Scene Scan Worker:** Runs asynchronously every **1000ms** (or immediately if the player moves more than 4 tiles). Gathers and sorts all 104x104 world objects, trees, rocks, banks, shops, altars, agility obstacles, and ground items.
- **Auto-Reconnect & Server Socket:** Re-binds to `127.0.0.1:43594` on client disconnects without leaking socket descriptors or throwing uncaught thread termination exceptions.

---

## 3. Reverse Engineering & Telemetry Extraction Pipeline

### 3.1 Player State, Coordinates & Vitals
- **Local Player Reference:** `client.getLocalPlayer()`
- **Coordinates & Spatial Mapping:**
  - `WorldPoint` coordinates: `player.getWorldLocation()` -> `getX()`, `getY()`, `getPlane()`.
  - Local sub-tile coordinates: `player.getLocalLocation()` -> `getX()`, `getY()`.
  - Tile plane index (0 = ground, 1 = first floor, 2 = second floor, 3 = roof/dungeon).
- **Vitals & Boosts:**
  - `client.getBoostedSkillLevel(Skill.HITPOINTS)` & `client.getRealSkillLevel(Skill.HITPOINTS)`.
  - `client.getBoostedSkillLevel(Skill.PRAYER)` & `client.getRealSkillLevel(Skill.PRAYER)`.
  - `client.getEnergy()` (Run energy percentage 0–100).
  - `client.getWeight()` (Player total carried equipment weight in kg).
  - `client.getVarbitValue(300)` / `client.getVarpValue(300)` for special attack charge (0–1000 scaled to 0–100%).
- **Animation & Movement Tracking:**
  - `player.getAnimation()`: Current action animation ID (-1 when idle).
  - `player.getPoseAnimation()`: Stance animation ID (differentiates idle, walking, running).
  - Idle flag: Computed when `animation == -1` AND player world coordinates have not changed for >= 600ms.

### 3.2 Combat State Detection & Sticky Decay Filter
Detecting combat purely from `player.getAnimation()` or instantaneous target interactions results in rapid flickering because RuneLite's target pointers and combat animations clear during tick cooldowns.

#### The Sticky Decay Pipeline
1. **Direct Attacking Target:** Check `player.getInteracting()`. If an `Actor` (NPC or Player) is returned, mark `isAttacking = true` and update the active target details.
2. **Reverse Attacking Scanner (Under Attack):** Iterate through all active NPCs in the scene (`client.getNpcs()`). If any NPC has `npc.getInteracting() == localPlayer`, register that NPC in `attackingEnemiesList` and set `underAttack = true`.
3. **Sticky Decay Timer (5000ms Buffer):** When interactions momentarily clear (e.g. during weapon delay ticks or target retargeting), the agent maintains combat status for 5.0 seconds before returning to idle state.
4. **Target Telemetry Resolution:**
   - **Target Combat Level:** Extracted via `NPCComposition` (`getCombatLevel()`, `getTransformedComposition()`, `getDefinition()`).
   - **Target Health Bar Parsing:** Extracted by reading `Actor.getHealthRatio()` and `Actor.getHealthScale()`. If unavailable, scans `Actor.getHealthBars()` (`HealthBarUpdate`) and computes exact percentage: `(ratio * 100) / scale`.
   - **Target Chebyshev Distance:** Computed as `Math.max(Math.abs(targetX - playerX), Math.abs(targetY - playerY))`.
   - **Enemy Overhead Prayers & Styles:** Detected by inspecting head icon IDs (`getOverheadIcon()`) and attack animation IDs.

### 3.3 Scene Graph Traversal (104x104 Matrix) & Entity Classification
The client loads a local scene grid of 104x104 tiles. `BytecodeAgent` extracts all entities across planes 0 to 3:
- **`Scene.getTiles()[plane][x][y]` Traversal:**
  - **`Tile.getGameObjects()`**: Interactive world entities (trees, rocks, bank booths, altars, agility obstacles).
  - **`Tile.getWallObject()`**: Doors, gates, bank windows, boundary obstacles.
  - **`Tile.getDecorativeObject()`**: Wall decorations and boundary markers.
  - **`Tile.getGroundObject()`**: Floor traps, levers, ground levers, ladders.
  - **`Tile.getGroundItems()`**: Items dropped on the floor with item ID, stack quantity, and Chebyshev distance.

### 3.4 World Objects & ID Resolution Hierarchy
To ensure instant, 100% accurate classification of world objects without relying on slow network lookups:
1. **Composition Resolution:**
   - Call `obj.getComposition()` or `obj.getDefinition()`.
   - If not cached on object, invoke `client.getObjectDefinition(id)` or `client.loadObjectComposition(id)`.
   - Extract `getName()` and strip color tags (`<col=...>`).
2. **Word-Boundary Matching:** Uses strict boundary matching so words like `"street lamp"` or `"market"` do not falsely trigger `"tree"` or `"oak"`.
3. **Hardcoded Entity ID Lookup Tables:**
   - **Tree Types:** Normal (1276, 1278), Oak (1751, 10820), Willow (10819, 10829), Teak (9036, 15062), Maple (10831, 4677), Mahogany (9034, 40444), Yew (10822, 10823), Magic (10834, 10835), Redwood (29668, 29670), Juniper (28892), Blisterwood (39655).
   - **Bank Structures:** Bank Booths (10355, 24101, 28430, 27254, 34752), Deposit Boxes (10517, 26254, 25937), Bank Chests (782, 4484, 12308, 26707, 31674), GE Counters (10060, 10061).
   - **Altars & Pools:** Chaos Altar (409), Nature/Cosmic/Death Altars (2478–2488), Ornate Pools (29147–29150), Ferox Pool (39549).
   - **Mining Rocks:** Copper & Tin (11360–11365), Iron (11364–11367), Coal (11366–11369), Mithril (11370–11373), Adamantite (11374–11377), Runite (11378–11381), Gold & Silver (11380–11385), Amethyst (11388–11390).

### 3.5 Container & Widget Scraping (Inventory, Bank, Shop, GE, Dialogs)
1. **`ItemContainer` Direct Extraction:**
   - Resolves containers by index: `93` (Inventory), `94` (Equipment), `95` (Bank), `4` (Shop), `541` (Grand Exchange).
   - Resolves enum IDs from `net.runelite.api.InventoryID`.
   - Fallback: Scans `client.getItemContainers()` (`HashTable` / `NodeHashTable`).
   - Retrieves `Item.getId()` and `Item.getQuantity()`.
   - Resolves names via `client.getItemDefinition(itemId).getName()`.
2. **Deep Widget Tree Crawlers (Virtualized & Hidden Interfaces):**
   - **Bank Widget Groups:** `12`, `15`, `192`, `642`, `583`.
   - **Shop Widget Groups:** `300`, `301`, `423`.
   - **Grand Exchange Widget Groups:** `465`.
   - **Dialog Widget Groups:** `219`, `229`, `231`, `193`, `11`.
   - Recursively traverses `widget.getChildren()`, `widget.getDynamicChildren()`, and `widget.getNestedChildren()` to extract text, options, and item counts when containers are virtualized.

---

## 4. IPC Protocol & Comprehensive Packet Specification

### 4.1 Transport & Framing Architecture
- **Transport:** Local TCP Socket (`127.0.0.1:43594`).
- **Framing:** Plain ASCII/UTF-8 single-line strings terminated by newline `\n`.
- **Delimiter Convention:** `KEY: VALUE\n`
- **Sub-field Delimiters:** Comma `,` or Pipe `|` depending on payload complexity.

### 4.2 Complete Telemetry Packet Dictionary

| Packet Key | Example Payload | Field Breakdown & Description |
|---|---|---|
| `PLAYER_NAME` | `Zezima` | Current logged-in player display name. |
| `COMBAT_LEVEL` | `126` | Player combat level (3–126). |
| `TOTAL_LEVEL` | `2277` | Total overall skill level. |
| `QUEST_POINTS` | `300` | Completed quest points. |
| `HP` | `99/99` | `<current_hp>/<max_hp>` |
| `PRAYER` | `77/77` | `<current_prayer>/<max_prayer>` |
| `RUN_ENERGY` | `100` | Run energy integer (0–100). |
| `WEIGHT` | `14` | Total weight in kilograms. |
| `SPECIAL_ATTACK` | `100` | Special attack energy percentage (0–100). |
| `SPECIAL_ATTACK_ACTIVE`| `True` | Special attack toggle status (`True`/`False`). |
| `MAGIC_SPELLBOOK` | `Standard` | Active spellbook (`Standard`, `Ancient`, `Lunar`, `Arceuus`). |
| `AUTOCAST_SPELL` | `Ice Barrage` | Currently configured autocast spell name. |
| `ACTIVE_TAB` | `Inventory` | Currently open interface tab. |
| `ANIMATION` | `866` | Active action animation ID (`-1` = idle). |
| `POSE_ANIMATION` | `808` | Stance animation ID (idle, walking, running). |
| `WORLD_LOCATION` | `3205,3420,0` | `<worldX>,<worldY>,<plane>` |
| `LOCAL_LOCATION` | `6400,6400` | `<localFineX>,<localFineY>` sub-tile coordinates. |
| `PLANE` | `0` | Current height level index (0–3). |
| `IS_MOVING` | `False` | Player locomotion status (`True`/`False`). |
| `IS_IDLE` | `True` | Player idle state (`True`/`False`). |
| `COMBAT` | `IN_COMBAT: True \| TARGET: Man \| HP: 100% \| LEVEL: 2 \| UNDER_ATTACK: False` | Comprehensive combat summary payload. |
| `COMBAT_TARGET` | `Man` | Name of current target entity. |
| `COMBAT_TARGET_INDEX`| `142` | Server scene index of target entity. |
| `COMBAT_TARGET_LEVEL`| `2` | Target combat level. |
| `COMBAT_TARGET_HP` | `85%` | Target health percentage string. |
| `COMBAT_TARGET_DISTANCE`| `3` | Chebyshev distance to target tile. |
| `COMBAT_ENEMY_PRAYER`| `Protect from Melee` | Active overhead prayer icon on target. |
| `COMBAT_ENEMY_STYLE` | `Slash` | Detected attack animation style of target. |
| `COMBAT_UNDER_ATTACK`| `True` | Player is taking damage or targeted by an enemy. |
| `COMBAT_ATTACKING_ENEMIES`| `Guard (lvl 21), Guard (lvl 21)` | Comma-separated list of enemies targeting player. |
| `SKILL[0..23]` | `SKILL[0]: Attack,99,99,13034431` | `SKILL[<id>]: <name>,<boosted>,<real>,<xp>` |
| `INV[0..27]` | `INV[0]: 4151,Abyssal whip,1` | `INV[<slot>]: <itemId>,<name>,<quantity>` |
| `EQUIP[0..13]` | `EQUIP[3]: 4151,Abyssal whip` | `EQUIP[<slot>]: <itemId>,<name>` (Slot: 0=Head, 1=Cape, 2=Neck, 3=Weapon, 4=Torso, 5=Shield, 7=Legs, 9=Hands, 10=Feet, 12=Ring, 13=Ammo) |
| `NPC[idx]` | `NPC[0]: 3078,Man,100,3205,3420,0,5,True,866,False` | `<id>,<name>,<hp%>,<worldX>,<worldY>,<plane>,<dist>,<inCombat>,<anim>,<targetingMe>` |
| `GROUND_ITEM[idx]` | `GROUND_ITEM[0]: 526,Bones,1,3205,3420,0,4` | `<id>,<name>,<quantity>,<worldX>,<worldY>,<plane>,<dist>` |
| `TREE_OBJ[idx]` | `TREE_OBJ[0]: 10820,Oak tree,4,3208,3425` | `<id>,<name>,<dist>,<worldX>,<worldY>` |
| `BANK_OBJ[idx]` | `BANK_OBJ[0]: 10355,Bank booth,2,3210,3430` | `<id>,<name>,<dist>,<worldX>,<worldY>` |
| `SHOP_OBJ[idx]` | `SHOP_OBJ[0]: 301,General store,6,3215,3410` | `<id>,<name>,<dist>,<worldX>,<worldY>` |
| `ALTAR_OBJ[idx]` | `ALTAR_OBJ[0]: 409,Chaos altar,3,3220,3400` | `<id>,<name>,<dist>,<worldX>,<worldY>` |
| `ROCK_OBJ[idx]` | `ROCK_OBJ[0]: 11364,Iron rocks,5,3290,3370` | `<id>,<name>,<dist>,<worldX>,<worldY>` |
| `SHORTCUT_OBJ[idx]` | `SHORTCUT_OBJ[0]: 16509,Agility shortcut,3,3100,9900` | `<id>,<name>,<dist>,<worldX>,<worldY>` |
| `OBSTACLE_OBJ[idx]` | `OBSTACLE_OBJ[0]: 14843,Log balance,2,2474,3436` | `<id>,<name>,<dist>,<worldX>,<worldY>` |
| `BANK_OPEN` | `True` | Bank window open status (`True`/`False`). |
| `BANK_ITEM[idx]` | `BANK_ITEM[0]: 4151,Abyssal whip,5` | `<itemId>,<name>,<quantity>` |
| `SHOP_OPEN` | `True` | Shop interface open status (`True`/`False`). |
| `SHOP_ITEM[idx]` | `SHOP_ITEM[0]: 590,Tinderbox,10` | `<itemId>,<name>,<stockQuantity>` |
| `GE_OPEN` | `True` | Grand Exchange interface open status. |
| `GE_SLOT[0..7]` | `GE_SLOT[0]: BUY,4151,Abyssal whip,1,1800000,COMPLETED` | `<state>,<itemId>,<name>,<qty>,<price>,<status>` |
| `DIALOG_OPEN` | `True` | NPC or quest dialog open status. |
| `DIALOG_TEXT` | `Hello adventurer! Can you help me?` | Current dialog prompt text. |
| `DIALOG_OPTIONS` | `Yes, absolutely!\|No, I'm busy.` | Pipe-delimited list of selectable dialog options. |
| `GAME_TICK` | `14250` | Exact monotonic game tick count (`client.getTickCount()`). |
| `CAMERA_PITCH` | `383` | Camera pitch angle (128 to 383). |
| `CAMERA_YAW` | `1024` | Camera yaw rotation (0 to 2047). |
| `CAMERA_ZOOM` | `512` | Camera zoom factor / scale. |
| `CAMERA_SCALE` | `512` | Viewport camera scale. |
| `CAMERA_POS` | `3205,3420,1200` | `<cameraX>,<cameraY>,<cameraZ>` absolute 3D camera vector. |
| `CANVAS_SIZE` | `800,600` | `<width>,<height>` client window canvas dimensions. |
| `VIEWPORT_BOUNDS`| `765,503,4,4` | `<viewportWidth>,<viewportHeight>,<offsetX>,<offsetY>` rendering viewport bounds. |
| `STATUS_POISON` | `true,6` | `<isPoisoned>,<damage>` active poison status. |
| `STATUS_VENOM` | `true,12` | `<isVenom>,<damage>` active venom status (escalating damage). |
| `STATUS_ANTIFIRE` | `150,true` | `<ticksRemaining>,<isSuperAntifire>` antifire potion timer. |
| `STATUS_STAMINA` | `100` | Remaining stamina potion duration in ticks. |
| `STATUS_IMMUNITY_VENOM`| `200` | Remaining venom/poison immunity duration in ticks. |
| `AUTO_RETALIATE` | `True` | Auto-retaliate combat setting state (`True`/`False`). |
| `RUN_MODE` | `True` | Run energy movement mode toggle (`True`/`False`). |
| `ACTIVE_PRAYERS` | `Protect from Melee,Piety` | Comma-separated list of currently activated standard/offensive prayers. |
| `GE_OFFER_[0..7]`| `Buying,4151,Abyssal whip,1800000,1,0,0` | `<state>,<itemId>,<itemName>,<price>,<totalQty>,<transferredQty>,<spent>` per slot. |
| `PROJECTILE` | `100,25,26,28,29,14,0,12,65` | `<id>,<startX>,<startY>,<targetX>,<targetY>,<targetIndex>,<plane>,<remainingCycles>,<endCycle>`. |
| `GRAPHICS_OBJECT`| `120,3205,3420,0,15,0` | `<id>,<worldX>,<worldY>,<plane>,<startCycle>,<level>` (SpotAnimations / Floor hazards). |
| `RUNE_POUCH_SLOT_[0..3]`| `8,Blood Rune,500` | `<runeId>,<runeName>,<quantity>` slotted in standard / divine rune pouch. |
| `EQUIPMENT_BONUSES`| `10,20,5,0,0,15,25,10,0,0,45,0,0,5` | `<atkStab>,<atkSlash>,<atkCrush>,<atkMagic>,<atkRange>,<defStab>,<defSlash>,<defCrush>,<defMagic>,<defRange>,<meleeStr>,<rangedStr>,<magicDmg>,<prayer>`. |
| `MENU_ENTRY` | `0,Attack,Guard (level-21),14,9,0,0` | `<index>,<option>,<target>,<identifier>,<opcode>,<param0>,<param1>` from hover/context menu. |

---

## 5. C# .NET 9 Core Engine & State Management

### 5.1 BrainEngine TCP Listener
`BrainEngine` acts as the primary singleton network coordinator. It opens a non-blocking `TcpListener` on `127.0.0.1:43594` and manages stream reading via `Task.Run` worker loops with full `CancellationTokenSource` teardown semantics.

```csharp
public void ProcessLine(string line)
{
    if (string.IsNullOrWhiteSpace(line)) return;
    int colonIdx = line.IndexOf(':');
    if (colonIdx == -1) return;

    ReadOnlySpan<char> span = line.AsSpan();
    ReadOnlySpan<char> keySpan = span.Slice(0, colonIdx).Trim();
    ReadOnlySpan<char> valSpan = span.Slice(colonIdx + 1).Trim();

    string key = keySpan.ToString();
    string value = valSpan.ToString();

    OnRawPacketReceived?.Invoke(key, value);
    PacketDecoder.Decode(State, key, value);
    OnStateUpdated?.Invoke();
}
```

### 5.2 High-Performance Span-Based PacketDecoder
To guarantee zero-allocation high throughput during 150ms packet bursts, `PacketDecoder.cs` uses `ReadOnlySpan<char>`, `stackalloc`, and fast integer parsers (`int.TryParse(ReadOnlySpan<char>, ...)`). It routes telemetry into thread-safe collections:
- **Spatial Object Hashing:** Indexed objects (Trees, Rocks, Banks, Altars) generate persistent, collision-free hashes based on entity class tags and world coordinates.
- **Array Resizing & Slot Re-use:** Inventory (28 slots), Equipment (14 slots), and Skills (24 slots) use fixed-size pre-allocated arrays to eliminate GC pressure.

### 5.3 Thread-Safe GameState Domain Model
`GameState` provides reactive, thread-safe models representing the entire game context:
- `PlayerState`: Coordinates, health, prayer, energy, animations, special attack, active tabs.
- `SkillState[]`: Real/Boosted levels, current XP, XP gained this session, and time-to-level calculations.
- `InventoryItem[]`: 28 slots containing ID, Name, Quantity, and UI bounding boxes.
- `EquipmentItem[]`: 14 equipment gear slots with stats and equipment IDs.
- `ObservableCollection<NpcEntity>`: Live NPCs within the 104x104 scene.
- `ObservableCollection<WorldObjectEntity>`: Live classified objects (Trees, Banks, Altars, Rocks, Obstacles).
- `ObservableCollection<GroundItemEntity>`: Dropped loot and ground spawns.
- `BankState`, `ShopState`, `GeState`, `DialogState`: Live interface tracking.

---

## 6. Human Input Simulation & Anti-Ban Physics

Bot detection on modern OSRS relies heavily on statistical mouse movement analysis (heuristic jerk, acceleration curvature, standard deviation of micro-pauses, and click-down hold times). The `HumanInput` engine completely bypasses naive linear cursor jumping (`SetCursorPos`) in favor of biomimetic physics.

### 6.1 Natural Bézier Curve Mouse Trajectories
Mouse paths are generated using **Cubic Bézier Splines** with randomized control points and dynamic velocity profiling:

$$B(t) = (1-t)^3 P_0 + 3(1-t)^2 t P_1 + 3(1-t) t^2 P_2 + t^3 P_3, \quad t \in [0, 1]$$

- **Control Point Generation:** $P_1$ and $P_2$ are perturbed perpendicularly to the trajectory vector with random curvature factors proportional to distance.
- **WindMouse Physics:** Incorporates gravity pull towards the target and randomized wind resistance to simulate physical hand tremors and micro-corrections.
- **Micro-Overshoot Simulation:** On long-distance cursor travels (>300px), there is a 20–35% probability of overshooting the target bounds by 2–8 pixels, followed by a human-like 40–90ms corrective trajectory.

### 6.2 Gaussian & Log-Normal Reaction Models
Human reaction times do not follow uniform distributions. `HumanInput` generates delays using **Box-Muller Gaussian Transforms** and **Log-Normal Distributions**:

```csharp
public static int NextGaussian(int mean, int stdDev, int min = 10, int max = 10000)
{
    var rand = Random.Shared;
    double u1 = 1.0 - rand.NextDouble();
    double u2 = 1.0 - rand.NextDouble();
    double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    double randNormal = mean + stdDev * randStdNormal;
    return Math.Clamp((int)randNormal, min, max);
}

public static int NextLogNormal(int medianMs, double shape = 0.35, int min = 15, int max = 15000)
{
    var rand = Random.Shared;
    double u1 = 1.0 - rand.NextDouble();
    double u2 = 1.0 - rand.NextDouble();
    double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    double mu = Math.Log(Math.Max(1, medianMs));
    double value = Math.Exp(mu + shape * randStdNormal);
    return Math.Clamp((int)value, min, max);
}
```

- **Click Hold Duration:** Gaussian distribution with mean = 75ms, stdDev = 15ms (clamped between 35ms and 160ms).
- **Menu Hover Delays:** Log-normal distribution simulating visual recognition delay before clicking sub-menus.

### 6.3 Target Interaction Points & Micro-Overshoots
Instead of clicking the dead center of entity bounding boxes or inventory slots, `GetGaussianInteractionPoint` selects an interaction coordinate using a 2D Gaussian distribution centered on the box with $\sigma = \text{width} \times 0.22$, strictly clamped within the interactable bounds.

### 6.4 Keyboard, Camera & Widget Interaction
- **Camera Rotation:** Middle-mouse button dragging using randomized angular velocities.
- **Typing Engine:** Simulates human typing cadence with inter-key delays (50–180ms), occasional simulated typos, and backspace corrections.
- **Micro-Breaks:** Automatic background timer injecting 2–15 second cognitive pauses after prolonged repetitive tasks.

---

## 7. Scripting Framework & Visual Node Automation

### 7.1 BotFramework & FSM Execution Model
`BotFramework` provides an asynchronous Finite State Machine (FSM) execution model:
- `FsmBot`: Base class maintaining state transitions, execution loop, failure recovery, and diagnostic logging.
- `EntityQueries`: Fluent LINQ-like queries for finding entities:
  - `BotApi.Npcs.Where(n => n.Name == "Goblin" && !n.InCombat).OrderByDistance().FirstOrDefault()`
  - `BotApi.Objects.Where(o => o.Name.Contains("Tree")).OrderByDistance().FirstOrDefault()`
  - `BotApi.Inventory.GetCount("Iron ore")`
  - `BotApi.GroundItems.Where(g => g.Name == "Rune scimitar").FirstOrDefault()`

### 7.2 CustomScriptEngine & JSON Schema
`CustomScriptEngine` executes visual, node-based automation scripts loaded from JSON definitions stored in `custom_scripts/`.

#### Script JSON Schema Example:
```json
{
  "name": "Visual Auto Fighter",
  "description": "Attacks configured NPCs, manages food, special attacks, and loots items.",
  "loopDelayMs": 600,
  "nodes": [
    {
      "nodeName": "Eat Food Check",
      "condition": {
        "type": "HpBelowPercent",
        "intParam": 50
      },
      "action": {
        "type": "EatFood",
        "stringParam": "Lobster"
      }
    },
    {
      "nodeName": "Attack Target",
      "condition": {
        "type": "PlayerNotInCombat"
      },
      "action": {
        "type": "AttackNpc",
        "stringParam": "Guard"
      }
    }
  ]
}
```

### 7.3 Action & Condition Type Reference

#### Supported Custom Actions (65+ Types):
- **Combat & Vitals:** `AttackNpc`, `EatFood`, `DrinkPotion`, `DrinkStamina`, `DrinkAntiPoison`, `DrinkAntiVenom`, `DrinkAntifire`, `DrinkPrayerPotion`, `DrinkBoostPotion`, `TogglePrayer`, `ActivateQuickPrayers`, `DeactivatePrayers`, `FlickPrayer`, `ToggleSpecialAttack`, `ToggleAutoRetaliate`, `DodgeHazard`, `SwitchGearSet`.
- **Resource Gathering:** `ChopObject`, `MineObject`, `ClickObject`, `RunAgilityObstacle`, `CleanHerb`, `FletchItem`, `CookFood`, `SmeltOre`, `AlchItem`.
- **Inventory & Items:** `DropItem`, `DropAllOfItem`, `DropAllExcept`, `EquipItem`, `UnequipItem`, `UseItemOnItem`, `CastSpellOnItem`, `LootGroundItem`, `LootAllConfigured`.
- **Banking & Shops:** `OpenNearestBank`, `BankDepositAll`, `BankDepositAllExcept`, `BankDepositEquipment`, `BankWithdrawItem`, `BankWithdrawAll`, `CloseBank`, `OpenShop`, `ShopBuyItem`, `ShopSellItem`, `CloseShop`.
- **Grand Exchange:** `OpenGrandExchange`, `GEBuyItem`, `GESellItem`, `GECollectAll`, `GEAbortOffer`, `CloseGrandExchange`.
- **Dialog & Navigation:** `ContinueDialog`, `SelectDialogOption`, `TypeDialogText`, `WaitForDialog`, `WalkToCoords`, `WalkToBank`, `ToggleRun`, `EnterMinigamePortal`.
- **Anti-Ban Utilities:** `AntiBanCognitiveDelay`, `AntiBanCheckMicroBreak`, `AntiBanIdleWander`, `WaitSeconds`, `WaitForIdle`.

#### Supported Custom Conditions (35+ Types):
- `Always`, `InventoryFull`, `InventoryNotFull`, `InventoryHasItem`, `InventoryDoesNotHaveItem`, `InventoryCountLessThan`, `InventoryCountGreaterThan`.
- `PlayerIsIdle`, `PlayerIsNotIdle`, `PlayerInCombat`, `PlayerNotInCombat`, `TargetInCombat`.
- `HpBelowPercent`, `PrayerBelow`, `SpecialAttackAbove`, `RunEnergyBelow`.
- `Poisoned`, `Envenomed`, `AntifireExpired`, `StaminaExpired`, `PrayerIsActive`, `PrayerIsNotActive`.
- `HazardNearby`, `ProjectileIncoming`, `AutoRetaliateDisabled`, `RunDisabled`, `RunePouchHas`, `GeSlotFinished`.
- `BankIsOpen`, `BankIsClosed`, `ShopIsOpen`, `ShopIsClosed`, `GeIsOpen`, `GeIsClosed`, `DialogIsOpen`.
- `MinigameIsActive`, `MinigameNotActive`, `GroundItemNearby`.

### 7.4 Starter Bot Implementations
The project includes fully featured, pre-built bots in `Scripts\StarterBots.cs` accessible via `StarterBotCatalog.GetAllStarterBots()`:
1. **Smart Combat Fighter (`AutoCombatFighterBot`):** Automated PvM fighter with emergency eating, anti-poison/venom, hazard dodging (AoE floor mechanics), special attack execution, combat engagement checks, and rare ground drop looting.
2. **PvM Boss Slayer Pro (`AutoPvMBossSlayerBot`):** Advanced PvM slayer featuring dynamic floor hazard avoidance, projectile tracking, overhead defensive prayer flicking/management (`Protect from Melee/Missiles/Magic`), potion maintenance (anti-venom, antifire, stamina), weapon special attack triggers, and high-value loot extraction.
3. **Grand Exchange Flipper (`AutoGrandExchangeFlipperBot`):** Real-time Grand Exchange arbitrage bot tracking all 8 offer slots, completed purchase/sale detection, active trade monitoring, and inventory capital re-allocation.
4. **Smart Woodcutter (`AutoWoodcutterBot`):** Multi-tier tree detection, bird nest looting, chopping animation monitoring, and full inventory handling.
5. **Smart Fisher (`AutoFisherBot`):** Fishing spot discovery, animation monitoring, and inventory banking/management.
6. **Rooftop Agility Runner (`RooftopAgilityBot`):** Navigates rooftop agility courses, loots Marks of Grace, and tracks lap completions.
7. **High Alchemy Pro (`AutoAlcherBot`):** Automated High Alchemy with human Gaussian/log-normal variance click cadence and Nature rune verification.

---

## 8. Build, Packaging & Deployment Pipeline

### 8.1 Prerequisites
1. **Java Development Kit (JDK 11 or JDK 17+):**
   - Must have `javac` and `jar` on system `PATH`.
   - Ensure `JAVA_HOME` points to a 64-bit JDK.
2. **.NET 9.0 SDK:**
   - Must have `dotnet` CLI (version 9.0.100+).
3. **Windows 10/11 x64:**
   - PowerShell 5.1 or PowerShell 7+.

### 8.2 Automated Java Agent Build
Compile `BytecodeAgent.java` and `AttachHelper.java` targeting Java 11 bytecode compatibility and package with the manifest:

```powershell
# 1. Create agent build output folder
New-Item -ItemType Directory -Force -Path "agent\out"

# 2. Compile Java sources
javac -source 11 -target 11 -cp "agent\src\main\java" -d "agent\out" `
    agent\src\main\java\com\osrsmr\attach\AttachHelper.java `
    agent\src\main\java\com\osrsmr\agent\BytecodeAgent.java

# 3. Create agent.jar with META-INF manifest
jar cvfm agent.jar agent\src\main\resources\META-INF\MANIFEST.MF -C agent\out .

# 4. Copy agent.jar to all executable and project output paths
Copy-Item agent.jar agent\agent.jar -Force
Copy-Item agent.jar bin\Debug\net9.0-windows\agent.jar -Force
Copy-Item agent.jar bin\Release\net9.0-windows\agent.jar -Force
```

### 8.3 .NET 9 WPF Client Build
Compile the C# solution targeting Windows x64 Release:

```powershell
dotnet build osrsmr.csproj -c Release
```

### 8.4 Single-Click Full Build Script
Execute `rebuild.ps1` in the project root to perform a complete, clean rebuild of both the Java Bytecode Agent and the .NET 9 client:

```powershell
.\rebuild.ps1
```

---

## 9. Troubleshooting & Reverse Engineering Reference

| Issue / Symptom | Root Cause | Solution |
|---|---|---|
| **Agent fails to attach (`com.sun.tools.attach.AttachNotSupportedException`)** | Target JVM running under different elevation or 32-bit/64-bit mismatch. | Ensure both RuneLite and `osrsmr.exe` run under the same user privileges (non-admin or both admin) and both use 64-bit runtimes. |
| **Agent loads but `Client` instance is `null`** | RuneLite ClassLoader hierarchy changed or plugin sandbox isolation active. | Check `attach_log.txt`. The agent automatically walks all active JVM thread groups to locate the root classloader. Verify RuneLite is fully past the login/loading screen. |
| **TCP Port Conflict (`Address already in use: 43594`)** | Previous JVM or host instance did not release the local socket. | Run `Stop-Process -Name osrsmr, RuneLite -Force` in PowerShell or kill PID holding port 43594 via `netstat -ano \| findstr 43594`. |
| **Bank or Shop items not appearing in UI** | Interface virtualized or widget group ID changed after game engine update. | Verify widget group constants in `BytecodeAgent.java` (`12`, `15`, `300`, `301`, `423`). The recursive widget scraper traverses dynamic child arrays automatically. |
| **Tree or Rock names showing as generic "Object"** | Uncached definition in client cache on scene load. | Ensure ID is registered in `BytecodeAgent.java`'s fast ID lookup tables (`TREE_OBJ`, `ROCK_OBJ`). |
| **Mouse cursor clicks slightly outside RuneLite canvas** | Windows DPI scaling factor (e.g. 125% or 150%) distorting window bounds. | Set `osrsmr.exe` and RuneLite DPI settings to "Application" in Windows Compatibility Properties or use Per-Monitor DPI V2 awareness. |

---
*End of Developer & Reverse Engineering Handbook*
