package com.osrsmr.agent;

import java.lang.instrument.Instrumentation;
import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.lang.reflect.Modifier;
import java.net.Socket;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;
import java.util.Collection;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

public class BytecodeAgent {
    private static final String VERSION = "1.3.3";
    private static final int PORT = 43594;
    private static volatile Thread heartbeatThread = null;
    private static final String JVM_PID = getPidInternal();
    private static final ConcurrentHashMap<Integer, String> ITEM_NAME_CACHE = new ConcurrentHashMap<>();

    private static String getPidInternal() {
        try {
            String jvmName = java.lang.management.ManagementFactory.getRuntimeMXBean().getName();
            int idx = jvmName.indexOf('@');
            if (idx > 0) return jvmName.substring(0, idx);
            return jvmName;
        } catch (Throwable t) {
            return "Unknown";
        }
    }
    
    // RuneLite API / Instance Cache
    private static Object runeLiteClient = null;
    
    // Obfuscated Field Discovery State
    private static String foundClientClass = null;
    private static ClassLoader clientClassLoader = null;
    
    private static Field gameStateField = null;
    private static int gameStateMultiplier = 1;
    
    private static Field localPlayerField = null;
    private static Field skillsField = null;
    private static Field npcListField = null;
    private static Field currentTabField = null;
    private static int currentTabMultiplier = 1;
    private static Field equipmentField = null;
    private static Field inventoryIdsField = null;
    private static Field inventoryQuantitiesField = null;
    
    private static Field worldField = null;
    private static int worldMultiplier = 1;
    private static Field destinationXField = null;
    private static Field destinationYField = null;
    private static Field playerXField = null;
    private static Field playerYField = null;
    private static Field sceneField = null;
    private static Field objectsField = null;
    private static Field healthField = null;
    private static Field animationField = null;
    private static Field orientationField = null;
    
    private static final int[] MULTIPLIERS = {
        1, 149592726, -220679128, 1140066225, -1375364801, 
        1726053805, 506259277, -1565406731, 1511233857, -1204273031,
        -615234123, 165384213, 912384121, -192837465, 582934811,
        762384913, -102938475, 110293847, -203948571, 992837465,
        1618033989, -182736451, 837465193, -938271645, 1238917231,
        -1928371923, 1928374651, 1639840131, -847362911, 192847163,
        -128374619, 102938471, -58392019, 1782394711, -192847193,
        10293847, -29384719, 84729103, -19283746
    };
    
    private static final String[] SKILL_NAMES = {
        "Attack", "Defence", "Strength", "Hitpoints", "Ranged", "Prayer", "Magic", "Cooking",
        "Woodcutting", "Fletching", "Fishing", "Firemaking", "Crafting", "Smithing", "Mining",
        "Herblore", "Agility", "Thieving", "Slayer", "Farming", "Runecraft", "Hunter", "Construction", "Sailing",
        "Skill24", "Skill25", "Skill26", "Skill27", "Skill28", "Skill29", "Skill30", "Skill31", "Skill32", "Skill33", "Skill34", "Skill35"
    };

    public static void premain(String agentArgs, Instrumentation inst) {
        initialize(inst);
    }

    public static void agentmain(String agentArgs, Instrumentation inst) {
        initialize(inst);
    }

    private static synchronized void initialize(Instrumentation inst) {
        if (heartbeatThread != null && heartbeatThread.isAlive()) {
            System.out.println("[osrsmr] Agent already active and running (PID " + JVM_PID + ")");
            return;
        }

        heartbeatThread = new Thread(() -> {
            try {
                // Wait briefly for client initialization
                Thread.sleep(1000);
                System.out.println("[osrsmr] Starting Discovery Agent v" + VERSION + " (PID " + JVM_PID + ")...");

                Socket socket = null;
                OutputStream out = null;

                while (true) {
                    try {
                        // 1. Scan loaded classes for RuneLite client or obfuscated fields
                        try {
                            scanAndDiscover(inst);
                        } catch (Throwable ignored) {}

                        boolean hasGameClient = (runeLiteClient != null || foundClientClass != null || gameStateField != null || localPlayerField != null);

                        // If this JVM instance does not contain the game client yet,
                        // keep scanning without crashing
                        if (!hasGameClient && inst != null) {
                            hasGameClient = true;
                        }

                        if (!hasGameClient) {
                            if (socket != null) {
                                try { socket.close(); } catch (Exception ignored) {}
                                socket = null;
                            }
                            Thread.sleep(2000);
                            continue;
                        }

                        if (socket == null || socket.isClosed() || !socket.isConnected()) {
                            try {
                                socket = new Socket("127.0.0.1", PORT);
                                out = socket.getOutputStream();
                                System.out.println("[osrsmr] Connected to Bridge on port " + PORT + " (PID " + JVM_PID + ")");
                            } catch (Exception e) {
                                // Bridge not ready yet
                                Thread.sleep(1500);
                                continue;
                            }
                        }

                        StringBuilder data = new StringBuilder();
                        data.append("PID: ").append(JVM_PID).append("\n");
                        data.append("Status: Hook Active (v").append(VERSION).append(")\n");

                        // 2. Try RuneLite API Extraction first if available
                        boolean runeLiteSuccess = false;
                        if (runeLiteClient != null) {
                            try {
                                runeLiteSuccess = processRuneLiteClient(runeLiteClient, data);
                            } catch (Throwable t) {
                                runeLiteSuccess = false;
                            }
                        }

                        // 3. Fallback to Obfuscated / Heuristic Extraction
                        if (!runeLiteSuccess) {
                            try {
                                processObfuscatedClient(inst, data);
                            } catch (Throwable ignored) {}
                        }

                        // Send data over socket
                        try {
                            byte[] bytes = data.toString().getBytes(StandardCharsets.UTF_8);
                            out.write(bytes);
                            out.flush();
                        } catch (Exception ioEx) {
                            // Network I/O failure (socket closed or broken)
                            if (socket != null) {
                                try { socket.close(); } catch (Exception ignored) {}
                                socket = null;
                            }
                        }
                    } catch (Throwable t) {
                        // Keep agent loop alive without dropping healthy connections
                    }
                    Thread.sleep(1000);
                }
            } catch (Exception e) {
                e.printStackTrace();
            }
        }, "osrsmr-heartbeat");
        heartbeatThread.setDaemon(true);
        heartbeatThread.start();
    }

    private static void scanAndDiscover(Instrumentation inst) {
        if (runeLiteClient != null || inst == null) {
            return;
        }
        try {
            Class<?>[] allLoaded = inst.getAllLoadedClasses();

            // Try to find RuneLite Client instance
            if (runeLiteClient == null) {
                // Method 1: Check RuneLite injector & static fields
                for (Class<?> clazz : allLoaded) {
                    String cName = clazz.getName();
                    if (cName.equals("net.runelite.client.RuneLite")) {
                        try {
                            Object injector = null;
                            // Try getInjector()
                            try {
                                Method getInjector = clazz.getDeclaredMethod("getInjector");
                                getInjector.setAccessible(true);
                                injector = getInjector.invoke(null);
                            } catch (Throwable ignored) {}

                            // Try injector field
                            if (injector == null) {
                                for (Field f : clazz.getDeclaredFields()) {
                                    if (f.getName().equals("injector") || f.getType().getName().contains("Injector")) {
                                        f.setAccessible(true);
                                        injector = f.get(null);
                                        if (injector != null) break;
                                    }
                                }
                            }

                            if (injector != null) {
                                Class<?> clientClass = null;
                                try {
                                    clientClass = Class.forName("net.runelite.api.Client", false, clazz.getClassLoader());
                                } catch (Throwable ignored) {}

                                if (clientClass == null) {
                                    for (Class<?> c : allLoaded) {
                                        if (c.getName().equals("net.runelite.api.Client")) {
                                            clientClass = c;
                                            break;
                                        }
                                    }
                                }

                                if (clientClass != null) {
                                    for (Method m : injector.getClass().getMethods()) {
                                        if (m.getName().equals("getInstance") && m.getParameterCount() == 1 && m.getParameterTypes()[0] == Class.class) {
                                            try {
                                                m.setAccessible(true);
                                                Object instance = m.invoke(injector, clientClass);
                                                if (instance != null) {
                                                    runeLiteClient = instance;
                                                    System.out.println("[osrsmr] RuneLite Client acquired via Injector.getInstance(Client.class)");
                                                    break;
                                                }
                                            } catch (Throwable ignored) {}
                                        }
                                    }
                                }

                                if (runeLiteClient == null) {
                                    try {
                                        Method getAllBindings = injector.getClass().getMethod("getAllBindings");
                                        getAllBindings.setAccessible(true);
                                        Map<?, ?> map = (Map<?, ?>) getAllBindings.invoke(injector);
                                        if (map != null) {
                                            for (Object binding : map.values()) {
                                                try {
                                                    Method getProvider = binding.getClass().getMethod("getProvider");
                                                    getProvider.setAccessible(true);
                                                    Object provider = getProvider.invoke(binding);
                                                    if (provider != null) {
                                                        Method get = provider.getClass().getMethod("get");
                                                        get.setAccessible(true);
                                                        Object val = get.invoke(provider);
                                                        if (val != null && isRuneLiteClientObject(val)) {
                                                            runeLiteClient = val;
                                                            System.out.println("[osrsmr] RuneLite Client acquired via Injector bindings!");
                                                            break;
                                                        }
                                                    }
                                                } catch (Throwable ignored) {}
                                            }
                                        }
                                    } catch (Throwable ignored) {}
                                }
                            }
                        } catch (Throwable t) {
                            t.printStackTrace();
                        }
                    }
                    if (runeLiteClient != null) break;
                }

                // Method 2: Check static fields & singleton objects in all loaded classes
                if (runeLiteClient == null) {
                    for (Class<?> clazz : allLoaded) {
                        Field[] fields;
                        try {
                            fields = clazz.getDeclaredFields();
                        } catch (Throwable t) { continue; }

                        for (Field f : fields) {
                            if (Modifier.isStatic(f.getModifiers()) && !f.getType().isPrimitive()) {
                                try {
                                    f.setAccessible(true);
                                    Object val = f.get(null);
                                    if (val == null) continue;

                                    if (isRuneLiteClientObject(val)) {
                                        runeLiteClient = val;
                                        System.out.println("[osrsmr] RuneLite Client instance discovered in " + clazz.getName() + "." + f.getName());
                                        break;
                                    }

                                    // Check fields of singleton / manager objects
                                    String typeName = val.getClass().getName();
                                    if (typeName.startsWith("net.runelite.") || typeName.equals("client") || typeName.contains("ClientLoader")) {
                                        for (Field innerF : val.getClass().getDeclaredFields()) {
                                            if (!innerF.getType().isPrimitive()) {
                                                innerF.setAccessible(true);
                                                Object innerVal = innerF.get(val);
                                                if (innerVal != null && isRuneLiteClientObject(innerVal)) {
                                                    runeLiteClient = innerVal;
                                                    System.out.println("[osrsmr] RuneLite Client discovered in " + typeName + "." + innerF.getName());
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                } catch (Throwable ignored) {}
                            }
                            if (runeLiteClient != null) break;
                        }
                        if (runeLiteClient != null) break;
                    }
                }
            }

            // Global scan for obfuscated static fields across ALL loaded classes
            for (Class<?> clazz : allLoaded) {
                String name = clazz.getName();
                
                // Track main client class
                if (foundClientClass == null) {
                    if (name.equals("client") || name.equals("Client") || name.endsWith(".client")) {
                        foundClientClass = name;
                        clientClassLoader = clazz.getClassLoader();
                    }
                }

                Field[] fields;
                try {
                    fields = clazz.getDeclaredFields();
                } catch (Throwable t) { continue; }

                for (Field f : fields) {
                    if (!Modifier.isStatic(f.getModifiers())) continue;

                    try {
                        f.setAccessible(true);

                        // 1. GameState field
                        if (gameStateField == null && f.getType() == int.class) {
                            int val = f.getInt(null);
                            for (int m : MULTIPLIERS) {
                                int decoded = val * m;
                                if (decoded == 10 || decoded == 11 || decoded == 20 || decoded == 25 || decoded == 30 || decoded == 40 || decoded == 45) {
                                    gameStateField = f;
                                    gameStateMultiplier = m;
                                    System.out.println("[osrsmr] Discovered GameState field in " + name + "." + f.getName() + " (Decoded: " + decoded + ", mult: " + m + ")");
                                    break;
                                }
                            }
                        }

                        // 2. Skills Array field (int[] of length 24..50 with valid levels)
                        if (skillsField == null && f.getType() == int[].class) {
                            int[] arr = (int[]) f.get(null);
                            if (arr != null && arr.length >= 24 && arr.length <= 50) {
                                int logicalLevels = 0;
                                for (int v : arr) {
                                    if (v >= 1 && v <= 125) logicalLevels++;
                                    else {
                                        for (int m : MULTIPLIERS) {
                                            int d = v * m;
                                            if (d >= 1 && d <= 125) { logicalLevels++; break; }
                                        }
                                    }
                                }
                                if (logicalLevels >= 12) {
                                    skillsField = f;
                                    System.out.println("[osrsmr] Discovered Skills array in " + name + "." + f.getName() + " (len: " + arr.length + ")");
                                }
                            }
                        }

                        // 3. World field (int decoded 301..599 or 1..299)
                        if (worldField == null && f.getType() == int.class) {
                            int val = f.getInt(null);
                            if (val != 0) {
                                for (int m : MULTIPLIERS) {
                                    int decoded = val * m;
                                    if (decoded >= 301 && decoded <= 599) {
                                        worldField = f;
                                        worldMultiplier = m;
                                        System.out.println("[osrsmr] Discovered World field in " + name + "." + f.getName() + " (World: " + decoded + ")");
                                        break;
                                    } else if (decoded >= 1 && decoded <= 299) {
                                        worldField = f;
                                        worldMultiplier = m;
                                        break;
                                    }
                                }
                            }
                        }

                        // 4. LocalPlayer field (Actor object with many fields and a name string)
                        if (localPlayerField == null && !f.getType().isPrimitive() && !f.getType().isArray()) {
                            Object playerObj = f.get(null);
                            if (playerObj != null) {
                                Field[] pFields = playerObj.getClass().getDeclaredFields();
                                if (pFields.length >= 20) {
                                    for (Field pf : pFields) {
                                        if (pf.getType() == String.class) {
                                            pf.setAccessible(true);
                                            String str = (String) pf.get(playerObj);
                                            if (str != null && !str.isEmpty() && !str.contains("<") && str.length() < 20 && !str.startsWith("java.")) {
                                                localPlayerField = f;
                                                System.out.println("[osrsmr] Discovered LocalPlayer in " + name + "." + f.getName() + " (Player Name: " + str + ")");
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // 5. Inventory & Equipment arrays
                        if (f.getType() == int[].class) {
                            int[] arr = (int[]) f.get(null);
                            if (arr != null) {
                                if (arr.length >= 28 && arr.length <= 40 && !f.equals(skillsField)) {
                                    int nonZeroCount = 0;
                                    for (int v : arr) if (v != 0) nonZeroCount++;
                                    if (inventoryIdsField == null && nonZeroCount > 0) {
                                        inventoryIdsField = f;
                                    } else if (inventoryQuantitiesField == null) {
                                        inventoryQuantitiesField = f;
                                    }
                                } else if ((arr.length == 11 || arr.length == 14) && equipmentField == null) {
                                    equipmentField = f;
                                }
                            }
                        }

                        // 6. NPC List array (Object[] length > 100)
                        if (npcListField == null && f.getType().isArray() && !f.getType().getComponentType().isPrimitive()) {
                            Object[] arr = (Object[]) f.get(null);
                            if (arr != null && arr.length >= 100 && arr.length <= 65536) {
                                npcListField = f;
                            }
                        }

                        // 7. Current Tab (int decoded 0..14)
                        if (currentTabField == null && f.getType() == int.class) {
                            int val = f.getInt(null);
                            if (val != 0) {
                                for (int m : MULTIPLIERS) {
                                    int decoded = val * m;
                                    if (decoded >= 0 && decoded <= 14) {
                                        currentTabField = f;
                                        currentTabMultiplier = m;
                                        break;
                                    }
                                }
                            }
                        }

                    } catch (Throwable ignored) {}
                }
            }
        } catch (Throwable t) {
            t.printStackTrace();
        }
    }

    private static boolean isRuneLiteClientObject(Object obj) {
        if (obj == null) return false;
        Class<?> cls = obj.getClass();
        String cName = cls.getName();
        if (cName.equals("client") || cName.endsWith(".Client") || cName.contains("RSClient")) {
            return true;
        }
        
        Class<?> curr = cls;
        while (curr != null && curr != Object.class) {
            if (curr.getName().contains("Client") || curr.getName().contains("RSClient")) {
                return true;
            }
            for (Class<?> iface : curr.getInterfaces()) {
                if (iface.getName().contains("Client") || iface.getName().contains("RSClient")) {
                    return true;
                }
            }
            curr = curr.getSuperclass();
        }
        
        try {
            Method m = cls.getMethod("getGameState");
            if (m != null) return true;
        } catch (Throwable ignored) {}
        try {
            Method m = cls.getMethod("getLocalPlayer");
            if (m != null) return true;
        } catch (Throwable ignored) {}
        return false;
    }

    private static boolean processRuneLiteClient(Object client, StringBuilder data) {
        try {
            data.append("Client Class: RuneLite-Injected\n");

            // GameState
            int gs = 0;
            String stateStr = "Unknown";
            try {
                Method getGameStateMethod = client.getClass().getMethod("getGameState");
                getGameStateMethod.setAccessible(true);
                Object gsObj = getGameStateMethod.invoke(client);
                if (gsObj != null) {
                    if (gsObj instanceof Number) {
                        gs = ((Number) gsObj).intValue();
                    } else if (gsObj instanceof Enum) {
                        Enum<?> gsEnum = (Enum<?>) gsObj;
                        String name = gsEnum.name();
                        try {
                            Method getStateMethod = gsObj.getClass().getMethod("getState");
                            getStateMethod.setAccessible(true);
                            gs = (Integer) getStateMethod.invoke(gsObj);
                        } catch (Throwable t) {
                            if ("LOGGED_IN".equalsIgnoreCase(name)) gs = 30;
                            else if ("LOGIN_SCREEN".equalsIgnoreCase(name)) gs = 10;
                            else if ("LOGIN_SCREEN_AUTHENTICATOR".equalsIgnoreCase(name)) gs = 11;
                            else if ("LOGGING_IN".equalsIgnoreCase(name)) gs = 20;
                            else if ("LOADING".equalsIgnoreCase(name)) gs = 25;
                            else if ("HOPPING".equalsIgnoreCase(name)) gs = 45;
                            else if ("CONNECTION_LOST".equalsIgnoreCase(name)) gs = 40;
                            else if ("STARTING".equalsIgnoreCase(name)) gs = 1;
                        }
                    }
                }
            } catch (Throwable ignored) {}

            // LocalPlayer
            Object player = null;
            try {
                Method getLocalPlayerMethod = client.getClass().getMethod("getLocalPlayer");
                getLocalPlayerMethod.setAccessible(true);
                player = getLocalPlayerMethod.invoke(client);
            } catch (Throwable ignored) {}
            if (player == null) {
                try {
                    Method getTopView = client.getClass().getMethod("getTopLevelWorldView");
                    getTopView.setAccessible(true);
                    Object topView = getTopView.invoke(client);
                    if (topView != null) {
                        Method getLocalPlayer = topView.getClass().getMethod("getLocalPlayer");
                        getLocalPlayer.setAccessible(true);
                        player = getLocalPlayer.invoke(topView);
                    }
                } catch (Throwable ignored) {}
            }

            // Infer Logged In if player exists
            if (player != null) {
                if (gs == 0 || gs == 20 || gs == 25) {
                    gs = 30;
                }
            }

            if (gs == 30) stateStr = "Logged In";
            else if (gs == 10 || gs == 11) stateStr = "Login Screen";
            else if (gs == 20) stateStr = "Logging In";
            else if (gs == 25) stateStr = "Loading";
            else if (gs == 45) stateStr = "Hopping";
            else if (gs == 40) stateStr = "Connection Lost";
            else if (gs == 1) stateStr = "Starting";
            else stateStr = "Detecting...";

            data.append("GameState: ").append(gs).append("\n");
            data.append("ENGINE_STATE: ").append(stateStr).append("\n");

            // Player Data
            int playerX = 0, playerY = 0, plane = 0;
            if (player != null) {
                try {
                    Method getName = player.getClass().getMethod("getName");
                    getName.setAccessible(true);
                    String pName = (String) getName.invoke(player);
                    if (pName != null && !pName.isEmpty()) {
                        data.append("PLAYER_NAME: ").append(pName).append("\n");
                    }
                } catch (Throwable ignored) {}

                try {
                    Method getWorldLocation = player.getClass().getMethod("getWorldLocation");
                    getWorldLocation.setAccessible(true);
                    Object wp = getWorldLocation.invoke(player);
                    if (wp != null) {
                        Method getX = wp.getClass().getMethod("getX");
                        Method getY = wp.getClass().getMethod("getY");
                        Method getPlane = wp.getClass().getMethod("getPlane");
                        getX.setAccessible(true);
                        getY.setAccessible(true);
                        getPlane.setAccessible(true);
                        playerX = (Integer) getX.invoke(wp);
                        playerY = (Integer) getY.invoke(wp);
                        plane = (Integer) getPlane.invoke(wp);
                    }
                } catch (Throwable ignored) {}

                if (playerX == 0 || playerY == 0) {
                    try {
                        Method getLocalLocation = player.getClass().getMethod("getLocalLocation");
                        getLocalLocation.setAccessible(true);
                        Object lp = getLocalLocation.invoke(player);
                        if (lp != null) {
                            Method getX = lp.getClass().getMethod("getX");
                            Method getY = lp.getClass().getMethod("getY");
                            getX.setAccessible(true);
                            getY.setAccessible(true);
                            int lpX = (Integer) getX.invoke(lp);
                            int lpY = (Integer) getY.invoke(lp);

                            int baseX = 0, baseY = 0;
                            try {
                                Method getBaseX = client.getClass().getMethod("getBaseX");
                                Method getBaseY = client.getClass().getMethod("getBaseY");
                                getBaseX.setAccessible(true);
                                getBaseY.setAccessible(true);
                                baseX = (Integer) getBaseX.invoke(client);
                                baseY = (Integer) getBaseY.invoke(client);
                            } catch (Throwable ignored) {}

                            if (baseX == 0 || baseY == 0) {
                                try {
                                    Method getTopView = client.getClass().getMethod("getTopLevelWorldView");
                                    getTopView.setAccessible(true);
                                    Object topView = getTopView.invoke(client);
                                    if (topView != null) {
                                        Method getBaseX = topView.getClass().getMethod("getBaseX");
                                        Method getBaseY = topView.getClass().getMethod("getBaseY");
                                        getBaseX.setAccessible(true);
                                        getBaseY.setAccessible(true);
                                        baseX = (Integer) getBaseX.invoke(topView);
                                        baseY = (Integer) getBaseY.invoke(topView);
                                    }
                                } catch (Throwable ignored) {}
                            }

                            if (baseX > 0 && baseY > 0) {
                                playerX = baseX + (lpX >> 7);
                                playerY = baseY + (lpY >> 7);
                            }
                        }
                    } catch (Throwable ignored) {}
                }

                if (playerX > 0 && playerY > 0) {
                    data.append("PLAYER_X: ").append(playerX).append("\n");
                    data.append("PLAYER_Y: ").append(playerY).append("\n");
                    data.append("LOCATION: (").append(playerX).append(", ").append(playerY).append(", ").append(plane).append(")\n");
                    data.append("LOCATION_STATUS: Connected\n");
                }

                try {
                    Method getAnimation = player.getClass().getMethod("getAnimation");
                    getAnimation.setAccessible(true);
                    int anim = (Integer) getAnimation.invoke(player);
                    data.append("ANIMATION: ").append(anim).append("\n");
                } catch (Throwable ignored) {}

                try {
                    Method getOrientation = player.getClass().getMethod("getOrientation");
                    getOrientation.setAccessible(true);
                    int orient = (Integer) getOrientation.invoke(player);
                    data.append("ORIENTATION: ").append(orient).append("\n");
                } catch (Throwable ignored) {}

                try {
                    Method getCombatLevel = player.getClass().getMethod("getCombatLevel");
                    getCombatLevel.setAccessible(true);
                    int combat = (Integer) getCombatLevel.invoke(player);
                    if (combat > 0) {
                        data.append("COMBAT_LEVEL: ").append(combat).append("\n");
                    }
                } catch (Throwable ignored) {}
            }

            // Skills Data
            int[] realLevels = null;
            int[] boostedLevels = null;
            try {
                Method getRealSkills = client.getClass().getMethod("getRealSkillLevels");
                getRealSkills.setAccessible(true);
                realLevels = (int[]) getRealSkills.invoke(client);
            } catch (Throwable ignored) {}

            try {
                Method getBoosted = client.getClass().getMethod("getBoostedSkillLevels");
                getBoosted.setAccessible(true);
                boostedLevels = (int[]) getBoosted.invoke(client);
            } catch (Throwable ignored) {}

            if (realLevels != null) {
                for (int i = 0; i < Math.min(realLevels.length, SKILL_NAMES.length); i++) {
                    int real = realLevels[i];
                    if (boostedLevels != null && i < boostedLevels.length) {
                        int boosted = boostedLevels[i];
                        data.append("SKILL[").append(SKILL_NAMES[i]).append("]: ").append(boosted).append("/").append(real).append("\n");
                    } else {
                        data.append("SKILL[").append(SKILL_NAMES[i]).append("]: ").append(real).append("\n");
                    }
                }
            }

            // World
            try {
                Method getWorld = client.getClass().getMethod("getWorld");
                getWorld.setAccessible(true);
                int world = (Integer) getWorld.invoke(client);
                if (world > 0) {
                    if (world < 300) world += 300;
                    data.append("WORLD: ").append(world).append("\n");
                }
            } catch (Throwable ignored) {}

            // Current Tab
            try {
                int tab = -1;
                try {
                    Method getVarc = client.getClass().getMethod("getVarcIntValue", int.class);
                    getVarc.setAccessible(true);
                    tab = (Integer) getVarc.invoke(client, 171);
                } catch (Throwable t1) {
                    try {
                        Method getVarc = client.getClass().getMethod("getVarcInt", int.class);
                        getVarc.setAccessible(true);
                        tab = (Integer) getVarc.invoke(client, 171);
                    } catch (Throwable t2) {
                        try {
                            Method getVar = client.getClass().getMethod("getVar", int.class);
                            getVar.setAccessible(true);
                            tab = (Integer) getVar.invoke(client, 171);
                        } catch (Throwable t3) {
                            for (Method m : client.getClass().getMethods()) {
                                if ((m.getName().equals("getVar") || m.getName().equals("getVarcIntValue")) && m.getParameterCount() == 1) {
                                    Class<?> pType = m.getParameterTypes()[0];
                                    if (pType.isEnum()) {
                                        for (Object ec : pType.getEnumConstants()) {
                                            if (ec.toString().contains("INVENTORY_TAB") || ec.toString().contains("TAB")) {
                                                try {
                                                    m.setAccessible(true);
                                                    tab = (Integer) m.invoke(client, ec);
                                                    if (tab >= 0 && tab <= 14) break;
                                                } catch (Throwable ignored) {}
                                            }
                                        }
                                    }
                                }
                                if (tab >= 0 && tab <= 14) break;
                            }
                        }
                    }
                }
                if (tab >= 0 && tab <= 14) {
                    data.append("CURRENT_TAB: ").append(tab).append("\n");
                }
            } catch (Throwable ignored) {}

            // Energy & Weight
            try {
                Method getEnergy = client.getClass().getMethod("getEnergy");
                getEnergy.setAccessible(true);
                int energy = (Integer) getEnergy.invoke(client);
                data.append("RUN_ENERGY: ").append(energy / 100).append("%\n");
            } catch (Throwable ignored) {}

            try {
                Method getWeight = client.getClass().getMethod("getWeight");
                getWeight.setAccessible(true);
                int weight = (Integer) getWeight.invoke(client);
                data.append("WEIGHT: ").append(weight).append(" kg\n");
            } catch (Throwable ignored) {}

            // Inventory & Equipment via ItemContainer
            readRuneLiteItemContainer(client, 93, "INV", 28, data);
            readRuneLiteItemContainer(client, 94, "EQUIP", 14, data);

            // NPCs
            try {
                Object npcsObj = null;
                // Check modern RuneLite WorldView first
                try {
                    Method getTopView = client.getClass().getMethod("getTopLevelWorldView");
                    getTopView.setAccessible(true);
                    Object topView = getTopView.invoke(client);
                    if (topView != null) {
                        for (String mName : new String[]{"npcs", "getNpcs", "getNPCs"}) {
                            try {
                                Method m = topView.getClass().getMethod(mName);
                                m.setAccessible(true);
                                npcsObj = m.invoke(topView);
                                if (npcsObj != null) break;
                            } catch (Throwable ignored) {}
                        }
                    }
                } catch (Throwable ignored) {}

                // Fallback to direct client call
                if (npcsObj == null) {
                    for (String mName : new String[]{"getNpcs", "npcs", "getNPCs"}) {
                        try {
                            Method m = client.getClass().getMethod(mName);
                            m.setAccessible(true);
                            npcsObj = m.invoke(client);
                            if (npcsObj != null) break;
                        } catch (Throwable ignored) {}
                    }
                }

                if (npcsObj != null) {
                    int count = 0;
                    try {
                        if (npcsObj instanceof Iterable) {
                            for (Object npc : (Iterable<?>) npcsObj) {
                                if (npc != null && count < 25) {
                                    appendRuneLiteNpc(client, npc, count, playerX, playerY, data);
                                    count++;
                                }
                            }
                        } else if (npcsObj instanceof Object[]) {
                            Object[] npcArr = (Object[]) npcsObj;
                            for (Object npc : npcArr) {
                                if (npc != null && count < 25) {
                                    appendRuneLiteNpc(client, npc, count, playerX, playerY, data);
                                    count++;
                                }
                            }
                        }
                    } catch (Throwable ignored) {}
                    data.append("TOTAL_NPCS: ").append(count).append("\n");
                }
            } catch (Throwable ignored) {}

            // Plane & Camera & FPS
            try {
                Method getPlane = client.getClass().getMethod("getPlane");
                getPlane.setAccessible(true);
                data.append("PLANE: ").append(getPlane.invoke(client)).append("\n");
            } catch (Throwable ignored) {}

            try {
                Method getFPS = client.getClass().getMethod("getFPS");
                getFPS.setAccessible(true);
                data.append("FPS: ").append(getFPS.invoke(client)).append("\n");
            } catch (Throwable ignored) {}

            return true;
        } catch (Throwable t) {
            return false;
        }
    }

    private static void readRuneLiteItemContainer(Object client, int containerId, String prefix, int maxItems, StringBuilder data) {
        try {
            Object container = null;

            // Attempt 1: getItemContainer(int) or getItemContainer(InventoryID)
            for (Method m : client.getClass().getMethods()) {
                if (m.getName().equals("getItemContainer") && m.getParameterCount() == 1) {
                    Class<?> pType = m.getParameterTypes()[0];
                    if (pType == int.class || pType == Integer.class) {
                        try {
                            m.setAccessible(true);
                            container = m.invoke(client, containerId);
                            if (container != null) break;
                        } catch (Throwable ignored) {}
                    } else if (pType.isEnum()) {
                        for (Object enumConst : pType.getEnumConstants()) {
                            try {
                                Method getId = enumConst.getClass().getMethod("getId");
                                getId.setAccessible(true);
                                int id = (Integer) getId.invoke(enumConst);
                                if (id == containerId) {
                                    m.setAccessible(true);
                                    container = m.invoke(client, enumConst);
                                    break;
                                }
                            } catch (Throwable ignored) {}
                        }
                        if (container != null) break;
                    }
                }
            }

            // Attempt 2: getItemContainers() or table scan
            if (container == null) {
                try {
                    for (Method m : client.getClass().getMethods()) {
                        if (m.getName().startsWith("getItemContainer") && m.getParameterCount() == 0) {
                            m.setAccessible(true);
                            Object res = m.invoke(client);
                            if (res instanceof Map) {
                                container = ((Map<?, ?>) res).get(containerId);
                                if (container != null) break;
                            }
                        }
                    }
                } catch (Throwable ignored) {}
            }

            if (container != null) {
                Method getItems = container.getClass().getMethod("getItems");
                getItems.setAccessible(true);
                Object itemsObj = getItems.invoke(container);
                Object[] items = null;
                if (itemsObj instanceof Object[]) {
                    items = (Object[]) itemsObj;
                } else if (itemsObj instanceof Collection) {
                    items = ((Collection<?>) itemsObj).toArray();
                }

                if (items != null) {
                    for (int i = 0; i < maxItems; i++) {
                        if (i < items.length && items[i] != null) {
                            Object item = items[i];
                            int id = -1;
                            int qty = 0;
                            try {
                                Method getId = item.getClass().getMethod("getId");
                                getId.setAccessible(true);
                                id = (Integer) getId.invoke(item);
                            } catch (Throwable ignored) {}
                            try {
                                Method getQty = item.getClass().getMethod("getQuantity");
                                getQty.setAccessible(true);
                                qty = (Integer) getQty.invoke(item);
                            } catch (Throwable ignored) {}

                            if (id > 0 && id != 65535) {
                                String name = resolveItemName(client, id);
                                if (name != null && !name.isEmpty() && !name.equalsIgnoreCase("null")) {
                                    data.append(prefix).append("[").append(i).append("]: ").append(name).append(",").append(qty).append("\n");
                                } else {
                                    data.append(prefix).append("[").append(i).append("]: ").append(id).append(",").append(qty).append("\n");
                                }
                            } else {
                                data.append(prefix).append("[").append(i).append("]: 0,0\n");
                            }
                        } else {
                            data.append(prefix).append("[").append(i).append("]: 0,0\n");
                        }
                    }
                    return;
                }
            }
        } catch (Throwable ignored) {}

        // Fallback: output empty slots
        for (int i = 0; i < maxItems; i++) {
            data.append(prefix).append("[").append(i).append("]: 0,0\n");
        }
    }

    private static String resolveItemName(Object client, int id) {
        if (id <= 0 || client == null) return null;
        String cached = ITEM_NAME_CACHE.get(id);
        if (cached != null) return cached;

        try {
            for (String mName : new String[]{"getItemDefinition", "getItemComposition"}) {
                try {
                    Method m = client.getClass().getMethod(mName, int.class);
                    m.setAccessible(true);
                    Object def = m.invoke(client, id);
                    if (def != null) {
                        Method getName = def.getClass().getMethod("getName");
                        getName.setAccessible(true);
                        Object n = getName.invoke(def);
                        if (n instanceof String) {
                            String s = ((String) n).replaceAll("<[^>]*>", "").trim();
                            if (!s.isEmpty() && !s.equalsIgnoreCase("null")) {
                                ITEM_NAME_CACHE.put(id, s);
                                return s;
                            }
                        }
                    }
                } catch (Throwable ignored) {}
            }
        } catch (Throwable ignored) {}
        return null;
    }

    private static void appendRuneLiteNpc(Object client, Object npc, int index, int playerX, int playerY, StringBuilder data) {
        try {
            int id = -1;
            int dist = 0;
            String health = "100%";

            try {
                Method getId = npc.getClass().getMethod("getId");
                getId.setAccessible(true);
                id = (Integer) getId.invoke(npc);
            } catch (Throwable ignored) {}

            String name = extractNpcName(client, npc, id);

            try {
                Method getWorldLocation = npc.getClass().getMethod("getWorldLocation");
                getWorldLocation.setAccessible(true);
                Object wp = getWorldLocation.invoke(npc);
                if (wp != null && playerX > 0 && playerY > 0) {
                    Method getX = wp.getClass().getMethod("getX");
                    Method getY = wp.getClass().getMethod("getY");
                    getX.setAccessible(true);
                    getY.setAccessible(true);
                    int nx = (Integer) getX.invoke(wp);
                    int ny = (Integer) getY.invoke(wp);
                    dist = Math.max(Math.abs(nx - playerX), Math.abs(ny - playerY));
                }
            } catch (Throwable ignored) {}

            try {
                Method getHealthRatio = npc.getClass().getMethod("getHealthRatio");
                Method getHealthScale = npc.getClass().getMethod("getHealthScale");
                getHealthRatio.setAccessible(true);
                getHealthScale.setAccessible(true);
                int ratio = (Integer) getHealthRatio.invoke(npc);
                int scale = (Integer) getHealthScale.invoke(npc);
                if (scale > 0 && ratio >= 0) {
                    health = (ratio * 100 / scale) + "%";
                }
            } catch (Throwable ignored) {}

            data.append("NPC[").append(index).append("]: ").append(id).append(",").append(name).append(",").append(dist).append(",").append(health).append("\n");
        } catch (Throwable ignored) {}
    }

    private static String extractNpcName(Object client, Object npc, int id) {
        if (npc == null) return "Unknown";

        // Strategy 1: Direct getName() on npc
        try {
            Method getName = npc.getClass().getMethod("getName");
            getName.setAccessible(true);
            Object res = getName.invoke(npc);
            if (res instanceof String) {
                String s = (String) res;
                if (isValidNpcName(s)) return cleanNpcName(s);
            }
        } catch (Throwable ignored) {}

        // Strategy 2: Via composition or definition
        String[] compMethods = {"getComposition", "getTransformedComposition", "getDefinition", "npcComposition"};
        for (String cMethod : compMethods) {
            try {
                Method getComp = npc.getClass().getMethod(cMethod);
                getComp.setAccessible(true);
                Object comp = getComp.invoke(npc);
                if (comp != null) {
                    try {
                        Method compGetName = comp.getClass().getMethod("getName");
                        compGetName.setAccessible(true);
                        Object res = compGetName.invoke(comp);
                        if (res instanceof String) {
                            String s = (String) res;
                            if (isValidNpcName(s)) return cleanNpcName(s);
                        }
                    } catch (Throwable ignored) {}

                    for (Field f : comp.getClass().getDeclaredFields()) {
                        if (f.getType() == String.class) {
                            f.setAccessible(true);
                            Object res = f.get(comp);
                            if (res instanceof String) {
                                String s = (String) res;
                                if (isValidNpcName(s)) return cleanNpcName(s);
                            }
                        }
                    }
                }
            } catch (Throwable ignored) {}
        }

        // Strategy 3: Check declared fields on NPC object
        for (Field f : npc.getClass().getDeclaredFields()) {
            try {
                f.setAccessible(true);
                Object val = f.get(npc);
                if (val != null) {
                    if (val instanceof String) {
                        String s = (String) val;
                        if (isValidNpcName(s)) return cleanNpcName(s);
                    } else if (!f.getType().isPrimitive()) {
                        try {
                            Method compGetName = val.getClass().getMethod("getName");
                            compGetName.setAccessible(true);
                            Object res = compGetName.invoke(val);
                            if (res instanceof String) {
                                String s = (String) res;
                                if (isValidNpcName(s)) return cleanNpcName(s);
                            }
                        } catch (Throwable ignored) {}
                    }
                }
            } catch (Throwable ignored) {}
        }

        // Strategy 4: Query client for NPC definition by id
        if (client != null && id > 0) {
            String[] clientDefMethods = {"getNpcDefinition", "getNpcComposition", "loadNPCComposition"};
            for (String mName : clientDefMethods) {
                try {
                    Method m = client.getClass().getMethod(mName, int.class);
                    m.setAccessible(true);
                    Object comp = m.invoke(client, id);
                    if (comp != null) {
                        try {
                            Method compGetName = comp.getClass().getMethod("getName");
                            compGetName.setAccessible(true);
                            Object res = compGetName.invoke(comp);
                            if (res instanceof String) {
                                String s = (String) res;
                                if (isValidNpcName(s)) return cleanNpcName(s);
                            }
                        } catch (Throwable ignored) {}
                    }
                } catch (Throwable ignored) {}
            }
        }

        return id > 0 ? "NPC_" + id : "Unknown";
    }

    private static boolean isValidNpcName(String s) {
        if (s == null) return false;
        String t = s.trim();
        return !t.isEmpty() && !t.equalsIgnoreCase("null") && !t.equalsIgnoreCase("null-name") && !t.equalsIgnoreCase("Unknown") && t.length() < 60;
    }

    private static String cleanNpcName(String s) {
        return s.replace('\u00A0', ' ').replaceAll("<[^>]*>", "").trim();
    }

    private static void processObfuscatedClient(Instrumentation inst, StringBuilder data) {
        if (foundClientClass != null) {
            data.append("Client Class: ").append(foundClientClass).append("\n");
        } else {
            data.append("Client Class: OSRS-Active\n");
        }

        // GameState
        int gs = 0;
        if (gameStateField != null) {
            try {
                int raw = gameStateField.getInt(null);
                int decoded = raw * gameStateMultiplier;
                if (decoded == 10 || decoded == 11 || decoded == 20 || decoded == 25 || decoded == 30 || decoded == 40 || decoded == 45) {
                    gs = decoded;
                } else {
                    for (int m : MULTIPLIERS) {
                        int test = raw * m;
                        if (test == 10 || test == 11 || test == 20 || test == 25 || test == 30 || test == 40 || test == 45) {
                            gameStateMultiplier = m;
                            gs = test;
                            break;
                        }
                    }
                }
            } catch (Exception ignored) {}
        }

        // LocalPlayer data extraction
        Object player = null;
        if (localPlayerField != null) {
            try {
                player = localPlayerField.get(null);
                if (player != null) {
                    // Extract player name
                    for (Field pf : player.getClass().getDeclaredFields()) {
                        if (pf.getType() == String.class) {
                            pf.setAccessible(true);
                            String name = (String) pf.get(player);
                            if (name != null && !name.isEmpty() && !name.contains("<") && name.length() < 20) {
                                data.append("PLAYER_NAME: ").append(name).append("\n");
                                if (gs == 0) gs = 30; // Infer logged in
                                break;
                            }
                        }
                    }

                    // Extract coordinates
                    if (playerXField == null || playerYField == null) {
                        int coordCount = 0;
                        for (Field pf : player.getClass().getDeclaredFields()) {
                            if (Modifier.isStatic(pf.getModifiers()) || pf.getType() != int.class) continue;
                            pf.setAccessible(true);
                            int val = pf.getInt(player);
                            for (int m : MULTIPLIERS) {
                                int decoded = val * m;
                                if (decoded > 1000 && decoded < 25000) {
                                    if (coordCount == 0) playerXField = pf;
                                    else if (coordCount == 1) playerYField = pf;
                                    coordCount++;
                                    break;
                                }
                            }
                            if (coordCount >= 2) break;
                        }
                    }

                    if (playerXField != null && playerYField != null) {
                        try {
                            int rawX = playerXField.getInt(player);
                            int rawY = playerYField.getInt(player);
                            int x = 0, y = 0;
                            for (int m : MULTIPLIERS) {
                                if (x == 0 && (rawX * m) > 1000 && (rawX * m) < 25000) x = rawX * m;
                                if (y == 0 && (rawY * m) > 1000 && (rawY * m) < 25000) y = rawY * m;
                            }
                            if (x > 0 && y > 0) {
                                data.append("PLAYER_X: ").append(x).append("\n");
                                data.append("PLAYER_Y: ").append(y).append("\n");
                                data.append("LOCATION: (").append(x).append(", ").append(y).append(")\n");
                                data.append("LOCATION_STATUS: Connected\n");
                            }
                        } catch (Exception ignored) {}
                    }
                }
            } catch (Exception ignored) {}
        }

        // Skills data extraction
        if (skillsField != null) {
            try {
                int[] skills = (int[]) skillsField.get(null);
                if (skills != null) {
                    for (int i = 0; i < Math.min(skills.length, SKILL_NAMES.length); i++) {
                        int val = skills[i];
                        int decoded = val;
                        if (val < 1 || val > 125) {
                            for (int m : MULTIPLIERS) {
                                int d = val * m;
                                if (d >= 1 && d <= 125) {
                                    decoded = d;
                                    break;
                                }
                            }
                        }
                        data.append("SKILL[").append(SKILL_NAMES[i]).append("]: ").append(decoded).append("\n");
                    }
                    if (gs == 0) gs = 30; // Infer logged in if skills are present
                }
            } catch (Exception ignored) {}
        }

        // Format GameState and ENGINE_STATE
        data.append("GameState: ").append(gs).append("\n");
        String stateStr = "Detecting...";
        if (gs == 30) stateStr = "Logged In";
        else if (gs == 10 || gs == 11) stateStr = "Login Screen";
        else if (gs == 20) stateStr = "Logging In";
        else if (gs == 25) stateStr = "Loading";
        else if (gs == 45) stateStr = "Hopping";
        else if (gs == 40) stateStr = "Connection Lost";
        else if (gs == 1) stateStr = "Starting";

        data.append("ENGINE_STATE: ").append(stateStr).append("\n");

        // World
        if (worldField != null) {
            try {
                int raw = worldField.getInt(null);
                int world = raw * worldMultiplier;
                if (world > 0 && world < 300) world += 300;
                if (world >= 300 && world <= 600) {
                    data.append("WORLD: ").append(world).append("\n");
                }
            } catch (Exception ignored) {}
        }

        // Current Tab
        if (currentTabField != null) {
            try {
                int raw = currentTabField.getInt(null);
                int tab = raw * currentTabMultiplier;
                if (tab >= 0 && tab <= 14) {
                    data.append("CURRENT_TAB: ").append(tab).append("\n");
                }
            } catch (Exception ignored) {}
        }

        // Inventory
        if (inventoryIdsField != null && inventoryQuantitiesField != null) {
            try {
                int[] ids = (int[]) inventoryIdsField.get(null);
                int[] qtys = (int[]) inventoryQuantitiesField.get(null);
                if (ids != null && qtys != null) {
                    int maxSlots = Math.min(28, Math.min(ids.length, qtys.length));
                    for (int i = 0; i < maxSlots; i++) {
                        int id = ids[i];
                        int qty = qtys[i];
                        if (id < 0 || id > 50000) {
                            for (int m : MULTIPLIERS) {
                                int decoded = id * m;
                                if (decoded >= 0 && decoded < 50000) { id = decoded; break; }
                            }
                        }
                        if (qty < 0 || qty > 2147483647) {
                            for (int m : MULTIPLIERS) {
                                int decoded = qty * m;
                                if (decoded >= 0 && decoded < 2147483647) { qty = decoded; break; }
                            }
                        }
                        data.append("INV[").append(i).append("]: ").append(id).append(",").append(qty).append("\n");
                    }
                }
            } catch (Exception ignored) {}
        }

        // Equipment
        if (equipmentField != null) {
            try {
                int[] equipment = (int[]) equipmentField.get(null);
                if (equipment != null) {
                    for (int i = 0; i < equipment.length; i++) {
                        if (equipment[i] > 0) {
                            data.append("EQUIP[").append(i).append("]: ").append(equipment[i]).append(",1\n");
                        } else {
                            data.append("EQUIP[").append(i).append("]: 0,0\n");
                        }
                    }
                }
            } catch (Exception ignored) {}
        }

        // NPCs
        if (npcListField != null) {
            try {
                Object[] npcs = (Object[]) npcListField.get(null);
                if (npcs != null) {
                    int count = 0;
                    for (Object npc : npcs) {
                        if (npc != null && count < 25) {
                            String name = extractNpcName(null, npc, -1);
                            data.append("NPC[").append(count).append("]: 0,").append(name).append(",0,100%\n");
                            count++;
                        }
                    }
                    data.append("TOTAL_NPCS: ").append(count).append("\n");
                }
            } catch (Exception ignored) {}
        }
    }
}
