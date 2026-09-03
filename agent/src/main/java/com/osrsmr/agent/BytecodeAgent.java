package com.osrsmr.agent;

import java.lang.instrument.Instrumentation;
import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.lang.reflect.Modifier;
import java.net.Socket;
import java.io.BufferedWriter;
import java.io.OutputStreamWriter;
import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;
import java.util.ArrayList;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

/**
 * High-performance, lightweight RuneLite telemetry agent.
 * Directly reads live game memory and streams structured telemetry to osrsmr.
 */
public class BytecodeAgent {
    private static final String VERSION = "1.4.0";
    private static final int PORT = 43594;
    private static volatile Thread heartbeatThread = null;
    private static final String JVM_PID = getPidInternal();

    // Fast O(1) definition caches
    private static final ConcurrentHashMap<Integer, String> ITEM_NAME_CACHE = new ConcurrentHashMap<>(1024);
    private static final ConcurrentHashMap<Integer, String> OBJECT_NAME_CACHE = new ConcurrentHashMap<>(1024);
    private static final ConcurrentHashMap<Integer, String> NPC_NAME_CACHE = new ConcurrentHashMap<>(512);
    private static final ConcurrentHashMap<String, Method> METHOD_CACHE = new ConcurrentHashMap<>(256);
    private static final Method NULL_METHOD_MARKER;

    static {
        Method dummy = null;
        try {
            dummy = Object.class.getMethod("hashCode");
        } catch (Throwable ignored) {}
        NULL_METHOD_MARKER = dummy;
    }

    // Cached Scene Payload for lightweight telemetry
    private static final StringBuilder cachedSceneData = new StringBuilder(16384);
    private static volatile long lastSceneScanTime = 0;
    private static volatile int lastScenePlayerX = -1;
    private static volatile int lastScenePlayerY = -1;
    private static volatile int lastScenePlane = -1;

    // Combat tracking state
    private static volatile String lastCombatTarget = "None";
    private static volatile int lastCombatTargetIndex = -1;
    private static volatile int lastCombatTargetLevel = 0;
    private static volatile String lastCombatTargetHp = "None";
    private static volatile int lastCombatTargetDistance = 0;
    private static volatile String lastCombatTargetPrayer = "None";
    private static volatile String lastCombatTargetWeapon = "None";
    private static volatile int lastCombatTargetAnim = -1;
    private static volatile int lastCombatTargetPose = -1;
    private static final List<String> lastCombatTargetGear = new ArrayList<>(16);
    private static final int[] lastEnemyEquipIds = new int[14];
    private static final String[] lastEnemyEquipNames = new String[14];
    private static final List<String> attackingEnemiesList = new ArrayList<>(16);

    private static final class SceneEntry implements Comparable<SceneEntry> {
        final int dist;
        final int id;
        final String name;
        final int worldX;
        final int worldY;
        final String extra;

        SceneEntry(int dist, int id, String name, int worldX, int worldY, String extra) {
            this.dist = dist;
            this.id = id;
            this.name = name;
            this.worldX = worldX;
            this.worldY = worldY;
            this.extra = extra;
        }

        @Override
        public int compareTo(SceneEntry o) {
            return Integer.compare(this.dist, o.dist);
        }
    }

    private static final class ItemInfo {
        final int id;
        final String name;
        final int qty;

        ItemInfo(int id, String name, int qty) {
            this.id = id;
            this.name = name;
            this.qty = qty;
        }
    }

    private static final List<SceneEntry> treeEntries = new ArrayList<>(64);
    private static final List<SceneEntry> bankEntries = new ArrayList<>(32);
    private static final List<SceneEntry> shopEntries = new ArrayList<>(32);
    private static final List<SceneEntry> altarEntries = new ArrayList<>(32);
    private static final List<SceneEntry> rockEntries = new ArrayList<>(64);
    private static final List<SceneEntry> shortcutEntries = new ArrayList<>(32);
    private static final List<SceneEntry> obstacleEntries = new ArrayList<>(32);
    private static final List<SceneEntry> groundItemEntries = new ArrayList<>(64);

    private static final java.util.Set<Integer> KNOWN_BANK_OBJECT_IDS = new java.util.HashSet<>(Arrays.asList(
        10083, 10355, 10356, 10357, 10517, 10562, 10583, 10584, 11744, 11748, 12308, 14367, 16642,
        18491, 19051, 20325, 22819, 24101, 24347, 25808, 26707, 26711, 27266, 27267, 27268, 27292,
        28430, 28431, 28432, 28433, 28546, 28547, 28548, 28549, 29085, 30089, 31427, 34752, 36559,
        36786, 39239, 42847, 44464, 4483, 6943, 7407, 7408
    ));

    // RuneLite Hook Cache
    private static volatile Object runeLiteClient = null;
    private static volatile Object runeLiteItemManager = null;

    private static final String[] SKILL_NAMES = {
        "Attack", "Defence", "Strength", "Hitpoints", "Ranged", "Prayer", "Magic", "Cooking",
        "Woodcutting", "Fletching", "Fishing", "Firemaking", "Crafting", "Smithing", "Mining",
        "Herblore", "Agility", "Thieving", "Slayer", "Farming", "Runecraft", "Hunter", "Construction", "Sailing"
    };

    private static final Object[][] STANDARD_PRAYERS_MAP = {
        {"Thick Skin", "THICK_SKIN", 4104},
        {"Burst of Strength", "BURST_OF_STRENGTH", 4105},
        {"Clarity of Thought", "CLARITY_OF_THOUGHT", 4106},
        {"Sharp Eye", "SHARP_EYE", 4122},
        {"Mystic Will", "MYSTIC_WILL", 4123},
        {"Rock Skin", "ROCK_SKIN", 4107},
        {"Superhuman Strength", "SUPERHUMAN_STRENGTH", 4108},
        {"Improved Reflexes", "IMPROVED_REFLEXES", 4109},
        {"Rapid Restore", "RAPID_RESTORE", 4110},
        {"Rapid Heal", "RAPID_HEAL", 4111},
        {"Protect Item", "PROTECT_ITEM", 4112},
        {"Hawk Eye", "HAWK_EYE", 4124},
        {"Mystic Lore", "MYSTIC_LORE", 4125},
        {"Steel Skin", "STEEL_SKIN", 4113},
        {"Ultimate Strength", "ULTIMATE_STRENGTH", 4114},
        {"Incredible Reflexes", "INCREDIBLE_REFLEXES", 4115},
        {"Protect from Magic", "PROTECT_FROM_MAGIC", 4116},
        {"Protect from Missiles", "PROTECT_FROM_MISSILES", 4117},
        {"Protect from Melee", "PROTECT_FROM_MELEE", 4118},
        {"Eagle Eye", "EAGLE_EYE", 4126},
        {"Mystic Might", "MYSTIC_MIGHT", 4127},
        {"Retribution", "RETRIBUTION", 4119},
        {"Redemption", "REDEMPTION", 4120},
        {"Smite", "SMITE", 4121},
        {"Preserve", "PRESERVE", 5466},
        {"Chivalry", "CHIVALRY", 4128},
        {"Piety", "PIETY", 4129},
        {"Rigour", "RIGOUR", 5464},
        {"Augury", "AUGURY", 5465}
    };

    public static void premain(String agentArgs, Instrumentation inst) {
        initialize(inst);
    }

    public static void agentmain(String agentArgs, Instrumentation inst) {
        initialize(inst);
    }

    private static synchronized void initialize(Instrumentation inst) {
        String sunJavaCmd = System.getProperty("sun.java.command", "");
        if (sunJavaCmd.contains("com.osrsmr.attach.AttachHelper")) return;
        if (heartbeatThread != null && heartbeatThread.isAlive()) return;

        heartbeatThread = new Thread(() -> {
            try {
                Thread.sleep(300);
                Socket socket = null;
                BufferedWriter writer = null;
                StringBuilder data = new StringBuilder(16384);

                while (true) {
                    try {
                        if (runeLiteClient == null || runeLiteItemManager == null) {
                            scanAndDiscover(inst);
                        }

                        if (runeLiteClient == null) {
                            if (socket != null) {
                                try { socket.close(); } catch (Exception ignored) {}
                                socket = null;
                                writer = null;
                            }
                            Thread.sleep(500);
                            continue;
                        }

                        if (socket == null || socket.isClosed() || !socket.isConnected()) {
                            try {
                                socket = new Socket("127.0.0.1", PORT);
                                socket.setTcpNoDelay(true);
                                socket.setSendBufferSize(65536);
                                writer = new BufferedWriter(new OutputStreamWriter(socket.getOutputStream(), StandardCharsets.UTF_8), 16384);
                            } catch (Exception e) {
                                Thread.sleep(800);
                                continue;
                            }
                        }

                        data.setLength(0);
                        data.append("PID: ").append(JVM_PID).append("\n");
                        data.append("Status: Hook Active (v").append(VERSION).append(")\n");

                        processRuneLiteClient(runeLiteClient, data);

                        if (writer != null) {
                            writer.append(data);
                            writer.flush();
                        }
                    } catch (Throwable t) {
                        if (socket != null) {
                            try { socket.close(); } catch (Exception ignored) {}
                            socket = null;
                            writer = null;
                        }
                    }
                    Thread.sleep(50);
                }
            } catch (Exception ignored) {}
        }, "RuneLite-Telemetry-Worker");

        heartbeatThread.setDaemon(true);
        heartbeatThread.setPriority(Thread.NORM_PRIORITY - 1);
        heartbeatThread.start();
    }

    private static String getPidInternal() {
        try {
            String jvmName = java.lang.management.ManagementFactory.getRuntimeMXBean().getName();
            int idx = jvmName.indexOf('@');
            return idx > 0 ? jvmName.substring(0, idx) : jvmName;
        } catch (Throwable t) {
            return "Unknown";
        }
    }

    private static volatile long lastScanTimeMillis = 0;

    private static void scanAndDiscover(Instrumentation inst) {
        if ((runeLiteClient != null && runeLiteItemManager != null) || inst == null) return;
        long now = System.currentTimeMillis();
        if (now - lastScanTimeMillis < 500) return;
        lastScanTimeMillis = now;

        try {
            Class<?>[] allLoaded = inst.getAllLoadedClasses();

            for (Class<?> clazz : allLoaded) {
                if ("net.runelite.client.RuneLite".equals(clazz.getName())) {
                    try {
                        Object injector = null;
                        Method getInjector = findMethod(clazz, "getInjector");
                        if (getInjector != null) {
                            injector = getInjector.invoke(null);
                        }

                        if (injector != null) {
                            // Discover Client
                            for (Class<?> c : allLoaded) {
                                if ("net.runelite.api.Client".equals(c.getName())) {
                                    Method getInstance = findMethod(injector.getClass(), "getInstance", Class.class);
                                    if (getInstance != null) {
                                        runeLiteClient = getInstance.invoke(injector, c);
                                        break;
                                    }
                                }
                            }
                            // Discover ItemManager
                            for (Class<?> c : allLoaded) {
                                if ("net.runelite.client.game.ItemManager".equals(c.getName())) {
                                    Method getInstance = findMethod(injector.getClass(), "getInstance", Class.class);
                                    if (getInstance != null) {
                                        runeLiteItemManager = getInstance.invoke(injector, c);
                                        break;
                                    }
                                }
                            }
                        }
                    } catch (Throwable ignored) {}
                }
                if (runeLiteClient != null && runeLiteItemManager != null) break;
            }
        } catch (Throwable ignored) {}
    }

    private static void processRuneLiteClient(Object client, StringBuilder data) {
        try {
            data.append("Client Class: RuneLite-Injected\n");

            // 1. GameState
            int gs = 0;
            String stateStr = "Unknown";
            Object gsObj = invokeMethodQuietly(client, "getGameState");
            if (gsObj instanceof Number) {
                gs = ((Number) gsObj).intValue();
            } else if (gsObj instanceof Enum) {
                String enumName = ((Enum<?>) gsObj).name();
                if ("LOGGED_IN".equalsIgnoreCase(enumName)) gs = 30;
                else if ("LOGIN_SCREEN".equalsIgnoreCase(enumName)) gs = 10;
                else if ("LOADING".equalsIgnoreCase(enumName)) gs = 25;
                else if ("HOPPING".equalsIgnoreCase(enumName)) gs = 45;
                else if ("CONNECTION_LOST".equalsIgnoreCase(enumName)) gs = 40;
            }

            Object player = invokeMethodQuietly(client, "getLocalPlayer");
            if (player == null) {
                Object topView = invokeMethodQuietly(client, "getTopLevelWorldView");
                if (topView != null) {
                    player = invokeMethodQuietly(topView, "getLocalPlayer");
                }
            }
            if (player != null && gs == 0) gs = 30;

            if (gs == 30) stateStr = "Logged In";
            else if (gs == 10) stateStr = "Login Screen";
            else if (gs == 25) stateStr = "Loading";
            else if (gs == 45) stateStr = "Hopping";
            else if (gs == 40) stateStr = "Connection Lost";
            else stateStr = "Detecting...";

            data.append("GameState: ").append(gs).append("\n");
            data.append("ENGINE_STATE: ").append(stateStr).append("\n");

            // World
            Object wObj = invokeMethodQuietly(client, "getWorld");
            if (wObj instanceof Integer) {
                int world = (Integer) wObj;
                if (world > 0) {
                    if (world < 300) world += 300;
                    data.append("WORLD: ").append(world).append("\n");
                }
            }

            if (gs != 30 && player == null) return;

            // 2. Local Player Location & State
            int playerX = 0, playerY = 0, plane = 0;
            String localPlayerName = "";
            String interactingName = "None";
            String interactingType = "None";
            int interactingId = -1;

            boolean isInstanced = false;
            try {
                Object instObj = invokeMethodQuietly(client, "isInInstancedRegion");
                if (instObj instanceof Boolean) {
                    isInstanced = (Boolean) instObj;
                }
                if (!isInstanced) {
                    Object topView = invokeMethodQuietly(client, "getTopLevelWorldView");
                    if (topView != null) {
                        Object wvInst = invokeMethodQuietly(topView, "isInInstance");
                        if (wvInst instanceof Boolean) isInstanced = (Boolean) wvInst;
                    }
                }
            } catch (Throwable ignored) {}

            data.append("IS_INSTANCED: ").append(isInstanced ? "True" : "False").append("\n");
            data.append("IN_INSTANCE: ").append(isInstanced ? "True" : "False").append("\n");

            // Game Tick & Cycle (read early for tick-perfect synchronization)
            Object tick = invokeMethodQuietly(client, "getTickCount");
            if (tick instanceof Integer) {
                data.append("GAME_TICK: ").append(tick).append("\n");
            }
            Object cycle = invokeMethodQuietly(client, "getGameCycle");
            if (cycle instanceof Integer) {
                data.append("GAME_CYCLE: ").append(cycle).append("\n");
            } else if (tick instanceof Integer) {
                data.append("GAME_CYCLE: ").append(tick).append("\n");
            }

            if (player != null) {
                Object nameObj = invokeMethodQuietly(player, "getName");
                if (nameObj instanceof String) {
                    localPlayerName = ((String) nameObj).replace('\u00A0', ' ').replaceAll("<[^>]*>", "").trim();
                    if (!localPlayerName.isEmpty()) {
                        data.append("PLAYER_NAME: ").append(localPlayerName).append("\n");
                    }
                }

                Object wp = invokeMethodQuietly(player, "getWorldLocation");
                if (wp != null) {
                    Object gx = invokeMethodQuietly(wp, "getX");
                    Object gy = invokeMethodQuietly(wp, "getY");
                    Object gp = invokeMethodQuietly(wp, "getPlane");
                    if (gx instanceof Integer) playerX = (Integer) gx;
                    if (gy instanceof Integer) playerY = (Integer) gy;
                    if (gp instanceof Integer) plane = (Integer) gp;
                }

                if (playerX > 0 && playerY > 0) {
                    int regionId = ((playerX >> 6) << 8) | (playerY >> 6);
                    String locationName = resolveLocationName(playerX, playerY, plane, regionId);
                    data.append("PLAYER_X: ").append(playerX).append("\n");
                    data.append("PLAYER_Y: ").append(playerY).append("\n");
                    data.append("PLANE: ").append(plane).append("\n");
                    data.append("REGION_ID: ").append(regionId).append("\n");
                    data.append("LOCATION: ").append(locationName).append("\n");
                    data.append("LOCATION_NAME: ").append(locationName).append("\n");
                    data.append("TOWN: ").append(locationName).append("\n");
                    data.append("WORLD_LOCATION: ").append(playerX).append(",").append(playerY).append(",").append(plane).append("\n");
                }

                Object animObj = invokeMethodQuietly(player, "getAnimation");
                if (animObj instanceof Integer) data.append("ANIMATION: ").append(animObj).append("\n");

                Object poseAnimObj = invokeMethodQuietly(player, "getPoseAnimation");
                if (poseAnimObj instanceof Integer) data.append("POSE_ANIMATION: ").append(poseAnimObj).append("\n");

                Object orientObj = invokeMethodQuietly(player, "getOrientation");
                if (orientObj instanceof Integer) data.append("ORIENTATION: ").append(orientObj).append("\n");

                Object cbObj = invokeMethodQuietly(player, "getCombatLevel");
                if (cbObj instanceof Integer) data.append("COMBAT_LEVEL: ").append(cbObj).append("\n");

                // Target / Interacting Actor
                Object target = invokeMethodQuietly(player, "getInteracting");
                if (target != null) {
                    String clName = target.getClass().getName().toLowerCase();
                    boolean isNpc = clName.contains("npc");
                    boolean isPlr = clName.contains("player");
                    Object idObj = invokeMethodQuietly(target, "getId");
                    if (idObj instanceof Integer) interactingId = (Integer) idObj;

                    if (isNpc) {
                        interactingType = "NPC";
                        interactingName = extractNpcName(client, target, interactingId);
                    } else if (isPlr) {
                        interactingType = "Player";
                        Object pn = invokeMethodQuietly(target, "getName");
                        if (pn instanceof String) interactingName = cleanName((String) pn);
                    } else {
                        interactingName = extractNpcName(client, target, interactingId);
                    }
                }

                data.append("INTERACTING: ").append(interactingName).append("\n");
                data.append("INTERACTING_TYPE: ").append(interactingType).append("\n");
                data.append("INTERACTING_ID: ").append(interactingId).append("\n");
            }

            // 3. Skills
            Object rLevelsObj = invokeMethodQuietly(client, "getRealSkillLevels");
            Object bLevelsObj = invokeMethodQuietly(client, "getBoostedSkillLevels");
            Object xpObj = invokeMethodQuietly(client, "getSkillExperiences");

            if (rLevelsObj instanceof int[] && bLevelsObj instanceof int[]) {
                int[] rLevels = (int[]) rLevelsObj;
                int[] bLevels = (int[]) bLevelsObj;
                int[] exps = (xpObj instanceof int[]) ? (int[]) xpObj : null;

                for (int i = 0; i < Math.min(rLevels.length, SKILL_NAMES.length); i++) {
                    int real = rLevels[i];
                    int boosted = (i < bLevels.length) ? bLevels[i] : real;
                    data.append("SKILL[").append(SKILL_NAMES[i]).append("]: ").append(boosted).append("/").append(real).append("\n");

                    if (exps != null && i < exps.length) {
                        data.append("SKILL_XP[").append(SKILL_NAMES[i]).append("]: ").append(exps[i]).append("\n");
                    }

                    if ("Hitpoints".equalsIgnoreCase(SKILL_NAMES[i])) {
                        data.append("HP: ").append(boosted).append("/").append(real).append("\n");
                    } else if ("Prayer".equalsIgnoreCase(SKILL_NAMES[i])) {
                        data.append("PRAYER: ").append(boosted).append("/").append(real).append("\n");
                    }
                }
            }

            // 4. Energy & Weight
            Object energyObj = invokeMethodQuietly(client, "getEnergy");
            if (energyObj instanceof Integer) {
                int energy = ((Integer) energyObj) / 100;
                data.append("RUN_ENERGY: ").append(energy).append("\n");
                data.append("ENERGY: ").append(energy).append("\n");
            }

            Object weightObj = invokeMethodQuietly(client, "getWeight");
            if (weightObj instanceof Integer) {
                data.append("WEIGHT: ").append(weightObj).append("\n");
            }

            // 5. Special Attack & Spellbook
            int specAmt = getVarbitValue(client, 300);
            if (specAmt >= 0) data.append("SPECIAL_ATTACK: ").append(specAmt / 10).append("\n");

            int specActive = getVarbitValue(client, 301);
            if (specActive >= 0) data.append("SPECIAL_ATTACK_ACTIVE: ").append(specActive == 1 ? "True" : "False").append("\n");

            int spellbook = getVarbitValue(client, 4070);
            String sbName = "Standard";
            if (spellbook == 1) sbName = "Ancient";
            else if (spellbook == 2) sbName = "Lunar";
            else if (spellbook == 3) sbName = "Arceuus";
            data.append("MAGIC_SPELLBOOK: ").append(sbName).append("\n");

            int autocast = getVarbitValue(client, 276);
            if (autocast > 0) data.append("AUTOCAST_SPELL: ").append(autocast).append("\n");

            String activeTab = extractActiveTab(client);
            data.append("ACTIVE_TAB: ").append(activeTab).append("\n");

            // Movement & Idle state
            int pAnim = -1, pPoseAnim = -1, pIdlePose = -1;
            Object animObj = invokeMethodQuietly(player, "getAnimation");
            if (animObj instanceof Integer) pAnim = (Integer) animObj;
            Object poseAnimObj = invokeMethodQuietly(player, "getPoseAnimation");
            if (poseAnimObj instanceof Integer) pPoseAnim = (Integer) poseAnimObj;
            Object idlePoseObj = invokeMethodQuietly(player, "getIdlePoseAnimation");
            if (idlePoseObj instanceof Integer) pIdlePose = (Integer) idlePoseObj;

            boolean isMoving = (pPoseAnim != -1 && pIdlePose != -1 && pPoseAnim != pIdlePose);
            boolean isIdle = (pAnim == -1 && !isMoving);
            data.append("IS_MOVING: ").append(isMoving ? "True" : "False").append("\n");
            data.append("IS_IDLE: ").append(isIdle ? "True" : "False").append("\n");

            // Vengeance
            int vengActive = getVarbitValue(client, 2451);
            data.append("VENGEANCE_ACTIVE: ").append(vengActive == 1 ? "True" : "False").append("\n");

            // Wilderness
            if (playerY >= 3520 && playerY <= 4000 && playerX >= 2940 && playerX <= 3400) {
                int wildyLvl = (playerY - 3520) / 8 + 1;
                data.append("WILDERNESS_LEVEL: ").append(wildyLvl).append("\n");
                data.append("IN_WILDERNESS: True\n");
            } else {
                data.append("WILDERNESS_LEVEL: 0\n");
                data.append("IN_WILDERNESS: False\n");
            }

            // 6. Inventory & Equipment
            readItemContainer(client, 93, "INV", 28, data);
            readItemContainer(client, 94, "EQUIP", 14, data);

            // 7. Bank & Shop Containers
            processBankAndShop(client, data);

            // 7.5 Grand Exchange & Storage Containers
            processGrandExchange(client, data);
            processStorageContainers(client, data);

            // 8. Scene Objects & Ground Items
            processSceneObjectsAndGroundItems(client, playerX, playerY, plane, data);

            // 9. Surrounding NPCs, Players & Unified PK Combat State
            processCombatAndEntities(client, player, localPlayerName, playerX, playerY, plane, data);

            // 10. Dialogue
            processDialogue(client, data);

            // 11. Active Prayers
            processPrayers(client, data);

            // 12. Buffs, Potion Timers & Poison/Venom Status
            processBuffsAndStatusTimers(client, data);

            // 13. Camera & Canvas
            Object camX = invokeMethodQuietly(client, "getCameraX");
            Object camY = invokeMethodQuietly(client, "getCameraY");
            Object camZ = invokeMethodQuietly(client, "getCameraZ");
            Object pitch = invokeMethodQuietly(client, "getCameraPitch");
            Object yaw = invokeMethodQuietly(client, "getCameraYaw");
            if (camX instanceof Integer && camY instanceof Integer && camZ instanceof Integer) {
                data.append("CAMERA: ").append(camX).append(",").append(camY).append(",").append(camZ)
                    .append(",").append(pitch != null ? pitch : 0)
                    .append(",").append(yaw != null ? yaw : 0).append("\n");
            }

            Object cw = invokeMethodQuietly(client, "getCanvasWidth");
            Object ch = invokeMethodQuietly(client, "getCanvasHeight");
            if (cw instanceof Integer && ch instanceof Integer) {
                data.append("CANVAS: ").append(cw).append(",").append(ch).append("\n");
            }

        } catch (Throwable ignored) {}
    }

    private static Object getItemContainer(Object client, int containerId) {
        if (client == null) return null;
        try {
            Object container = invokeMethodQuietly(client, "getItemContainer", containerId);
            if (container != null) return container;
        } catch (Throwable ignored) {}

        try {
            Class<?> invEnum = Class.forName("net.runelite.api.InventoryID", true, client.getClass().getClassLoader());
            if (invEnum != null && invEnum.isEnum()) {
                for (Object constant : invEnum.getEnumConstants()) {
                    Object idObj = invokeMethodQuietly(constant, "getId");
                    if (idObj instanceof Integer && ((Integer) idObj) == containerId) {
                        Method m = findMethod(client.getClass(), "getItemContainer", invEnum);
                        if (m != null) {
                            Object container = m.invoke(client, constant);
                            if (container != null) return container;
                        }
                    }
                }
            }
        } catch (Throwable ignored) {}

        try {
            Object containersTable = invokeMethodQuietly(client, "getItemContainers");
            if (containersTable != null) {
                Object container = invokeMethodQuietly(containersTable, "get", (long) containerId);
                if (container != null) return container;
                container = invokeMethodQuietly(containersTable, "get", containerId);
                if (container != null) return container;
            }
        } catch (Throwable ignored) {}

        return null;
    }

    private static void readItemContainer(Object client, int containerId, String prefix, int maxSlots, StringBuilder data) {
        if (client == null) return;
        try {
            Object container = getItemContainer(client, containerId);
            if (container == null) return;

            Object itemsObj = invokeMethodQuietly(container, "getItems");
            if (!(itemsObj instanceof Object[])) return;

            Object[] items = (Object[]) itemsObj;
            for (int i = 0; i < maxSlots; i++) {
                Object item = (i < items.length) ? items[i] : null;
                if (item == null) {
                    data.append(prefix).append("[").append(i).append("]: EMPTY\n");
                    continue;
                }

                int id = -1;
                int qty = 0;
                Object idObj = invokeMethodQuietly(item, "getId");
                if (idObj instanceof Integer) id = (Integer) idObj;
                Object qtyObj = invokeMethodQuietly(item, "getQuantity");
                if (qtyObj instanceof Integer) qty = (Integer) qtyObj;

                if (id > 0 && id != 65535) {
                    String name = resolveItemName(id);
                    data.append(prefix).append("[").append(i).append("]: ")
                        .append(id).append(",").append(name).append(",").append(Math.max(1, qty)).append("\n");
                } else {
                    data.append(prefix).append("[").append(i).append("]: EMPTY\n");
                }
            }
        } catch (Throwable ignored) {}
    }

    private static void processBankAndShop(Object client, StringBuilder data) {
        if (client == null) return;
        try {
            // 1. Bank Widgets (12 = Bank, 192 = Deposit Box, 631 = Seed Vault, 213 = PIN)
            Object bankWidget = getWidget(client, 12, 1);
            if (bankWidget == null) bankWidget = getWidget(client, 12, 0);
            if (bankWidget == null) bankWidget = getWidget(client, 192, 1);
            if (bankWidget == null) bankWidget = getWidget(client, 631, 1);
            if (bankWidget == null) bankWidget = getWidget(client, 213, 0);

            boolean bankWidgetOpen = isWidgetVisible(bankWidget);

            // Bank Container (95)
            Object bankContainer = getItemContainer(client, 95);
            List<ItemInfo> bankItemsList = new ArrayList<>();

            if (bankContainer != null) {
                Object itemsObj = invokeMethodQuietly(bankContainer, "getItems");
                if (itemsObj instanceof Object[]) {
                    Object[] items = (Object[]) itemsObj;
                    for (Object it : items) {
                        if (it == null) continue;
                        int id = -1, qty = 0;
                        Object idObj = invokeMethodQuietly(it, "getId");
                        if (idObj instanceof Integer) id = (Integer) idObj;
                        Object qtyObj = invokeMethodQuietly(it, "getQuantity");
                        if (qtyObj instanceof Integer) qty = (Integer) qtyObj;

                        if (id > 0 && id != 65535) {
                            String name = resolveItemName(id);
                            bankItemsList.add(new ItemInfo(id, name, Math.max(1, qty)));
                        }
                    }
                }
            }

            // Fallback: scan bank item container widget children if container didn't yield items
            if (bankItemsList.isEmpty() && bankWidgetOpen) {
                Object itemContainerWidget = getWidget(client, 12, 13);
                if (itemContainerWidget == null) itemContainerWidget = getWidget(client, 12, 12);
                if (itemContainerWidget == null) itemContainerWidget = getWidget(client, 12, 1);
                if (itemContainerWidget != null) {
                    Object childrenObj = invokeMethodQuietly(itemContainerWidget, "getDynamicChildren");
                    if (childrenObj == null) childrenObj = invokeMethodQuietly(itemContainerWidget, "getChildren");
                    if (childrenObj instanceof Object[]) {
                        for (Object childWidget : (Object[]) childrenObj) {
                            if (childWidget == null) continue;
                            int itemId = -1, itemQty = 0;
                            Object idObj = invokeMethodQuietly(childWidget, "getItemId");
                            if (idObj instanceof Integer) itemId = (Integer) idObj;
                            if (itemId <= 0) {
                                idObj = invokeMethodQuietly(childWidget, "getId");
                                if (idObj instanceof Integer) itemId = (Integer) idObj;
                            }
                            Object qtyObj = invokeMethodQuietly(childWidget, "getItemQuantity");
                            if (qtyObj instanceof Integer) itemQty = (Integer) qtyObj;

                            if (itemId > 0 && itemId != 65535) {
                                String name = resolveItemName(itemId);
                                bankItemsList.add(new ItemInfo(itemId, name, Math.max(1, itemQty)));
                            }
                        }
                    }
                }
            }

            boolean isBankOpen = bankWidgetOpen || !bankItemsList.isEmpty();
            data.append("BANK_OPEN: ").append(isBankOpen ? "True" : "False").append("\n");
            data.append("IS_BANK_OPEN: ").append(isBankOpen ? "True" : "False").append("\n");
            data.append("BANK_TOTAL_ITEMS: ").append(bankItemsList.size()).append("\n");
            for (int i = 0; i < bankItemsList.size(); i++) {
                ItemInfo it = bankItemsList.get(i);
                data.append("BANK_ITEM[").append(i).append("]: ").append(it.id).append(",").append(it.name).append(",").append(it.qty).append("\n");
            }

            // 2. Shop Widgets (300 = Shop) & Shop Container (3)
            Object shopWidget = getWidget(client, 300, 1);
            if (shopWidget == null) shopWidget = getWidget(client, 300, 0);
            boolean shopWidgetOpen = isWidgetVisible(shopWidget);

            Object shopContainer = getItemContainer(client, 3);
            List<ItemInfo> shopItemsList = new ArrayList<>();

            if (shopContainer != null) {
                Object itemsObj = invokeMethodQuietly(shopContainer, "getItems");
                if (itemsObj instanceof Object[]) {
                    Object[] items = (Object[]) itemsObj;
                    for (Object it : items) {
                        if (it == null) continue;
                        int id = -1, qty = 0;
                        Object idObj = invokeMethodQuietly(it, "getId");
                        if (idObj instanceof Integer) id = (Integer) idObj;
                        Object qtyObj = invokeMethodQuietly(it, "getQuantity");
                        if (qtyObj instanceof Integer) qty = (Integer) qtyObj;

                        if (id > 0 && id != 65535) {
                            String name = resolveItemName(id);
                            shopItemsList.add(new ItemInfo(id, name, Math.max(1, qty)));
                        }
                    }
                }
            }

            if (shopItemsList.isEmpty() && shopWidgetOpen) {
                Object itemContainerWidget = getWidget(client, 300, 16);
                if (itemContainerWidget == null) itemContainerWidget = getWidget(client, 300, 1);
                if (itemContainerWidget != null) {
                    Object childrenObj = invokeMethodQuietly(itemContainerWidget, "getDynamicChildren");
                    if (childrenObj == null) childrenObj = invokeMethodQuietly(itemContainerWidget, "getChildren");
                    if (childrenObj instanceof Object[]) {
                        for (Object childWidget : (Object[]) childrenObj) {
                            if (childWidget == null) continue;
                            int itemId = -1, itemQty = 0;
                            Object idObj = invokeMethodQuietly(childWidget, "getItemId");
                            if (idObj instanceof Integer) itemId = (Integer) idObj;
                            if (itemId <= 0) {
                                idObj = invokeMethodQuietly(childWidget, "getId");
                                if (idObj instanceof Integer) itemId = (Integer) idObj;
                            }
                            Object qtyObj = invokeMethodQuietly(childWidget, "getItemQuantity");
                            if (qtyObj instanceof Integer) itemQty = (Integer) qtyObj;

                            if (itemId > 0 && itemId != 65535) {
                                String name = resolveItemName(itemId);
                                shopItemsList.add(new ItemInfo(itemId, name, Math.max(1, itemQty)));
                            }
                        }
                    }
                }
            }

            boolean isShopOpen = shopWidgetOpen || !shopItemsList.isEmpty();
            data.append("SHOP_OPEN: ").append(isShopOpen ? "True" : "False").append("\n");
            data.append("IS_SHOP_OPEN: ").append(isShopOpen ? "True" : "False").append("\n");
            data.append("SHOP_TOTAL_ITEMS: ").append(shopItemsList.size()).append("\n");
            for (int i = 0; i < shopItemsList.size(); i++) {
                ItemInfo it = shopItemsList.get(i);
                data.append("SHOP_ITEM[").append(i).append("]: ").append(it.id).append(",").append(it.name).append(",").append(it.qty).append("\n");
            }
        } catch (Throwable ignored) {}
    }

    private static final String[] RUNE_POUCH_RUNES = {
        "None", "Air rune", "Water rune", "Earth rune", "Fire rune", "Mind rune",
        "Chaos rune", "Death rune", "Blood rune", "Cosmic rune", "Nature rune",
        "Law rune", "Body rune", "Soul rune", "Astral rune", "Mist rune",
        "Mud rune", "Dust rune", "Lava rune", "Steam rune", "Smoke rune", "Wrath rune", "Sunfire rune"
    };

    private static void processGrandExchange(Object client, StringBuilder data) {
        if (client == null) return;
        try {
            Object offersObj = invokeMethodQuietly(client, "getGrandExchangeOffers");
            if (offersObj instanceof Object[]) {
                Object[] offers = (Object[]) offersObj;
                for (int i = 0; i < offers.length && i < 8; i++) {
                    Object offer = offers[i];
                    if (offer == null) {
                        data.append("GE_SLOT[").append(i).append("]: Empty,0,Empty,0,0,0,0\n");
                        continue;
                    }
                    Object stateObj = invokeMethodQuietly(offer, "getState");
                    String state = (stateObj != null) ? stateObj.toString() : "Empty";
                    int itemId = 0, price = 0, totalQty = 0, qtySold = 0, spent = 0;
                    Object idObj = invokeMethodQuietly(offer, "getItemId");
                    if (idObj instanceof Integer) itemId = (Integer) idObj;
                    Object prObj = invokeMethodQuietly(offer, "getPrice");
                    if (prObj instanceof Integer) price = (Integer) prObj;
                    Object tqObj = invokeMethodQuietly(offer, "getTotalQuantity");
                    if (tqObj instanceof Integer) totalQty = (Integer) tqObj;
                    Object qsObj = invokeMethodQuietly(offer, "getQuantitySold");
                    if (qsObj instanceof Integer) qtySold = (Integer) qsObj;
                    Object spObj = invokeMethodQuietly(offer, "getSpent");
                    if (spObj instanceof Integer) spent = (Integer) spObj;

                    String itemName = (itemId > 0) ? resolveItemName(itemId) : "None";
                    data.append("GE_SLOT[").append(i).append("]: ")
                        .append(state).append(",")
                        .append(itemId).append(",")
                        .append(itemName).append(",")
                        .append(price).append(",")
                        .append(totalQty).append(",")
                        .append(qtySold).append(",")
                        .append(spent).append("\n");
                }
            }
        } catch (Throwable ignored) {}
    }

    private static void processStorageContainers(Object client, StringBuilder data) {
        if (client == null) return;
        try {
            // 1. Rune Pouch (Varbits 1144, 1145, 1146, 14285 / amounts 1139, 1140, 1141, 14286)
            int[] runeTypes = {
                getVarbitValue(client, 1144),
                getVarbitValue(client, 1145),
                getVarbitValue(client, 1146),
                getVarbitValue(client, 14285)
            };
            int[] runeAmounts = {
                getVarbitValue(client, 1139),
                getVarbitValue(client, 1140),
                getVarbitValue(client, 1141),
                getVarbitValue(client, 14286)
            };
            for (int i = 0; i < 4; i++) {
                int typeIdx = runeTypes[i];
                int qty = runeAmounts[i];
                String runeName = (typeIdx > 0 && typeIdx < RUNE_POUCH_RUNES.length) ? RUNE_POUCH_RUNES[typeIdx] : (typeIdx > 0 ? "Rune #" + typeIdx : "None");
                if (typeIdx > 0 && qty > 0) {
                    data.append("RUNE_POUCH[").append(i).append("]: ").append(typeIdx).append(",").append(runeName).append(",").append(qty).append("\n");
                } else {
                    data.append("RUNE_POUCH[").append(i).append("]: 0,None,0\n");
                }
            }

            // 2. Gem Bag (Varbits 3886..3890)
            int sapphire = getVarbitValue(client, 3886);
            int emerald = getVarbitValue(client, 3887);
            int ruby = getVarbitValue(client, 3888);
            int diamond = getVarbitValue(client, 3889);
            int dragonstone = getVarbitValue(client, 3890);
            if (sapphire >= 0 || emerald >= 0 || ruby >= 0 || diamond >= 0 || dragonstone >= 0) {
                data.append("GEM_BAG: ")
                    .append(Math.max(0, sapphire)).append(",")
                    .append(Math.max(0, emerald)).append(",")
                    .append(Math.max(0, ruby)).append(",")
                    .append(Math.max(0, diamond)).append(",")
                    .append(Math.max(0, dragonstone)).append("\n");
            }

            // 3. Essence Pouches (Small, Medium, Large, Giant, Colossal)
            int smallEss = getVarpValue(client, 1374);
            int medEss = getVarpValue(client, 1375);
            int largeEss = getVarpValue(client, 1376);
            int giantEss = getVarpValue(client, 1377);
            int colossalEss = getVarbitValue(client, 13709);
            if (smallEss >= 0 || medEss >= 0 || largeEss >= 0 || giantEss >= 0 || colossalEss >= 0) {
                data.append("ESSENCE_POUCHES: ")
                    .append(Math.max(0, smallEss)).append(",")
                    .append(Math.max(0, medEss)).append(",")
                    .append(Math.max(0, largeEss)).append(",")
                    .append(Math.max(0, giantEss)).append(",")
                    .append(Math.max(0, colossalEss)).append("\n");
            }

            // 4. Looting Bag Container (516)
            readItemContainer(client, 516, "LOOTING_BAG", 28, data);
        } catch (Throwable ignored) {}
    }

    private static void processSceneObjectsAndGroundItems(Object client, int playerX, int playerY, int plane, StringBuilder data) {
        if (client == null || playerX <= 0 || playerY <= 0) return;
        long now = System.currentTimeMillis();
        if ((now - lastSceneScanTime < 150) && (playerX == lastScenePlayerX) && (playerY == lastScenePlayerY) && (plane == lastScenePlane) && cachedSceneData.length() > 0) {
            data.append(cachedSceneData);
            return;
        }

        try {
            cachedSceneData.setLength(0);
            Object scene = null;
            Object topView = invokeMethodQuietly(client, "getTopLevelWorldView");
            if (topView != null) scene = invokeMethodQuietly(topView, "getScene");
            if (scene == null) scene = invokeMethodQuietly(client, "getScene");
            if (scene == null) return;

            Object tilesObj = invokeMethodQuietly(scene, "getTiles");
            if (!(tilesObj instanceof Object[][][])) return;

            Object[][][] tiles = (Object[][][]) tilesObj;
            if (plane < 0 || plane >= tiles.length) plane = 0;
            Object[][] planeTiles = tiles[plane];
            if (planeTiles == null) return;

            int baseX = 0, baseY = 0;
            if (topView != null) {
                Object bx = invokeMethodQuietly(topView, "getBaseX");
                Object by = invokeMethodQuietly(topView, "getBaseY");
                if (bx instanceof Integer && by instanceof Integer) {
                    baseX = (Integer) bx;
                    baseY = (Integer) by;
                }
            }
            if (baseX == 0 || baseY == 0) {
                Object bx = invokeMethodQuietly(client, "getBaseX");
                Object by = invokeMethodQuietly(client, "getBaseY");
                if (bx instanceof Integer && by instanceof Integer) {
                    baseX = (Integer) bx;
                    baseY = (Integer) by;
                }
            }
            if (baseX == 0 || baseY == 0) {
                Object lp = invokeMethodQuietly(client, "getLocalPlayer");
                if (lp != null) {
                    Object loc = invokeMethodQuietly(lp, "getLocalLocation");
                    if (loc != null) {
                        Object sx = invokeMethodQuietly(loc, "getSceneX");
                        Object sy = invokeMethodQuietly(loc, "getSceneY");
                        if (sx instanceof Integer && sy instanceof Integer) {
                            baseX = playerX - (Integer) sx;
                            baseY = playerY - (Integer) sy;
                        }
                    }
                }
            }

            int localX = (baseX > 0) ? playerX - baseX : 52;
            int localY = (baseY > 0) ? playerY - baseY : 52;
            int minTx = Math.max(0, localX - 45);
            int maxTx = Math.min(103, localX + 45);
            int minTy = Math.max(0, localY - 45);
            int maxTy = Math.min(103, localY + 45);

            treeEntries.clear();
            bankEntries.clear();
            shopEntries.clear();
            altarEntries.clear();
            rockEntries.clear();
            shortcutEntries.clear();
            obstacleEntries.clear();
            groundItemEntries.clear();

            for (int tx = minTx; tx <= maxTx; tx++) {
                for (int ty = minTy; ty <= maxTy; ty++) {
                    if (tx >= planeTiles.length || ty >= planeTiles[tx].length) continue;
                    Object tile = planeTiles[tx][ty];
                    if (tile == null) continue;

                    int worldX = baseX + tx;
                    int worldY = baseY + ty;
                    int dist = Math.max(Math.abs(worldX - playerX), Math.abs(worldY - playerY));

                    // 1. Game Objects
                    Object gObjsObj = invokeMethodQuietly(tile, "getGameObjects");
                    if (gObjsObj instanceof Object[]) {
                        for (Object go : (Object[]) gObjsObj) {
                            if (go != null) scanSceneObject(client, go, dist, worldX, worldY, playerX, playerY);
                        }
                    }

                    // 2. Wall Objects
                    Object wall = invokeMethodQuietly(tile, "getWallObject");
                    if (wall != null) scanSceneObject(client, wall, dist, worldX, worldY, playerX, playerY);

                    // 3. Ground Objects
                    Object groundObj = invokeMethodQuietly(tile, "getGroundObject");
                    if (groundObj != null) scanSceneObject(client, groundObj, dist, worldX, worldY, playerX, playerY);

                    // 4. Decorative Objects
                    Object decObj = invokeMethodQuietly(tile, "getDecorativeObject");
                    if (decObj != null) scanSceneObject(client, decObj, dist, worldX, worldY, playerX, playerY);

                    // 5. Ground Items
                    Object gItemsObj = invokeMethodQuietly(tile, "getGroundItems");
                    if (gItemsObj instanceof Iterable) {
                        for (Object gi : (Iterable<?>) gItemsObj) {
                            if (gi == null) continue;
                            int gItemId = -1, gQty = 1;
                            Object idObj = invokeMethodQuietly(gi, "getId");
                            if (idObj instanceof Integer) gItemId = (Integer) idObj;
                            Object qtyObj = invokeMethodQuietly(gi, "getQuantity");
                            if (qtyObj instanceof Integer) gQty = (Integer) qtyObj;

                            if (gItemId > 0 && gItemId != 65535) {
                                String gName = resolveItemName(gItemId);
                                groundItemEntries.add(new SceneEntry(dist, gItemId, gName, worldX, worldY, String.valueOf(Math.max(1, gQty))));
                            }
                        }
                    }
                }
            }

            // 6. Scan NPCs for Bankers and Shops / Stores
            try {
                Object npcsObj = null;
                if (topView != null) {
                    npcsObj = invokeMethodQuietly(topView, "npcs");
                    if (npcsObj == null) npcsObj = invokeMethodQuietly(topView, "getNpcs");
                }
                if (npcsObj == null) {
                    npcsObj = invokeMethodQuietly(client, "getNpcs");
                    if (npcsObj == null) npcsObj = invokeMethodQuietly(client, "npcs");
                }
                if (npcsObj != null) {
                    Iterable<?> iterable = (npcsObj instanceof Iterable) ? (Iterable<?>) npcsObj : Arrays.asList((Object[]) npcsObj);
                    for (Object npc : iterable) {
                        if (npc == null) continue;
                        int npcId = -1;
                        Object idObj = invokeMethodQuietly(npc, "getId");
                        if (idObj instanceof Integer) npcId = (Integer) idObj;
                        String npcName = extractNpcName(client, npc, npcId);

                        Object npcComp = invokeMethodQuietly(npc, "getComposition");
                        if (npcComp != null) {
                            Object imp = invokeMethodQuietly(npcComp, "getImpostor");
                            if (imp != null) npcComp = imp;
                        }
                        if (npcComp == null && client != null && npcId > 0) {
                            try {
                                Method m = findMethod(client.getClass(), "getNpcDefinition", int.class);
                                if (m == null) m = findMethod(client.getClass(), "getNpcComposition", int.class);
                                if (m != null) npcComp = m.invoke(client, npcId);
                            } catch (Throwable ignored) {}
                        }

                        boolean isBankerNpc = hasAction(npcComp, "bank", "exchange", "collect");
                        boolean isShopNpc = hasAction(npcComp, "trade", "shop", "buy", "buy-items", "trade-with");

                        int[] npcCoords = getActorWorldCoordinates(client, npc, baseX, baseY, playerX, playerY);
                        int nx = npcCoords[0], ny = npcCoords[1];
                        int dist = Math.max(Math.abs(nx - playerX), Math.abs(ny - playerY));
                        String lowerNpc = (npcName != null) ? npcName.toLowerCase() : "";

                        if (isBankerNpc || lowerNpc.contains("banker") || lowerNpc.contains("bank ") || lowerNpc.contains("exchange clerk") || lowerNpc.contains("teller") || lowerNpc.contains("emerald benedict") || lowerNpc.contains("ghost banker") || lowerNpc.contains("gnome banker") || lowerNpc.contains("financial advisor")) {
                            String bName = (npcName != null && !npcName.startsWith("NPC #")) ? npcName + " (NPC)" : "Banker (NPC)";
                            bankEntries.add(new SceneEntry(dist, npcId, bName, nx, ny, "Banker"));
                        } else if (isShopNpc || lowerNpc.contains("shop") || lowerNpc.contains("store") || lowerNpc.contains("trader") || lowerNpc.contains("merchant") || lowerNpc.contains("seller") || lowerNpc.contains("dealer") || lowerNpc.contains("keeper") || lowerNpc.contains("assistant") || lowerNpc.contains("vendor") || lowerNpc.contains("aubury") || lowerNpc.contains("zaff") || lowerNpc.contains("horvik") || lowerNpc.contains("bob") || lowerNpc.contains("brian") || lowerNpc.contains("thessalia") || lowerNpc.contains("lowe") || lowerNpc.contains("grum") || lowerNpc.contains("gerrant") || lowerNpc.contains("betty") || lowerNpc.contains("jatix") || lowerNpc.contains("cassie") || lowerNpc.contains("wayne") || lowerNpc.contains("pekit") || lowerNpc.contains("rommik") || lowerNpc.contains("farrad") || lowerNpc.contains("herquin") || lowerNpc.contains("wyd") || lowerNpc.contains("barker") || lowerNpc.contains("baker") || lowerNpc.contains("silk merchant") || lowerNpc.contains("fur trader") || lowerNpc.contains("gem merchant") || lowerNpc.contains("bartender") || lowerNpc.contains("barman") || lowerNpc.contains("waitress") || lowerNpc.contains("apothecary")) {
                            String sName = (npcName != null && !npcName.startsWith("NPC #")) ? npcName + " (NPC)" : "Merchant (NPC)";
                            shopEntries.add(new SceneEntry(dist, npcId, sName, nx, ny, "Shop"));
                        }
                    }
                }
            } catch (Throwable ignored) {}

            Collections.sort(treeEntries);
            Collections.sort(bankEntries);
            Collections.sort(shopEntries);
            Collections.sort(altarEntries);
            Collections.sort(rockEntries);
            Collections.sort(shortcutEntries);
            Collections.sort(obstacleEntries);
            Collections.sort(groundItemEntries);

            // Append Trees
            int maxTrees = Math.min(treeEntries.size(), 40);
            for (int i = 0; i < maxTrees; i++) {
                SceneEntry e = treeEntries.get(i);
                cachedSceneData.append("TREE[").append(i).append("]: ").append(e.id).append(",").append(e.name)
                    .append(",").append(e.dist).append(",").append(e.worldX).append(",").append(e.worldY)
                    .append(",").append(e.extra != null ? e.extra : "Available").append("\n");
            }
            cachedSceneData.append("TOTAL_TREES: ").append(maxTrees).append("\n");

            // Append Banks
            int maxBanks = Math.min(bankEntries.size(), 20);
            for (int i = 0; i < maxBanks; i++) {
                SceneEntry e = bankEntries.get(i);
                cachedSceneData.append("BANK_OBJ[").append(i).append("]: ").append(e.id).append(",").append(e.name)
                    .append(",").append(e.dist).append(",").append(e.worldX).append(",").append(e.worldY).append("\n");
            }
            cachedSceneData.append("TOTAL_BANKS: ").append(maxBanks).append("\n");
            if (!bankEntries.isEmpty()) {
                SceneEntry firstBank = bankEntries.get(0);
                cachedSceneData.append("NEAREST_BANK: ").append(firstBank.name).append("\n");
                cachedSceneData.append("NEAREST_BANK_DIST: ").append(firstBank.dist).append("\n");
                cachedSceneData.append("IN_BANK: ").append(firstBank.dist <= 16 ? "True" : "False").append("\n");
            }

            // Append Shops
            int maxShops = Math.min(shopEntries.size(), 20);
            for (int i = 0; i < maxShops; i++) {
                SceneEntry e = shopEntries.get(i);
                cachedSceneData.append("SHOP_OBJ[").append(i).append("]: ").append(e.id).append(",").append(e.name)
                    .append(",").append(e.dist).append(",").append(e.worldX).append(",").append(e.worldY).append("\n");
            }
            cachedSceneData.append("TOTAL_SHOPS: ").append(maxShops).append("\n");

            // Append Altars
            int maxAltars = Math.min(altarEntries.size(), 20);
            for (int i = 0; i < maxAltars; i++) {
                SceneEntry e = altarEntries.get(i);
                cachedSceneData.append("ALTAR_OBJ[").append(i).append("]: ").append(e.id).append(",").append(e.name)
                    .append(",").append(e.dist).append(",").append(e.worldX).append(",").append(e.worldY).append("\n");
            }
            cachedSceneData.append("TOTAL_ALTARS: ").append(maxAltars).append("\n");

            // Append Rocks
            int maxRocks = Math.min(rockEntries.size(), 30);
            for (int i = 0; i < maxRocks; i++) {
                SceneEntry e = rockEntries.get(i);
                cachedSceneData.append("ROCK_OBJ[").append(i).append("]: ").append(e.id).append(",").append(e.name)
                    .append(",").append(e.dist).append(",").append(e.worldX).append(",").append(e.worldY).append("\n");
            }
            cachedSceneData.append("TOTAL_ROCKS: ").append(maxRocks).append("\n");

            // Append Shortcuts & Obstacles
            int maxShortcuts = Math.min(shortcutEntries.size(), 25);
            for (int i = 0; i < maxShortcuts; i++) {
                SceneEntry e = shortcutEntries.get(i);
                cachedSceneData.append("SHORTCUT[").append(i).append("]: ").append(e.id).append(",").append(e.name)
                    .append(",").append(e.extra).append(",").append(e.dist).append(",").append(e.worldX).append(",").append(e.worldY).append("\n");
            }
            cachedSceneData.append("TOTAL_SHORTCUTS: ").append(maxShortcuts).append("\n");

            int maxObstacles = Math.min(obstacleEntries.size(), 25);
            for (int i = 0; i < maxObstacles; i++) {
                SceneEntry e = obstacleEntries.get(i);
                cachedSceneData.append("OBSTACLE[").append(i).append("]: ").append(e.id).append(",").append(e.name)
                    .append(",").append(e.extra).append(",").append(e.dist).append(",").append(e.worldX).append(",").append(e.worldY).append("\n");
            }
            cachedSceneData.append("TOTAL_OBSTACLES: ").append(maxObstacles).append("\n");

            // Append Ground Items
            int maxGroundItems = Math.min(groundItemEntries.size(), 40);
            for (int i = 0; i < maxGroundItems; i++) {
                SceneEntry e = groundItemEntries.get(i);
                cachedSceneData.append("GROUND_ITEM[").append(i).append("]: ").append(e.id).append(",").append(e.name)
                    .append(",").append(e.extra).append(",").append(e.dist).append(",").append(e.worldX).append(",").append(e.worldY).append("\n");
            }
            cachedSceneData.append("TOTAL_GROUND_ITEMS: ").append(maxGroundItems).append("\n");

            lastSceneScanTime = now;
            lastScenePlayerX = playerX;
            lastScenePlayerY = playerY;
            lastScenePlane = plane;

            data.append(cachedSceneData);
        } catch (Throwable ignored) {}
    }

    private static void scanSceneObject(Object client, Object obj, int dist, int worldX, int worldY, int playerX, int playerY) {
        int id = getObjectId(obj);
        if (id <= 0) return;

        Object comp = getObjectComposition(client, obj, id);
        if (comp != null) {
            Object imp = invokeMethodQuietly(comp, "getImpostor");
            if (imp != null) comp = imp;
        }

        String name = null;
        if (comp != null) {
            Object nameObj = invokeMethodQuietly(comp, "getName");
            if (nameObj instanceof String) {
                name = cleanName((String) nameObj);
            }
        }
        if (name == null || name.isEmpty() || "null".equalsIgnoreCase(name)) {
            name = extractObjectName(client, obj, id);
        }

        int actualWorldX = worldX;
        int actualWorldY = worldY;
        Object wp = invokeMethodQuietly(obj, "getWorldLocation");
        if (wp != null) {
            Object gx = invokeMethodQuietly(wp, "getX");
            Object gy = invokeMethodQuietly(wp, "getY");
            if (gx instanceof Integer && gy instanceof Integer && ((Integer) gx) > 0) {
                actualWorldX = (Integer) gx;
                actualWorldY = (Integer) gy;
            }
        }
        int actualDist = Math.max(Math.abs(actualWorldX - playerX), Math.abs(actualWorldY - playerY));

        boolean isKnownBank = KNOWN_BANK_OBJECT_IDS.contains(id);
        boolean hasBankAction = hasAction(comp, "bank", "open-bank", "exchange", "collect", "deposit");
        boolean hasShopAction = hasAction(comp, "trade", "shop", "buy", "buy-items", "trade-with", "value");
        boolean hasChopAction = hasAction(comp, "chop down", "chop", "cut", "pick-fruit", "pick");
        boolean hasMineAction = hasAction(comp, "mine", "prospect", "chip", "quarry");

        String lower = (name != null) ? name.toLowerCase() : "";

        if (hasChopAction || lower.contains("tree") || lower.contains("oak") || lower.contains("willow") || lower.contains("maple")
            || lower.contains("yew") || lower.contains("magic") || lower.contains("redwood") || lower.contains("teak") || lower.contains("mahogany")
            || lower.contains("sapling") || lower.contains("pine") || lower.contains("hollow") || lower.contains("achey") || lower.contains("juniper")
            || lower.contains("blisterwood") || lower.contains("sulliuscep") || lower.contains("roots") || lower.contains("evergreen") || lower.contains("bark") || lower.contains("tendril")) {
            String status = (lower.contains("stump") || lower.contains("depleted")) ? "Stump" : "Available";
            treeEntries.add(new SceneEntry(actualDist, id, (name != null && !name.isEmpty() && !name.startsWith("Object #")) ? name : "Tree", actualWorldX, actualWorldY, status));
        } else if (isKnownBank || hasBankAction || lower.contains("bank") || lower.contains("booth") || lower.contains("chest") || lower.contains("deposit box") || lower.contains("grand exchange") || lower.contains("vault") || lower.contains("counter") || lower.contains("teller") || lower.contains("desk") || lower.contains("safe")) {
            String bankName = (name != null && !name.startsWith("Object #")) ? name : "Bank Booth";
            bankEntries.add(new SceneEntry(actualDist, id, bankName, actualWorldX, actualWorldY, null));
        } else if (hasShopAction || lower.contains("shop") || lower.contains("store") || lower.contains("stall") || lower.contains("counter") || lower.contains("merchant") || lower.contains("trader") || lower.contains("market") || lower.contains("stand") || lower.contains("rack") || lower.contains("display") || lower.contains("cart") || lower.contains("vendor")) {
            String shopName = (name != null && !name.startsWith("Object #")) ? name : "Shop / Stall";
            shopEntries.add(new SceneEntry(actualDist, id, shopName, actualWorldX, actualWorldY, null));
        } else if (lower.contains("altar") || lower.contains("shrine") || lower.contains("pool") || lower.contains("font") || lower.contains("statue")) {
            altarEntries.add(new SceneEntry(actualDist, id, name, actualWorldX, actualWorldY, null));
        } else if (hasMineAction || lower.contains("rock") || lower.contains("ore") || lower.contains("vein") || lower.contains("mining") || lower.contains("deposit") || lower.contains("amethyst") || lower.contains("dense runestone") || lower.contains("sandstone") || lower.contains("granite") || lower.contains("clay") || lower.contains("salt") || lower.contains("basalt") || lower.contains("daeyalt") || lower.contains("volcanic ash") || lower.contains("coal") || lower.contains("mithril") || lower.contains("adamantite") || lower.contains("runite") || lower.contains("iron") || lower.contains("copper") || lower.contains("tin") || lower.contains("gold") || lower.contains("silver")) {
            String rockName = (name != null && !name.startsWith("Object #")) ? name : "Mining Rock";
            rockEntries.add(new SceneEntry(actualDist, id, rockName, actualWorldX, actualWorldY, null));
        } else if (lower.contains("shortcut") || lower.contains("underwall") || lower.contains("stepping stone") || lower.contains("gap") || lower.contains("crevice") || lower.contains("tunnel")) {
            shortcutEntries.add(new SceneEntry(actualDist, id, name, actualWorldX, actualWorldY, "1"));
        } else if (lower.contains("obstacle") || lower.contains("log balance") || lower.contains("rope") || lower.contains("pipe") || lower.contains("tightrope") || lower.contains("hurdle") || lower.contains("ledge") || lower.contains("climb") || lower.contains("plank") || lower.contains("zip line")) {
            obstacleEntries.add(new SceneEntry(actualDist, id, name, actualWorldX, actualWorldY, "Agility"));
        }
    }

    private static int getObjectId(Object obj) {
        if (obj == null) return -1;
        Object idObj = invokeMethodQuietly(obj, "getId");
        return (idObj instanceof Integer) ? (Integer) idObj : -1;
    }

    private static void processCombatAndEntities(Object client, Object player, String localPlayerName, int playerX, int playerY, int plane, StringBuilder data) {
        if (client == null) return;
        try {
            int baseX = 0, baseY = 0;
            try {
                Object bX = invokeMethodQuietly(client, "getBaseX");
                if (bX instanceof Integer) baseX = (Integer) bX;
                Object bY = invokeMethodQuietly(client, "getBaseY");
                if (bY instanceof Integer) baseY = (Integer) bY;
            } catch (Throwable ignored) {}

            boolean inCombat = false;
            boolean underAttack = false;
            String underAttackBy = "None";
            attackingEnemiesList.clear();

            Object directTarget = (player != null) ? invokeMethodQuietly(player, "getInteracting") : null;
            Object combatTargetActor = null;
            int targetIndex = -1;

            // 1. Process NPCs
            Object npcsObj = null;
            Object topView = invokeMethodQuietly(client, "getTopLevelWorldView");
            if (topView != null) {
                npcsObj = invokeMethodQuietly(topView, "npcs");
                if (npcsObj == null) npcsObj = invokeMethodQuietly(topView, "getNpcs");
            }
            if (npcsObj == null) {
                npcsObj = invokeMethodQuietly(client, "getNpcs");
                if (npcsObj == null) npcsObj = invokeMethodQuietly(client, "npcs");
            }

            int npcCount = 0;
            if (npcsObj != null) {
                Iterable<?> iterable = (npcsObj instanceof Iterable) ? (Iterable<?>) npcsObj : Arrays.asList((Object[]) npcsObj);
                for (Object npc : iterable) {
                    if (npc == null || npcCount >= 25) continue;

                    int npcId = -1;
                    Object idObj = invokeMethodQuietly(npc, "getId");
                    if (idObj instanceof Integer) npcId = (Integer) idObj;

                    String npcName = extractNpcName(client, npc, npcId);
                    int cbLevel = 0;
                    Object cbObj = invokeMethodQuietly(npc, "getCombatLevel");
                    if (cbObj instanceof Integer) cbLevel = (Integer) cbObj;

                    int anim = -1;
                    Object aObj = invokeMethodQuietly(npc, "getAnimation");
                    if (aObj instanceof Integer) anim = (Integer) aObj;

                    int hpRatio = -1;
                    Object hrObj = invokeMethodQuietly(npc, "getHealthRatio");
                    if (hrObj instanceof Integer) hpRatio = (Integer) hrObj;
                    int hpPct = (hpRatio >= 0) ? hpRatio : 100;

                    int[] npcCoords = getActorWorldCoordinates(client, npc, baseX, baseY, playerX, playerY);
                    int nx = npcCoords[0], ny = npcCoords[1];
                    int dist = Math.max(Math.abs(nx - playerX), Math.abs(ny - playerY));

                    boolean targetingMe = false;
                    boolean npcInCombat = false;
                    Object npcTarget = invokeMethodQuietly(npc, "getInteracting");
                    if (npcTarget != null) {
                        npcInCombat = true;
                        Object tn = invokeMethodQuietly(npcTarget, "getName");
                        String targetName = (tn instanceof String) ? cleanName((String) tn) : "None";
                        if (player != null && (npcTarget == player || npcTarget.equals(player) || (localPlayerName != null && !localPlayerName.isEmpty() && localPlayerName.equalsIgnoreCase(targetName)))) {
                            targetingMe = true;
                            underAttack = true;
                            if ("None".equals(underAttackBy)) underAttackBy = npcName;
                            if (!attackingEnemiesList.contains(npcName)) attackingEnemiesList.add(npcName);
                            inCombat = true;
                            if (combatTargetActor == null) {
                                combatTargetActor = npc;
                                targetIndex = npcCount;
                            }
                        }
                    }

                    if (directTarget != null && directTarget == npc) {
                        if (cbLevel > 0 || anim != -1 || targetingMe || hpPct < 100) {
                            combatTargetActor = npc;
                            targetIndex = npcCount;
                            inCombat = true;
                        }
                    }

                    // Format: <id>,<name>,<hp%>,<worldX>,<worldY>,<plane>,<dist>,<inCombat>,<anim>,<targetingMe>
                    data.append("NPC[").append(npcCount).append("]: ").append(npcId).append(",").append(npcName)
                        .append(",").append(hpPct).append(",").append(nx).append(",").append(ny)
                        .append(",").append(plane).append(",").append(dist)
                        .append(",").append(npcInCombat ? "True" : "False")
                        .append(",").append(anim).append(",").append(targetingMe ? "True" : "False").append("\n");
                    npcCount++;
                }
            }
            data.append("TOTAL_NPCS: ").append(npcCount).append("\n");

            // 2. Process Players
            Object playersObj = null;
            if (topView != null) {
                playersObj = invokeMethodQuietly(topView, "players");
                if (playersObj == null) playersObj = invokeMethodQuietly(topView, "getPlayers");
            }
            if (playersObj == null) {
                playersObj = invokeMethodQuietly(client, "getPlayers");
                if (playersObj == null) playersObj = invokeMethodQuietly(client, "players");
            }

            int playerCount = 0;
            if (playersObj != null) {
                Iterable<?> iterable = (playersObj instanceof Iterable) ? (Iterable<?>) playersObj : Arrays.asList((Object[]) playersObj);
                for (Object p : iterable) {
                    if (p == null || p == player || playerCount >= 20) continue;

                    String pName = "Player";
                    Object nameObj = invokeMethodQuietly(p, "getName");
                    if (nameObj instanceof String) pName = cleanName((String) nameObj);

                    int cb = 0;
                    Object cbObj = invokeMethodQuietly(p, "getCombatLevel");
                    if (cbObj instanceof Integer) cb = (Integer) cbObj;

                    int anim = -1;
                    Object animObj = invokeMethodQuietly(p, "getAnimation");
                    if (animObj instanceof Integer) anim = (Integer) animObj;

                    int[] pCoords = getActorWorldCoordinates(client, p, baseX, baseY, playerX, playerY);
                    int px = pCoords[0], py = pCoords[1];
                    int dist = Math.max(Math.abs(px - playerX), Math.abs(py - playerY));

                    String interacting = "None";
                    boolean targetingMe = false;
                    Object inter = invokeMethodQuietly(p, "getInteracting");
                    if (inter != null) {
                        Object inName = invokeMethodQuietly(inter, "getName");
                        if (inName instanceof String) interacting = cleanName((String) inName);
                        if (player != null && (inter == player || inter.equals(player) || (localPlayerName != null && !localPlayerName.isEmpty() && localPlayerName.equalsIgnoreCase(interacting)))) {
                            targetingMe = true;
                            underAttack = true;
                            if ("None".equals(underAttackBy)) underAttackBy = pName;
                            if (!attackingEnemiesList.contains(pName)) attackingEnemiesList.add(pName);
                            inCombat = true;
                            if (combatTargetActor == null) {
                                combatTargetActor = p;
                                targetIndex = playerCount;
                            }
                        }
                    }

                    if (directTarget != null && directTarget == p) {
                        combatTargetActor = p;
                        targetIndex = playerCount;
                        inCombat = true;
                    }

                    data.append("PLAYER[").append(playerCount).append("]: ").append(pName).append(",").append(cb)
                        .append(",").append(px).append(",").append(py).append(",").append(plane)
                        .append(",").append(dist).append(",").append(anim).append(",").append(interacting).append("\n");
                    playerCount++;
                }
            }
            data.append("TOTAL_PLAYERS: ").append(playerCount).append("\n");

            // 3. Resolve and Stream Unified Combat State
            if (combatTargetActor != null) {
                inCombat = true;
                String clName = combatTargetActor.getClass().getName().toLowerCase();
                boolean isPlr = clName.contains("player");

                String tName = "None";
                int tCb = 0;
                int tAnim = -1;
                int tPose = -1;
                int tDist = 0;
                String tHpStr = "100%";

                Object nObj = invokeMethodQuietly(combatTargetActor, "getName");
                if (nObj instanceof String) tName = cleanName((String) nObj);
                if ((tName.isEmpty() || "None".equalsIgnoreCase(tName) || "null".equalsIgnoreCase(tName)) && !isPlr) {
                    int cid = -1;
                    Object idObj = invokeMethodQuietly(combatTargetActor, "getId");
                    if (idObj instanceof Integer) cid = (Integer) idObj;
                    tName = extractNpcName(client, combatTargetActor, cid);
                }

                Object cbObj = invokeMethodQuietly(combatTargetActor, "getCombatLevel");
                if (cbObj instanceof Integer) tCb = (Integer) cbObj;

                Object animObj = invokeMethodQuietly(combatTargetActor, "getAnimation");
                if (animObj instanceof Integer) tAnim = (Integer) animObj;

                Object poseObj = invokeMethodQuietly(combatTargetActor, "getPoseAnimation");
                if (poseObj instanceof Integer) tPose = (Integer) poseObj;

                Object hrObj = invokeMethodQuietly(combatTargetActor, "getHealthRatio");
                if (hrObj instanceof Integer && ((Integer) hrObj) >= 0) {
                    tHpStr = hrObj + "%";
                }

                int[] tCoords = getActorWorldCoordinates(client, combatTargetActor, baseX, baseY, playerX, playerY);
                tDist = Math.max(Math.abs(tCoords[0] - playerX), Math.abs(tCoords[1] - playerY));

                String overhead = extractOverheadPrayer(combatTargetActor);

                lastCombatTarget = tName;
                lastCombatTargetIndex = targetIndex;
                lastCombatTargetLevel = tCb;
                lastCombatTargetHp = tHpStr;
                lastCombatTargetDistance = tDist;
                lastCombatTargetPrayer = overhead;
                lastCombatTargetAnim = tAnim;
                lastCombatTargetPose = tPose;

                if (isPlr) {
                    lastCombatTargetGear.clear();
                    extractPlayerEquipment(combatTargetActor, lastCombatTargetGear, lastEnemyEquipIds, lastEnemyEquipNames);
                    for (int s = 0; s < 14; s++) {
                        if (lastEnemyEquipIds[s] > 0) {
                            data.append("ENEMY_EQUIP[").append(s).append("]: ").append(lastEnemyEquipIds[s]).append(",").append(lastEnemyEquipNames[s]).append("\n");
                        } else {
                            data.append("ENEMY_EQUIP[").append(s).append("]: EMPTY\n");
                        }
                    }
                    String weapon = (lastEnemyEquipNames[3] != null && !"EMPTY".equals(lastEnemyEquipNames[3])) ? lastEnemyEquipNames[3] : "None";
                    lastCombatTargetWeapon = weapon;
                    data.append("COMBAT_ENEMY_WEAPON: ").append(weapon).append("\n");
                    data.append("ENEMY_WEAPON: ").append(weapon).append("\n");
                    if (!lastCombatTargetGear.isEmpty()) {
                        String gearStr = String.join(", ", lastCombatTargetGear);
                        data.append("COMBAT_ENEMY_GEAR: ").append(gearStr).append("\n");
                        data.append("ENEMY_GEAR: ").append(gearStr).append("\n");
                    } else {
                        data.append("COMBAT_ENEMY_GEAR: None\n");
                        data.append("ENEMY_GEAR: None\n");
                    }
                } else {
                    for (int s = 0; s < 14; s++) {
                        data.append("ENEMY_EQUIP[").append(s).append("]: EMPTY\n");
                    }
                    lastCombatTargetWeapon = "None";
                    data.append("COMBAT_ENEMY_WEAPON: None\n");
                    data.append("ENEMY_WEAPON: None\n");
                    data.append("COMBAT_ENEMY_GEAR: None\n");
                    data.append("ENEMY_GEAR: None\n");
                }

                data.append("COMBAT_TARGET: ").append(tName).append("\n");
                data.append("COMBAT_TARGET_INDEX: ").append(targetIndex).append("\n");
                data.append("COMBAT_TARGET_LEVEL: ").append(tCb).append("\n");
                data.append("COMBAT_TARGET_HP: ").append(tHpStr).append("\n");
                data.append("COMBAT_TARGET_DISTANCE: ").append(tDist).append("\n");
                data.append("COMBAT_ENEMY_PRAYER: ").append(overhead).append("\n");
                data.append("ENEMY_PRAYER: ").append(overhead).append("\n");
                data.append("COMBAT_ENEMY_ANIMATION: ").append(tAnim).append("\n");
                data.append("TARGET_ANIMATION: ").append(tAnim).append("\n");
                data.append("COMBAT_ENEMY_POSE: ").append(tPose).append("\n");
                data.append("COMBAT: IN_COMBAT: True | TARGET: ").append(tName).append(" | HP: ").append(tHpStr).append(" | LEVEL: ").append(tCb).append(" | UNDER_ATTACK: ").append(underAttack ? "True" : "False").append("\n");
            } else {
                for (int s = 0; s < 14; s++) {
                    data.append("ENEMY_EQUIP[").append(s).append("]: EMPTY\n");
                }
                data.append("COMBAT_TARGET: None\n");
                data.append("COMBAT_TARGET_INDEX: -1\n");
                data.append("COMBAT_TARGET_LEVEL: 0\n");
                data.append("COMBAT_TARGET_HP: None\n");
                data.append("COMBAT_TARGET_DISTANCE: 0\n");
                data.append("COMBAT_ENEMY_PRAYER: None\n");
                data.append("ENEMY_PRAYER: None\n");
                data.append("COMBAT_ENEMY_WEAPON: None\n");
                data.append("ENEMY_WEAPON: None\n");
                data.append("COMBAT_ENEMY_GEAR: None\n");
                data.append("ENEMY_GEAR: None\n");
                data.append("COMBAT: IN_COMBAT: ").append(inCombat ? "True" : "False").append(" | TARGET: None | HP: None | LEVEL: 0 | UNDER_ATTACK: ").append(underAttack ? "True" : "False").append("\n");
            }

            // Attacker state
            data.append("COMBAT_UNDER_ATTACK: ").append(underAttack ? "True" : "False").append("\n");
            data.append("UNDER_ATTACK: ").append(underAttack ? "True" : "False").append("\n");
            data.append("UNDER_ATTACK_BY: ").append(underAttackBy).append("\n");
            data.append("COMBAT_ATTACKING_ENEMIES: ").append(!attackingEnemiesList.isEmpty() ? String.join(", ", attackingEnemiesList) : "None").append("\n");
            data.append("IN_COMBAT: ").append(inCombat ? "True" : "False").append("\n");

        } catch (Throwable ignored) {}
    }

    private static String extractActiveTab(Object client) {
        if (client == null) return "Inventory";
        try {
            Object tabObj = invokeMethodQuietly(client, "getVarcIntValue", 171);
            if (tabObj instanceof Integer) {
                int tab = (Integer) tabObj;
                switch (tab) {
                    case 0: return "Combat";
                    case 1: return "Skills";
                    case 2: return "Quests";
                    case 3: return "Inventory";
                    case 4: return "Equipment";
                    case 5: return "Prayer";
                    case 6: return "Magic";
                    case 7: return "Clan";
                    case 8: return "Account";
                    case 9: return "Friends";
                    case 10: return "Logout";
                    case 11: return "Settings";
                    case 12: return "Emotes";
                    case 13: return "Music";
                }
            }
        } catch (Throwable ignored) {}
        return "Inventory";
    }

    private static String extractOverheadPrayer(Object actor) {
        if (actor == null) return "None";
        try {
            Object icon = invokeMethodQuietly(actor, "getOverheadIcon");
            if (icon == null) icon = invokeMethodQuietly(actor, "getHeadIcon");
            if (icon instanceof Enum) {
                String name = ((Enum<?>) icon).name().toUpperCase();
                if (name.contains("MELEE")) return "Protect from Melee";
                if (name.contains("RANG") || name.contains("MISSILE")) return "Protect from Missiles";
                if (name.contains("MAGIC") || name.contains("MAGE")) return "Protect from Magic";
                if (name.contains("SMITE")) return "Smite";
                if (name.contains("REDEMPTION")) return "Redemption";
                if (name.contains("RETRIBUTION")) return "Retribution";
                return name;
            }
        } catch (Throwable ignored) {}
        return "None";
    }

    private static void extractPlayerEquipment(Object targetPlayer, List<String> outGearNames, int[] outEquipIds, String[] outEquipNames) {
        if (targetPlayer == null) return;
        try {
            Object comp = invokeMethodQuietly(targetPlayer, "getPlayerComposition");
            if (comp == null) return;

            Object equipIdsObj = invokeMethodQuietly(comp, "getEquipmentIds");
            if (equipIdsObj instanceof int[]) {
                int[] rawIds = (int[]) equipIdsObj;
                for (int slot = 0; slot < 14; slot++) {
                    int rawId = (slot < rawIds.length) ? rawIds[slot] : -1;
                    int itemId = rawId;
                    if (itemId > 512) itemId -= 512;
                    if (itemId > 0 && itemId != 65535) {
                        outEquipIds[slot] = itemId;
                        String itmName = resolveItemName(itemId);
                        outEquipNames[slot] = itmName;
                        outGearNames.add(itmName);
                    } else {
                        outEquipIds[slot] = -1;
                        outEquipNames[slot] = "EMPTY";
                    }
                }
            }
        } catch (Throwable ignored) {}
    }

    private static void processDialogue(Object client, StringBuilder data) {
        if (client == null) return;
        try {
            // 1. NPC Dialogue (Widget 231)
            Object npcDialogWidget = getWidget(client, 231, 6);
            if (npcDialogWidget == null) npcDialogWidget = getWidget(client, 231, 5);
            if (isWidgetVisible(npcDialogWidget)) {
                Object textObj = invokeMethodQuietly(npcDialogWidget, "getText");
                if (textObj instanceof String) {
                    String text = cleanName((String) textObj);
                    if (!text.isEmpty()) {
                        Object npcTitle = getWidget(client, 231, 4);
                        String npcName = "NPC";
                        if (npcTitle != null) {
                            Object nObj = invokeMethodQuietly(npcTitle, "getText");
                            if (nObj instanceof String) npcName = cleanName((String) nObj);
                        }
                        data.append("DIALOG_ACTIVE: True\n");
                        data.append("DIALOG_TYPE: NPC\n");
                        data.append("DIALOG_TITLE: ").append(npcName).append("\n");
                        data.append("DIALOG_NPC_NAME: ").append(npcName).append("\n");
                        data.append("DIALOG_TEXT: ").append(text).append("\n");
                        data.append("DIALOG_OPTIONS: None\n");
                        return;
                    }
                }
            }

            // 2. Player Dialogue (Widget 217)
            Object playerDialogWidget = getWidget(client, 217, 6);
            if (playerDialogWidget == null) playerDialogWidget = getWidget(client, 217, 5);
            if (isWidgetVisible(playerDialogWidget)) {
                Object textObj = invokeMethodQuietly(playerDialogWidget, "getText");
                if (textObj instanceof String) {
                    String text = cleanName((String) textObj);
                    if (!text.isEmpty()) {
                        Object playerTitle = getWidget(client, 217, 4);
                        String pTitle = "Player";
                        if (playerTitle != null) {
                            Object nObj = invokeMethodQuietly(playerTitle, "getText");
                            if (nObj instanceof String) pTitle = cleanName((String) nObj);
                        }
                        data.append("DIALOG_ACTIVE: True\n");
                        data.append("DIALOG_TYPE: Player\n");
                        data.append("DIALOG_TITLE: ").append(pTitle).append("\n");
                        data.append("DIALOG_TEXT: ").append(text).append("\n");
                        data.append("DIALOG_OPTIONS: None\n");
                        return;
                    }
                }
            }

            // 3. Dialogue Options (Widget 219)
            Object optionsWidget = getWidget(client, 219, 1);
            if (isWidgetVisible(optionsWidget)) {
                Object childrenObj = invokeMethodQuietly(optionsWidget, "getChildren");
                if (childrenObj == null) childrenObj = invokeMethodQuietly(optionsWidget, "getDynamicChildren");
                if (childrenObj instanceof Object[]) {
                    Object[] children = (Object[]) childrenObj;
                    StringBuilder optionsSb = new StringBuilder();
                    String title = "Select an Option";
                    for (int i = 0; i < children.length; i++) {
                        Object child = children[i];
                        if (child == null) continue;
                        Object tObj = invokeMethodQuietly(child, "getText");
                        if (tObj instanceof String) {
                            String optText = cleanName((String) tObj);
                            if (optText.isEmpty() || optText.equalsIgnoreCase("Click here to continue")) continue;
                            if (i == 0 || (i == 1 && optionsSb.length() == 0)) {
                                title = optText;
                            } else {
                                if (optionsSb.length() > 0) optionsSb.append("|");
                                optionsSb.append(optText);
                            }
                        }
                    }
                    if (optionsSb.length() > 0) {
                        data.append("DIALOG_ACTIVE: True\n");
                        data.append("DIALOG_TYPE: Options\n");
                        data.append("DIALOG_TITLE: ").append(title).append("\n");
                        data.append("DIALOG_TEXT: ").append(title).append("\n");
                        data.append("DIALOG_OPTIONS: ").append(optionsSb.toString()).append("\n");
                        return;
                    }
                }
            }

            // 4. Message Box (Widget 229)
            Object msgWidget = getWidget(client, 229, 1);
            if (isWidgetVisible(msgWidget)) {
                Object textObj = invokeMethodQuietly(msgWidget, "getText");
                if (textObj instanceof String) {
                    String text = cleanName((String) textObj);
                    if (!text.isEmpty()) {
                        data.append("DIALOG_ACTIVE: True\n");
                        data.append("DIALOG_TYPE: Message\n");
                        data.append("DIALOG_TITLE: Message\n");
                        data.append("DIALOG_TEXT: ").append(text).append("\n");
                        data.append("DIALOG_OPTIONS: None\n");
                        return;
                    }
                }
            }

            // 5. Sprite / Item Message Box (Widget 193)
            Object spriteMsgWidget = getWidget(client, 193, 2);
            if (isWidgetVisible(spriteMsgWidget)) {
                Object textObj = invokeMethodQuietly(spriteMsgWidget, "getText");
                if (textObj instanceof String) {
                    String text = cleanName((String) textObj);
                    if (!text.isEmpty()) {
                        data.append("DIALOG_ACTIVE: True\n");
                        data.append("DIALOG_TYPE: Item Message\n");
                        data.append("DIALOG_TITLE: Message\n");
                        data.append("DIALOG_TEXT: ").append(text).append("\n");
                        data.append("DIALOG_OPTIONS: None\n");
                        return;
                    }
                }
            }

            // No active dialogue
            data.append("DIALOG_ACTIVE: False\n");
            data.append("DIALOG_TYPE: None\n");
            data.append("DIALOG_TITLE: None\n");
            data.append("DIALOG_TEXT: None\n");
            data.append("DIALOG_OPTIONS: None\n");
        } catch (Throwable ignored) {}
    }

    private static void processPrayers(Object client, StringBuilder data) {
        if (client == null) return;
        try {
            Class<?> prayerEnum = null;
            try {
                prayerEnum = Class.forName("net.runelite.api.Prayer", true, client.getClass().getClassLoader());
            } catch (Throwable ignored) {
                try {
                    prayerEnum = Class.forName("net.runelite.api.Prayer");
                } catch (Throwable ignored2) {}
            }

            Method isPrayerActive = null;
            if (prayerEnum != null && prayerEnum.isEnum()) {
                isPrayerActive = findMethod(client.getClass(), "isPrayerActive", prayerEnum);
            }

            int prayerVarp = getVarpValue(client, 83);
            List<String> activePrayersList = new ArrayList<>(8);

            for (int bit = 0; bit < STANDARD_PRAYERS_MAP.length; bit++) {
                Object[] row = STANDARD_PRAYERS_MAP[bit];
                String formattedName = (String) row[0];
                String enumName = (String) row[1];
                int varbitId = (Integer) row[2];

                boolean isActive = false;

                // 1. Check Varp 83 bitmask (OSRS native active prayers bitmask)
                if (prayerVarp > 0 && (prayerVarp & (1 << bit)) != 0) {
                    isActive = true;
                }

                // 2. Check RuneLite isPrayerActive
                if (!isActive && isPrayerActive != null && prayerEnum != null) {
                    try {
                        for (Object p : prayerEnum.getEnumConstants()) {
                            if (p instanceof Enum && ((Enum<?>) p).name().equalsIgnoreCase(enumName)) {
                                Object active = isPrayerActive.invoke(client, p);
                                if (Boolean.TRUE.equals(active)) {
                                    isActive = true;
                                    break;
                                }
                            }
                        }
                    } catch (Throwable ignored) {}
                }

                // 3. Check Varbit
                if (!isActive && varbitId > 0) {
                    int vb = getVarbitValue(client, varbitId);
                    if (vb == 1) isActive = true;
                }

                if (isActive) {
                    activePrayersList.add(formattedName);
                    data.append("PRAYER[").append(formattedName).append("]: Active\n");
                    data.append("PRAYER[").append(enumName).append("]: Active\n");
                } else {
                    data.append("PRAYER[").append(formattedName).append("]: Inactive\n");
                    data.append("PRAYER[").append(enumName).append("]: Inactive\n");
                }
            }

            // Quick Prayers
            int quickPrayerVarb = getVarbitValue(client, 4103);
            int quickPrayerVarp = getVarpValue(client, 84);
            boolean quickPrayerActive = (quickPrayerVarb == 1 || quickPrayerVarp > 0);
            data.append("QUICK_PRAYER: ").append(quickPrayerActive ? "True" : "False").append("\n");

            // Summary
            if (!activePrayersList.isEmpty()) {
                StringBuilder ap = new StringBuilder();
                for (int i = 0; i < activePrayersList.size(); i++) {
                    if (i > 0) ap.append(", ");
                    ap.append(activePrayersList.get(i));
                }
                data.append("ACTIVE_PRAYERS: ").append(ap).append("\n");
                data.append("ACTIVE_PRAYER_COUNT: ").append(activePrayersList.size()).append("\n");
            } else {
                data.append("ACTIVE_PRAYERS: None\n");
                data.append("ACTIVE_PRAYER_COUNT: 0\n");
            }
        } catch (Throwable ignored) {}
    }

    private static Object getWidget(Object client, int group, int child) {
        if (client == null) return null;
        int packedId = (group << 16) | (child & 0xFFFF);
        try {
            Method m = findMethod(client.getClass(), "getWidget", int.class);
            if (m != null) {
                Object res = m.invoke(client, packedId);
                if (res != null) return res;
            }
        } catch (Throwable ignored) {}
        try {
            Method m = findMethod(client.getClass(), "getWidget", int.class, int.class);
            if (m != null) {
                Object res = m.invoke(client, group, child);
                if (res != null) return res;
            }
        } catch (Throwable ignored) {}
        try {
            Method m = findMethod(client.getClass(), "getWidgets");
            if (m != null) {
                Object res = m.invoke(client);
                if (res instanceof Object[][]) {
                    Object[][] widgets = (Object[][]) res;
                    if (group >= 0 && group < widgets.length && widgets[group] != null) {
                        if (child >= 0 && child < widgets[group].length) {
                            return widgets[group][child];
                        }
                    }
                }
            }
        } catch (Throwable ignored) {}
        return null;
    }

    private static boolean isWidgetVisible(Object widget) {
        if (widget == null) return false;
        try {
            Object hidden = invokeMethodQuietly(widget, "isHidden");
            if (hidden instanceof Boolean && ((Boolean) hidden)) return false;
            Object selfHidden = invokeMethodQuietly(widget, "isSelfHidden");
            if (selfHidden instanceof Boolean && ((Boolean) selfHidden)) return false;
            return true;
        } catch (Throwable ignored) {}
        return false;
    }

    private static int[] getActorWorldCoordinates(Object client, Object actor, int baseX, int baseY, int fallbackX, int fallbackY) {
        if (actor == null) return new int[]{fallbackX, fallbackY};
        try {
            Object wp = invokeMethodQuietly(actor, "getWorldLocation");
            if (wp != null) {
                Object gx = invokeMethodQuietly(wp, "getX");
                Object gy = invokeMethodQuietly(wp, "getY");
                if (gx instanceof Integer && gy instanceof Integer && ((Integer) gx) > 0 && ((Integer) gy) > 0) {
                    return new int[]{(Integer) gx, (Integer) gy};
                }
            }
        } catch (Throwable ignored) {}
        try {
            Object lp = invokeMethodQuietly(actor, "getLocalLocation");
            if (lp != null) {
                Object lx = invokeMethodQuietly(lp, "getX");
                Object ly = invokeMethodQuietly(lp, "getY");
                if (lx instanceof Integer && ly instanceof Integer) {
                    int sceneX = ((Integer) lx) >> 7;
                    int sceneY = ((Integer) ly) >> 7;
                    if (baseX > 0 && baseY > 0) {
                        return new int[]{baseX + sceneX, baseY + sceneY};
                    }
                }
            }
        } catch (Throwable ignored) {}
        return new int[]{fallbackX, fallbackY};
    }

    private static int getVarbitValue(Object client, int varbitId) {
        if (client == null || varbitId < 0) return -1;
        try {
            Method m = findMethod(client.getClass(), "getVarbitValue", int.class);
            if (m != null) {
                Object res = m.invoke(client, varbitId);
                if (res instanceof Number) return ((Number) res).intValue();
            }
        } catch (Throwable ignored) {}
        return -1;
    }

    private static int getVarpValue(Object client, int varpId) {
        if (client == null || varpId < 0) return -1;
        try {
            Method m = findMethod(client.getClass(), "getVarpValue", int.class);
            if (m != null) {
                Object res = m.invoke(client, varpId);
                if (res instanceof Number) return ((Number) res).intValue();
            }
            Method mVarps = findMethod(client.getClass(), "getVarps");
            if (mVarps != null) {
                Object res = mVarps.invoke(client);
                if (res instanceof int[]) {
                    int[] varps = (int[]) res;
                    if (varpId < varps.length) return varps[varpId];
                }
            }
        } catch (Throwable ignored) {}
        return -1;
    }

    private static void processBuffsAndStatusTimers(Object client, StringBuilder data) {
        if (client == null) return;
        try {
            // 1. Stamina Potion
            int stamina = getVarbitValue(client, 25);
            if (stamina >= 0) {
                data.append("BUFF_STAMINA: ").append(stamina).append("\n");
            }

            // 2. Antifire Potions
            int antifire = getVarbitValue(client, 3981);
            if (antifire < 0) antifire = getVarpValue(client, 3975);
            if (antifire >= 0) {
                data.append("BUFF_ANTIFIRE: ").append(antifire).append("\n");
            }

            int superAntifire = getVarbitValue(client, 6101);
            if (superAntifire >= 0) {
                data.append("BUFF_SUPER_ANTIFIRE: ").append(superAntifire).append("\n");
            }

            // 3. Overload (NMZ: 3955, CoX: 5418)
            int ovlNmz = getVarbitValue(client, 3955);
            int ovlCox = getVarbitValue(client, 5418);
            int ovl = Math.max(ovlNmz >= 0 ? ovlNmz : 0, ovlCox >= 0 ? ovlCox : 0);
            if (ovl > 0) {
                data.append("BUFF_OVERLOAD: ").append(ovl).append("\n");
            } else if (ovlNmz >= 0 || ovlCox >= 0) {
                data.append("BUFF_OVERLOAD: 0\n");
            }

            // 4. Divine Potions
            int dSuper = getVarbitValue(client, 8429);
            int dRange = getVarbitValue(client, 8430);
            int dMage = getVarbitValue(client, 8431);
            int dBast = getVarbitValue(client, 8432);
            int dBattle = getVarbitValue(client, 8433);
            int divine = Math.max(dSuper, Math.max(dRange, Math.max(dMage, Math.max(dBast, dBattle))));
            if (divine > 0) {
                data.append("BUFF_DIVINE: ").append(divine).append("\n");
            } else if (dSuper >= 0) {
                data.append("BUFF_DIVINE: 0\n");
            }

            // 5. Imbued Heart / Saturated Heart Cooldown
            int heart = getVarbitValue(client, 5440);
            if (heart < 0) heart = getVarpValue(client, 1243);
            if (heart >= 0) {
                data.append("BUFF_IMBUED_HEART: ").append(heart).append("\n");
            }

            // 6. Prayer Enhance (CoX)
            int prayEnhance = getVarbitValue(client, 5451);
            if (prayEnhance >= 0) {
                data.append("BUFF_PRAYER_ENHANCE: ").append(prayEnhance).append("\n");
            }

            // 7. Charge Spell
            int charge = getVarbitValue(client, 272);
            if (charge >= 0) {
                data.append("BUFF_CHARGE: ").append(charge).append("\n");
            }

            // 8. Poison & Venom Status (Varp 102)
            int poisonVarp = getVarpValue(client, 102);
            if (poisonVarp >= 1000000) {
                int venomDmg = 6 + (int) Math.min(14, ((poisonVarp - 1000000) / 5) * 2);
                data.append("POISON_STATE: Venomed\n");
                data.append("POISON_DAMAGE: ").append(venomDmg).append("\n");
                data.append("POISON_IMMUNITY_TICKS: 0\n");
            } else if (poisonVarp > 0) {
                int poisonDmg = (int) Math.ceil(poisonVarp / 5.0);
                data.append("POISON_STATE: Poisoned\n");
                data.append("POISON_DAMAGE: ").append(poisonDmg).append("\n");
                data.append("POISON_IMMUNITY_TICKS: 0\n");
            } else if (poisonVarp < 0) {
                int immunityTicks = -poisonVarp;
                data.append("POISON_STATE: Immune\n");
                data.append("POISON_DAMAGE: 0\n");
                data.append("POISON_IMMUNITY_TICKS: ").append(immunityTicks).append("\n");
            } else if (poisonVarp == 0) {
                data.append("POISON_STATE: Normal\n");
                data.append("POISON_DAMAGE: 0\n");
                data.append("POISON_IMMUNITY_TICKS: 0\n");
            }
        } catch (Throwable ignored) {}
    }

    private static String resolveItemName(int id) {
        if (id <= 0 || id == 65535) return "Empty";
        String cached = ITEM_NAME_CACHE.get(id);
        if (cached != null) return cached;

        if (runeLiteItemManager != null) {
            try {
                Method m = findMethod(runeLiteItemManager.getClass(), "getItemComposition", int.class);
                if (m != null) {
                    Object comp = m.invoke(runeLiteItemManager, id);
                    if (comp != null) {
                        Object nameObj = invokeMethodQuietly(comp, "getName");
                        if (nameObj instanceof String) {
                            String name = cleanName((String) nameObj);
                            if (!name.isEmpty() && !"null".equalsIgnoreCase(name)) {
                                ITEM_NAME_CACHE.put(id, name);
                                return name;
                            }
                        }
                    }
                }
            } catch (Throwable ignored) {}
        }

        if (runeLiteClient != null) {
            try {
                Method m = findMethod(runeLiteClient.getClass(), "getItemDefinition", int.class);
                if (m == null) m = findMethod(runeLiteClient.getClass(), "getItemComposition", int.class);
                if (m != null) {
                    Object comp = m.invoke(runeLiteClient, id);
                    if (comp != null) {
                        Object nameObj = invokeMethodQuietly(comp, "getName");
                        if (nameObj instanceof String) {
                            String name = cleanName((String) nameObj);
                            if (!name.isEmpty() && !"null".equalsIgnoreCase(name)) {
                                ITEM_NAME_CACHE.put(id, name);
                                return name;
                            }
                        }
                    }
                }
            } catch (Throwable ignored) {}
        }

        return "Item #" + id;
    }

    private static Object getObjectComposition(Object client, Object obj, int id) {
        if (obj != null) {
            try {
                Object comp = invokeMethodQuietly(obj, "getComposition");
                if (comp != null) return comp;
            } catch (Throwable ignored) {}
        }
        if (client != null && id > 0) {
            try {
                Method m = findMethod(client.getClass(), "getObjectComposition", int.class);
                if (m == null) m = findMethod(client.getClass(), "getObjectDefinition", int.class);
                if (m != null) {
                    return m.invoke(client, id);
                }
            } catch (Throwable ignored) {}
        }
        return null;
    }

    private static boolean hasAction(Object comp, String... targetActions) {
        if (comp == null || targetActions == null) return false;
        try {
            Object actionsObj = invokeMethodQuietly(comp, "getActions");
            if (actionsObj instanceof String[]) {
                for (String act : (String[]) actionsObj) {
                    if (act == null) continue;
                    String actLower = act.toLowerCase();
                    for (String t : targetActions) {
                        if (actLower.contains(t.toLowerCase())) return true;
                    }
                }
            }
        } catch (Throwable ignored) {}
        return false;
    }

    public static String resolveLocationName(int x, int y, int plane, int regionId) {
        if (x <= 0 || y <= 0) return "Unknown";

        // 1. Specific Coordinate Bounding Boxes
        // Grand Exchange
        if (x >= 3140 && x <= 3190 && y >= 3470 && y <= 3515) return "Grand Exchange";

        // Varrock Areas
        if (x >= 3160 && x <= 3205 && y >= 3400 && y <= 3460) return "West Varrock";
        if (x >= 3235 && x <= 3285 && y >= 3400 && y <= 3460) return "East Varrock";
        if (x >= 3206 && x <= 3234 && y >= 3415 && y <= 3445) return "Varrock Square";
        if (x >= 3190 && x <= 3235 && y >= 3460 && y <= 3500) return "Varrock Palace";
        if (x >= 3200 && x <= 3250 && y >= 3370 && y <= 3414) return "South Varrock";
        if (x >= 3180 && x <= 3280 && y >= 9840 && y <= 9920) return "Varrock Sewers";
        if (x >= 3180 && x <= 3205 && y >= 3350 && y <= 3375) return "Champions' Guild";

        // Edgeville & Barbarian Village
        if (x >= 3070 && x <= 3120 && y >= 3460 && y <= 3520) return "Edgeville";
        if (x >= 3040 && x <= 3065 && y >= 3480 && y <= 3505) return "Edgeville Monastery";
        if (x >= 3080 && x <= 3140 && y >= 9840 && y <= 10000) return "Edgeville Dungeon";
        if (x >= 3070 && x <= 3115 && y >= 3410 && y <= 3455) return "Barbarian Village";

        // Lumbridge
        if (x >= 3210 && x <= 3235 && y >= 3210 && y <= 3230) return "Lumbridge Castle";
        if (x >= 3200 && x <= 3255 && y >= 3190 && y <= 3240) return "Lumbridge";
        if (x >= 3145 && x <= 3250 && y >= 3150 && y <= 3185) return "Lumbridge Swamp";
        if (x >= 3180 && x <= 3200 && y >= 3290 && y <= 3315) return "Lumbridge Windmill";

        // Draynor & Wizards Tower
        if (x >= 3075 && x <= 3120 && y >= 3220 && y <= 3270) return "Draynor Village";
        if (x >= 3080 && x <= 3130 && y >= 3330 && y <= 3380) return "Draynor Manor";
        if (x >= 3100 && x <= 3125 && y >= 3150 && y <= 3175) return "Wizards' Tower";

        // Al Kharid & Desert
        if (x >= 3265 && x <= 3320 && y >= 3140 && y <= 3200) return "Al Kharid";
        if (x >= 3280 && x <= 3310 && y >= 3150 && y <= 3180) return "Al Kharid Palace";
        if (x >= 3325 && x <= 3390 && y >= 3200 && y <= 3285) return "PvP Arena";
        if (x >= 3290 && x <= 3320 && y >= 3110 && y <= 3135) return "Shantay Pass";

        // Falador & Asgarnia
        if (x >= 2995 && x <= 3040 && y >= 3340 && y <= 3390) return "East Falador";
        if (x >= 2940 && x <= 2994 && y >= 3340 && y <= 3390) return "West Falador";
        if (x >= 2955 && x <= 3000 && y >= 3320 && y <= 3350) return "Falador Castle";
        if (x >= 2985 && x <= 3035 && y >= 3365 && y <= 3400) return "Falador Park";
        if (x >= 3010 && x <= 3060 && y >= 9690 && y <= 9750) return "Mining Guild";
        if (x >= 3000 && x <= 3070 && y >= 3200 && y <= 3265) return "Port Sarim";
        if (x >= 2935 && x <= 2985 && y >= 3195 && y <= 3250) return "Rimmington";
        if (x >= 2925 && x <= 2950 && y >= 3275 && y <= 3300) return "Crafting Guild";
        if (x >= 2870 && x <= 2940 && y >= 3420 && y <= 3480) return "Taverley";
        if (x >= 2815 && x <= 2940 && y >= 9740 && y <= 9860) return "Taverley Dungeon";
        if (x >= 2870 && x <= 2940 && y >= 3520 && y <= 3580) return "Burthorpe";
        if (x >= 2835 && x <= 2880 && y >= 3530 && y <= 3560) return "Warriors' Guild";
        if (x >= 3030 && x <= 3060 && y >= 4950 && y <= 4980) return "Rogues' Den";

        // Kandarin
        if (x >= 2790 && x <= 2860 && y >= 3415 && y <= 3460) return "Catherby";
        if (x >= 2690 && x <= 2750 && y >= 3460 && y <= 3510) return "Seers' Village";
        if (x >= 2745 && x <= 2780 && y >= 3490 && y <= 3525) return "Camelot Castle";
        if (x >= 2580 && x <= 2630 && y >= 3390 && y <= 3445) return "Fishing Guild";
        if (x >= 2655 && x <= 2678 && y >= 3415 && y <= 3445) return "Ranging Guild";
        if (x >= 2600 && x <= 2680 && y >= 3260 && y <= 3340) return "East Ardougne";
        if (x >= 2500 && x <= 2560 && y >= 3260 && y <= 3340) return "West Ardougne";
        if (x >= 2415 && x <= 2485 && y >= 3400 && y <= 3480) return "Tree Gnome Stronghold";
        if (x >= 2460 && x <= 2475 && y >= 3490 && y <= 3505) return "The Grand Tree";
        if (x >= 2520 && x <= 2550 && y >= 3160 && y <= 3190) return "Tree Gnome Village";
        if (x >= 2530 && x <= 2620 && y >= 3070 && y <= 3115) return "Yanille";
        if (x >= 2435 && x <= 2465 && y >= 3080 && y <= 3110) return "Castle Wars";

        // Karamja & Mor Ul Rek
        if (x >= 2910 && x <= 2960 && y >= 3130 && y <= 3180) return "Karamja (Musa Point)";
        if (x >= 2740 && x <= 2800 && y >= 3140 && y <= 3200) return "Brimhaven";
        if (x >= 2630 && x <= 2750 && y >= 9400 && y <= 9600) return "Brimhaven Dungeon";
        if (x >= 2430 && x <= 2560 && y >= 5120 && y <= 5185) return "TzHaar City";
        if (x >= 2820 && x <= 2880 && y >= 2950 && y <= 3000) return "Shilo Village";

        // Morytania
        if (x >= 3470 && x <= 3515 && y >= 3470 && y <= 3515) return "Canifis";
        if (x >= 3650 && x <= 3700 && y >= 3460 && y <= 3510) return "Port Phasmatys";
        if (x >= 3550 && x <= 3580 && y >= 3270 && y <= 3310) return "Barrows";
        if (x >= 3470 && x <= 3520 && y >= 3260 && y <= 3310) return "Mort'ton";

        // Great Kourend & Kebos
        if (x >= 1660 && x <= 1820 && y >= 3480 && y <= 3640) return "Hosidius";
        if (x >= 1560 && x <= 1600 && y >= 3470 && y <= 3510) return "Woodcutting Guild";
        if (x >= 1215 && x <= 1270 && y >= 3710 && y <= 3760) return "Farming Guild";
        if (x >= 1590 && x <= 1680 && y >= 3650 && y <= 3720) return "Kourend Castle";
        if (x >= 1460 && x <= 1590 && y >= 3530 && y <= 3650) return "Shayzien";
        if (x >= 1750 && x <= 1860 && y >= 3660 && y <= 3810) return "Port Piscarilius";
        if (x >= 1400 && x <= 1550 && y >= 3720 && y <= 3880) return "Lovakengj";
        if (x >= 1600 && x <= 1750 && y >= 3720 && y <= 3880) return "Arceuus";
        if (x >= 1600 && x <= 1730 && y >= 9980 && y <= 10110) return "Catacombs of Kourend";

        // Fremennik
        if (x >= 2620 && x <= 2700 && y >= 3630 && y <= 3700) return "Rellekka";
        if (x >= 2070 && x <= 2150 && y >= 3880 && y <= 3950) return "Lunar Isle";
        if (x >= 2500 && x <= 2600 && y >= 3830 && y <= 3900) return "Miscellania";
        if (x >= 2300 && x <= 2370 && y >= 3780 && y <= 3840) return "Neitiznot";
        if (x >= 2380 && x <= 2440 && y >= 3780 && y <= 3840) return "Jatizso";

        // Wilderness
        if (y >= 3520 && x >= 2940 && x <= 3400) {
            int wildyLevel = (y - 3520) / 8 + 1;
            if (x >= 3125 && x <= 3160 && y >= 3620 && y <= 3650) return "Ferox Enclave (Safe)";
            if (x >= 3075 && x <= 3125 && y >= 3940 && y <= 3970) return "Mage Arena (Wildy Lvl " + wildyLevel + ")";
            if (x >= 3225 && x <= 3245 && y >= 3630 && y <= 3650) return "Chaos Temple (Wildy Lvl " + wildyLevel + ")";
            if (x >= 3050 && x <= 3100 && y >= 3830 && y <= 3880) return "Lava Maze (Wildy Lvl " + wildyLevel + ")";
            if (x >= 3275 && x <= 3305 && y >= 3925 && y <= 3950) return "Rogues' Castle (Wildy Lvl " + wildyLevel + ")";
            if (x >= 3360 && x <= 3390 && y >= 3885 && y <= 3910) return "Fountain of Rune (Wildy Lvl " + wildyLevel + ")";
            return "Wilderness (Lvl " + wildyLevel + ")";
        }

        // Region ID fallbacks
        switch (regionId) {
            case 12597: return "West Varrock";
            case 12853: return "East Varrock";
            case 12598: return "Grand Exchange";
            case 12850: return "Lumbridge";
            case 11828: return "Falador";
            case 12342: return "Edgeville";
            case 12338: return "Draynor";
            case 13105: case 13106: return "Al Kharid";
            case 12341: return "Barbarian Village";
            case 12082: return "Port Sarim";
            case 11826: return "Rimmington";
            case 10806: return "Seers' Village";
            case 11062: return "Catherby";
            case 10291: case 10292: case 10547: case 10548: return "Ardougne";
            case 11571: case 11572: return "Taverley";
            case 11573: case 11829: return "Burthorpe";
            case 10288: return "Yanille";
            case 11568: case 11569: case 11824: case 11825: return "Karamja";
            case 13878: case 13877: case 14134: return "Canifis";
            case 6963: case 6964: case 7219: case 7220: return "Hosidius";
            default:
                if (regionId > 0) return "Region #" + regionId;
                return "Gielinor";
        }
    }

    private static String extractObjectName(Object client, Object obj, int id) {
        if (id <= 0) return "Object";
        String cached = OBJECT_NAME_CACHE.get(id);
        if (cached != null) return cached;

        if (obj != null) {
            try {
                Object comp = invokeMethodQuietly(obj, "getComposition");
                if (comp != null) {
                    Object imp = invokeMethodQuietly(comp, "getImpostor");
                    if (imp != null) comp = imp;
                    Object nameObj = invokeMethodQuietly(comp, "getName");
                    if (nameObj instanceof String) {
                        String name = cleanName((String) nameObj);
                        if (!name.isEmpty() && !"null".equalsIgnoreCase(name)) {
                            OBJECT_NAME_CACHE.put(id, name);
                            return name;
                        }
                    }
                }
            } catch (Throwable ignored) {}
        }

        if (client != null) {
            try {
                Method m = findMethod(client.getClass(), "getObjectComposition", int.class);
                if (m == null) m = findMethod(client.getClass(), "getObjectDefinition", int.class);
                if (m != null) {
                    Object comp = m.invoke(client, id);
                    if (comp != null) {
                        Object imp = invokeMethodQuietly(comp, "getImpostor");
                        if (imp != null) comp = imp;
                        Object nameObj = invokeMethodQuietly(comp, "getName");
                        if (nameObj instanceof String) {
                            String name = cleanName((String) nameObj);
                            if (!name.isEmpty() && !"null".equalsIgnoreCase(name)) {
                                OBJECT_NAME_CACHE.put(id, name);
                                return name;
                            }
                        }
                    }
                }
            } catch (Throwable ignored) {}
        }

        return "Object #" + id;
    }

    private static String extractNpcName(Object client, Object npc, int id) {
        if (npc != null) {
            Object nameObj = invokeMethodQuietly(npc, "getName");
            if (nameObj instanceof String) {
                String name = cleanName((String) nameObj);
                if (!name.isEmpty() && !"null".equalsIgnoreCase(name)) {
                    if (id > 0) NPC_NAME_CACHE.put(id, name);
                    return name;
                }
            }

            try {
                Object comp = invokeMethodQuietly(npc, "getComposition");
                if (comp == null) comp = invokeMethodQuietly(npc, "getTransformedComposition");
                if (comp != null) {
                    Object imp = invokeMethodQuietly(comp, "getImpostor");
                    if (imp != null) comp = imp;
                    Object cName = invokeMethodQuietly(comp, "getName");
                    if (cName instanceof String) {
                        String name = cleanName((String) cName);
                        if (!name.isEmpty() && !"null".equalsIgnoreCase(name)) {
                            if (id > 0) NPC_NAME_CACHE.put(id, name);
                            return name;
                        }
                    }
                }
            } catch (Throwable ignored) {}
        }
        if (id > 0) {
            String cached = NPC_NAME_CACHE.get(id);
            if (cached != null) return cached;

            if (client != null) {
                try {
                    Method m = findMethod(client.getClass(), "getNPCComposition", int.class);
                    if (m == null) m = findMethod(client.getClass(), "getNpcDefinition", int.class);
                    if (m != null) {
                        Object comp = m.invoke(client, id);
                        if (comp != null) {
                            Object imp = invokeMethodQuietly(comp, "getImpostor");
                            if (imp != null) comp = imp;
                            Object nameObj = invokeMethodQuietly(comp, "getName");
                            if (nameObj instanceof String) {
                                String name = cleanName((String) nameObj);
                                if (!name.isEmpty() && !"null".equalsIgnoreCase(name)) {
                                    NPC_NAME_CACHE.put(id, name);
                                    return name;
                                }
                            }
                        }
                    }
                } catch (Throwable ignored) {}
            }
        }
        return "NPC #" + (id > 0 ? id : 0);
    }

    private static String cleanName(String name) {
        if (name == null) return "";
        return name.replaceAll("<[^>]*>", "").replace('\u00A0', ' ').trim();
    }

    // -------------------------------------------------------------
    // Reflection Cache & Helpers
    // -------------------------------------------------------------
    private static Method findMethod(Class<?> clazz, String name, Class<?>... paramTypes) {
        if (clazz == null || name == null) return null;
        String cacheKey = clazz.getName() + '#' + name + (paramTypes != null && paramTypes.length > 0 ? '#' + paramTypes[0].getName() : "");
        Method cached = METHOD_CACHE.get(cacheKey);
        if (cached != null) return cached == NULL_METHOD_MARKER ? null : cached;

        Method found = null;
        try {
            found = (paramTypes == null || paramTypes.length == 0) ? clazz.getMethod(name) : clazz.getMethod(name, paramTypes);
        } catch (Throwable ignored) {
            for (Method m : clazz.getMethods()) {
                if (m.getName().equalsIgnoreCase(name)) {
                    if (paramTypes == null || paramTypes.length == 0 || m.getParameterCount() == paramTypes.length) {
                        found = m;
                        break;
                    }
                }
            }
        }

        if (found != null) {
            try { found.setAccessible(true); } catch (Throwable ignored) {}
        }
        METHOD_CACHE.put(cacheKey, found != null ? found : NULL_METHOD_MARKER);
        return found;
    }

    private static Object invokeMethodQuietly(Object target, String methodName, Object... args) {
        if (target == null || methodName == null) return null;
        try {
            Class<?>[] paramTypes = null;
            if (args != null && args.length > 0) {
                paramTypes = new Class<?>[args.length];
                for (int i = 0; i < args.length; i++) {
                    paramTypes[i] = args[i] != null ? args[i].getClass() : Object.class;
                    if (paramTypes[i] == Integer.class) paramTypes[i] = int.class;
                    else if (paramTypes[i] == Long.class) paramTypes[i] = long.class;
                    else if (paramTypes[i] == Boolean.class) paramTypes[i] = boolean.class;
                }
            }
            Method m = findMethod(target.getClass(), methodName, paramTypes);
            if (m != null) {
                return (args == null || args.length == 0) ? m.invoke(target) : m.invoke(target, args);
            }
        } catch (Throwable ignored) {}
        return null;
    }
}
