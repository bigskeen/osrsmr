package com.osrsmr.agent;

import java.lang.instrument.Instrumentation;
import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.lang.reflect.Modifier;
import java.net.Socket;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;
import java.util.Collection;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;
import java.util.ArrayList;
import java.util.Map;
import java.util.LinkedHashMap;
import java.util.concurrent.ConcurrentHashMap;

public class BytecodeAgent {
    private static final String VERSION = "1.3.4";
    private static final int PORT = 43594;
    private static volatile Thread heartbeatThread = null;
    private static final String JVM_PID = getPidInternal();
    private static final ConcurrentHashMap<Integer, String> ITEM_NAME_CACHE = new ConcurrentHashMap<>();
    private static final ConcurrentHashMap<Integer, String> OBJECT_NAME_CACHE = new ConcurrentHashMap<>();

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
    private static volatile Object runeLiteClient = null;
    private static volatile Object runeLiteInjector = null;
    private static volatile Object runeLiteItemManager = null;

    private static final String[] SKILL_NAMES = {
        "Attack", "Defence", "Strength", "Hitpoints", "Ranged", "Prayer", "Magic", "Cooking",
        "Woodcutting", "Fletching", "Fishing", "Firemaking", "Crafting", "Smithing", "Mining",
        "Herblore", "Agility", "Thieving", "Slayer", "Farming", "Runecraft", "Hunter", "Construction", "Sailing"
    };

    public static void premain(String agentArgs, Instrumentation inst) {
        initialize(inst);
    }

    public static void agentmain(String agentArgs, Instrumentation inst) {
        initialize(inst);
    }

    private static synchronized void initialize(Instrumentation inst) {
        String sunJavaCmd = System.getProperty("sun.java.command", "");
        if (sunJavaCmd.contains("com.osrsmr.attach.AttachHelper")) {
            return;
        }

        if (heartbeatThread != null && heartbeatThread.isAlive()) {
            System.out.println("[osrsmr] Agent already active and running (PID " + JVM_PID + ")");
            return;
        }

        heartbeatThread = new Thread(() -> {
            try {
                // Wait briefly for client initialization
                Thread.sleep(500);
                System.out.println("[osrsmr] Starting Discovery Agent v" + VERSION + " (PID " + JVM_PID + ")...");

                Socket socket = null;
                OutputStream out = null;

                while (true) {
                    try {
                        // 1. Scan loaded classes for RuneLite client
                        try {
                            scanAndDiscover(inst);
                        } catch (Throwable ignored) {}

                        // Establish / maintain TCP socket to Bridge only when RuneLite Client is discovered
                        if (runeLiteClient == null) {
                            if (socket != null) {
                                try { socket.close(); } catch (Exception ignored) {}
                                socket = null;
                                out = null;
                            }
                            Thread.sleep(500);
                            continue;
                        }

                        if (socket == null || socket.isClosed() || !socket.isConnected()) {
                            try {
                                socket = new Socket("127.0.0.1", PORT);
                                socket.setTcpNoDelay(true);
                                socket.setSendBufferSize(65536);
                                out = socket.getOutputStream();
                                System.out.println("[osrsmr] Connected to Bridge on port " + PORT + " (PID " + JVM_PID + ")");
                            } catch (Exception e) {
                                // Bridge not listening yet
                                Thread.sleep(1000);
                                continue;
                            }
                        }

                        StringBuilder data = new StringBuilder();
                        data.append("PID: ").append(JVM_PID).append("\n");
                        data.append("Status: Hook Active (v").append(VERSION).append(")\n");

                        // 2. RuneLite API Extraction
                        try {
                            processRuneLiteClient(runeLiteClient, data);
                        } catch (Throwable t) {
                            // Do not discard runeLiteClient on transient extraction error
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
                                out = null;
                            }
                        }
                    } catch (Throwable t) {
                        // Keep agent loop alive
                    }
                    Thread.sleep(400);
                }
            } catch (Exception e) {
                e.printStackTrace();
            }
        }, "osrsmr-heartbeat");
        heartbeatThread.setDaemon(true);
        heartbeatThread.start();
    }

    private static void scanAndDiscover(Instrumentation inst) {
        if ((runeLiteClient != null && runeLiteItemManager != null) || inst == null) {
            return;
        }
        try {
            Class<?>[] allLoaded = inst.getAllLoadedClasses();

            // Try to find RuneLite Client instance
            if (runeLiteClient == null || runeLiteItemManager == null) {
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
                                runeLiteInjector = injector;

                                // Acquire ItemManager from Injector
                                if (runeLiteItemManager == null) {
                                    try {
                                        Class<?> itemMgrClass = null;
                                        try {
                                            itemMgrClass = Class.forName("net.runelite.client.game.ItemManager", false, clazz.getClassLoader());
                                        } catch (Throwable ignored) {}
                                        if (itemMgrClass == null) {
                                            for (Class<?> c : allLoaded) {
                                                if (c.getName().equals("net.runelite.client.game.ItemManager")) {
                                                    itemMgrClass = c;
                                                    break;
                                                }
                                            }
                                        }
                                        if (itemMgrClass != null) {
                                            for (Method m : injector.getClass().getMethods()) {
                                                if (m.getName().equals("getInstance") && m.getParameterCount() == 1 && m.getParameterTypes()[0] == Class.class) {
                                                    try {
                                                        m.setAccessible(true);
                                                        Object mgr = m.invoke(injector, itemMgrClass);
                                                        if (mgr != null) {
                                                            runeLiteItemManager = mgr;
                                                            System.out.println("[osrsmr] RuneLite ItemManager acquired via Injector.getInstance(ItemManager.class)");
                                                            break;
                                                        }
                                                    } catch (Throwable ignored) {}
                                                }
                                            }
                                        }
                                    } catch (Throwable ignored) {}
                                }

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

                                if (runeLiteClient == null || runeLiteItemManager == null) {
                                    try {
                                        Method getAllBindings = injector.getClass().getMethod("getAllBindings");
                                        getAllBindings.setAccessible(true);
                                        Map<?, ?> map = (Map<?, ?>) getAllBindings.invoke(injector);
                                        if (map != null) {
                                            for (Map.Entry<?, ?> entry : map.entrySet()) {
                                                String keyStr = String.valueOf(entry.getKey());
                                                if (runeLiteClient == null && (keyStr.contains("net.runelite.api.Client") || keyStr.contains("RSClient"))) {
                                                    try {
                                                        Object binding = entry.getValue();
                                                        Method getProvider = binding.getClass().getMethod("getProvider");
                                                        getProvider.setAccessible(true);
                                                        Object provider = getProvider.invoke(binding);
                                                        if (provider != null) {
                                                            Method get = provider.getClass().getMethod("get");
                                                            get.setAccessible(true);
                                                            Object val = get.invoke(provider);
                                                            if (val != null && isRuneLiteClientObject(val)) {
                                                                runeLiteClient = val;
                                                                System.out.println("[osrsmr] RuneLite Client acquired via Injector binding " + keyStr);
                                                            }
                                                        }
                                                    } catch (Throwable ignored) {}
                                                }
                                                if (runeLiteItemManager == null && keyStr.contains("ItemManager")) {
                                                    try {
                                                        Object binding = entry.getValue();
                                                        Method getProvider = binding.getClass().getMethod("getProvider");
                                                        getProvider.setAccessible(true);
                                                        Object provider = getProvider.invoke(binding);
                                                        if (provider != null) {
                                                            Method get = provider.getClass().getMethod("get");
                                                            get.setAccessible(true);
                                                            Object val = get.invoke(provider);
                                                            if (val != null) {
                                                                runeLiteItemManager = val;
                                                                System.out.println("[osrsmr] RuneLite ItemManager acquired via Injector binding " + keyStr);
                                                            }
                                                        }
                                                    } catch (Throwable ignored) {}
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
                    if (runeLiteClient != null && runeLiteItemManager != null) break;
                }

                // Method 2: Check static fields & singleton objects in all loaded classes
                if (runeLiteClient == null || runeLiteItemManager == null) {
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

                                    if (runeLiteClient == null && isRuneLiteClientObject(val)) {
                                        runeLiteClient = val;
                                        System.out.println("[osrsmr] RuneLite Client instance discovered in " + clazz.getName() + "." + f.getName());
                                    }

                                    // Check fields of singleton / manager objects
                                    String typeName = val.getClass().getName();
                                    if (typeName.startsWith("net.runelite.") || typeName.equals("client") || typeName.contains("ClientLoader")) {
                                        for (Field innerF : val.getClass().getDeclaredFields()) {
                                            if (!innerF.getType().isPrimitive()) {
                                                innerF.setAccessible(true);
                                                Object innerVal = innerF.get(val);
                                                if (runeLiteClient == null && innerVal != null && isRuneLiteClientObject(innerVal)) {
                                                    runeLiteClient = innerVal;
                                                    System.out.println("[osrsmr] RuneLite Client discovered in " + typeName + "." + innerF.getName());
                                                }
                                                if (runeLiteItemManager == null && innerVal != null && innerVal.getClass().getName().contains("ItemManager")) {
                                                    runeLiteItemManager = innerVal;
                                                    System.out.println("[osrsmr] RuneLite ItemManager discovered in " + typeName + "." + innerF.getName());
                                                }
                                            }
                                        }
                                    }
                                } catch (Throwable ignored) {}
                            }
                        }
                        if (runeLiteClient != null && runeLiteItemManager != null) break;
                    }
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

        // Exclude UI and auxiliary components
        if (cName.startsWith("net.runelite.client.ui.")
                || cName.startsWith("net.runelite.client.plugins.")
                || cName.startsWith("net.runelite.client.config.")
                || cName.startsWith("net.runelite.client.chat.")
                || cName.startsWith("net.runelite.client.task.")
                || cName.startsWith("net.runelite.client.util.")
                || cName.startsWith("net.runelite.client.menus.")
                || cName.startsWith("net.runelite.client.discord.")
                || cName.startsWith("net.runelite.client.ws.")
                || cName.startsWith("net.runelite.client.events.")
                || cName.equals("net.runelite.client.RuneLite")
                || cName.contains("SessionManager")
                || cName.contains("Toolbar")
                || cName.contains("Panel")
                || cName.contains("Loader")
                || cName.contains("Thread")
                || cName.contains("Manager")
                || cName.contains("Injector")) {
            return false;
        }

        // Direct class or interface matching
        if (cName.equals("client") || cName.equals("net.runelite.client.Client")
                || cName.equals("net.runelite.api.Client") || cName.equals("net.runelite.rs.api.RSClient")) {
            return true;
        }

        for (Class<?> iface : cls.getInterfaces()) {
            String iName = iface.getName();
            if (iName.equals("net.runelite.api.Client") || iName.equals("net.runelite.rs.api.RSClient") || iName.equals("client")) {
                return true;
            }
        }

        Class<?> curr = cls.getSuperclass();
        while (curr != null && curr != Object.class) {
            String sName = curr.getName();
            if (sName.equals("client") || sName.equals("net.runelite.rs.api.RSClient") || sName.equals("net.runelite.api.Client")) {
                return true;
            }
            for (Class<?> iface : curr.getInterfaces()) {
                String iName = iface.getName();
                if (iName.equals("net.runelite.api.Client") || iName.equals("net.runelite.rs.api.RSClient")) {
                    return true;
                }
            }
            curr = curr.getSuperclass();
        }

        // Method-based validation: Must have getGameState() AND one core client method
        try {
            Method m1 = cls.getMethod("getGameState");
            if (m1 != null) {
                for (String mName : new String[]{"getLocalPlayer", "getCanvas", "getTopLevelWorldView", "getItemContainer", "getPlane"}) {
                    try {
                        Method m2 = cls.getMethod(mName);
                        if (m2 != null) return true;
                    } catch (Throwable ignored) {}
                }
            }
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

            // When player is not yet in-game, do not emit uninitialized zeroes/containers
            if (gs != 30 && player == null) {
                return true;
            }

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
            } else {
                try {
                    Class<?> skillEnumClass = null;
                    try {
                        skillEnumClass = Class.forName("net.runelite.api.Skill", false, client.getClass().getClassLoader());
                    } catch (Throwable ignored) {}
                    if (skillEnumClass != null && skillEnumClass.isEnum()) {
                        Object[] skillConstants = skillEnumClass.getEnumConstants();
                        Method getRealSkill = null;
                        Method getBoostedSkill = null;
                        try { getRealSkill = client.getClass().getMethod("getRealSkillLevel", skillEnumClass); } catch (Throwable ignored) {}
                        try { getBoostedSkill = client.getClass().getMethod("getBoostedSkillLevel", skillEnumClass); } catch (Throwable ignored) {}

                        if (getRealSkill != null) {
                            for (Object sc : skillConstants) {
                                try {
                                    String sName = ((Enum<?>) sc).name();
                                    int real = (Integer) getRealSkill.invoke(client, sc);
                                    int boosted = getBoostedSkill != null ? (Integer) getBoostedSkill.invoke(client, sc) : real;
                                    data.append("SKILL[").append(sName).append("]: ").append(boosted).append("/").append(real).append("\n");
                                } catch (Throwable ignored) {}
                            }
                        }
                    }
                } catch (Throwable ignored) {}
            }

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
            try { readRuneLiteItemContainer(client, 93, "INV", 28, data); } catch (Throwable ignored) {}
            try { readRuneLiteItemContainer(client, 94, "EQUIP", 14, data); } catch (Throwable ignored) {}

            // Magic & Prayers
            try { processRuneLiteMagic(client, data); } catch (Throwable ignored) {}
            try { processRuneLitePrayers(client, data); } catch (Throwable ignored) {}

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
                    int fishCount = 0;
                    try {
                        if (npcsObj instanceof Iterable) {
                            for (Object npc : (Iterable<?>) npcsObj) {
                                if (npc != null) {
                                    if (count < 25) {
                                        appendRuneLiteNpc(client, npc, count, playerX, playerY, data);
                                        count++;
                                    }
                                    if (isFishingSpotNpc(client, npc) && fishCount < 20) {
                                        appendRuneLiteFishingSpot(client, npc, fishCount, playerX, playerY, data);
                                        fishCount++;
                                    }
                                }
                            }
                        } else if (npcsObj instanceof Object[]) {
                            Object[] npcArr = (Object[]) npcsObj;
                            for (Object npc : npcArr) {
                                if (npc != null) {
                                    if (count < 25) {
                                        appendRuneLiteNpc(client, npc, count, playerX, playerY, data);
                                        count++;
                                    }
                                    if (isFishingSpotNpc(client, npc) && fishCount < 20) {
                                        appendRuneLiteFishingSpot(client, npc, fishCount, playerX, playerY, data);
                                        fishCount++;
                                    }
                                }
                            }
                        }
                    } catch (Throwable ignored) {}
                    data.append("TOTAL_NPCS: ").append(count).append("\n");
                    data.append("TOTAL_FISHING_SPOTS: ").append(fishCount).append("\n");
                }
            } catch (Throwable ignored) {}

            // Players
            try {
                Object playersObj = null;
                // Check modern RuneLite WorldView first
                try {
                    Method getTopView = client.getClass().getMethod("getTopLevelWorldView");
                    getTopView.setAccessible(true);
                    Object topView = getTopView.invoke(client);
                    if (topView != null) {
                        for (String mName : new String[]{"players", "getPlayers"}) {
                            try {
                                Method m = topView.getClass().getMethod(mName);
                                m.setAccessible(true);
                                playersObj = m.invoke(topView);
                                if (playersObj != null) break;
                            } catch (Throwable ignored) {}
                        }
                    }
                } catch (Throwable ignored) {}

                // Fallback to direct client call
                if (playersObj == null) {
                    for (String mName : new String[]{"getPlayers", "players"}) {
                        try {
                            Method m = client.getClass().getMethod(mName);
                            m.setAccessible(true);
                            playersObj = m.invoke(client);
                            if (playersObj != null) break;
                        } catch (Throwable ignored) {}
                    }
                }

                if (playersObj != null) {
                    int count = 0;
                    try {
                        if (playersObj instanceof Iterable) {
                            for (Object p : (Iterable<?>) playersObj) {
                                if (p != null && count < 25) {
                                    appendRuneLitePlayer(client, p, count, playerX, playerY, player, data);
                                    count++;
                                }
                            }
                        } else if (playersObj instanceof Object[]) {
                            Object[] pArr = (Object[]) playersObj;
                            for (Object p : pArr) {
                                if (p != null && count < 25) {
                                    appendRuneLitePlayer(client, p, count, playerX, playerY, player, data);
                                    count++;
                                }
                            }
                        }
                    } catch (Throwable ignored) {}
                    data.append("TOTAL_PLAYERS: ").append(count).append("\n");
                }
            } catch (Throwable ignored) {}

            // Plane & Camera & FPS
            try {
                Method getPlane = client.getClass().getMethod("getPlane");
                getPlane.setAccessible(true);
                plane = (Integer) getPlane.invoke(client);
                data.append("PLANE: ").append(plane).append("\n");
            } catch (Throwable ignored) {}

            try {
                Method getFPS = client.getClass().getMethod("getFPS");
                getFPS.setAccessible(true);
                data.append("FPS: ").append(getFPS.invoke(client)).append("\n");
            } catch (Throwable ignored) {}

            // Special Attack
            try { processRuneLiteSpecialAttack(client, data); } catch (Throwable ignored) {}

            // Slayer
            try { processRuneLiteSlayer(client, data); } catch (Throwable ignored) {}

            // Dialog
            try { processRuneLiteDialog(client, data); } catch (Throwable ignored) {}

            // Bank and Shop
            try { processRuneLiteBankAndShop(client, data); } catch (Throwable ignored) {}

            // Scene Objects (Trees, Banks, Shops, Altars, Rocks, Shortcuts, Agility Obstacles) & Ground Items
            try { processRuneLiteSceneObjects(client, playerX, playerY, plane, data); } catch (Throwable ignored) {}

            // Minigames (Pest Control, Wintertodt, Tempoross, GotR, BA, Castle Wars, etc.)
            try { processRuneLiteMinigames(client, playerX, playerY, data); } catch (Throwable ignored) {}

            return true;
        } catch (Throwable t) {
            return false;
        }
    }

    private static final String[] STANDARD_PRAYER_NAMES = {
        "Thick Skin", "Burst of Strength", "Clarity of Thought", "Sharp Eye", "Mystic Will",
        "Rock Skin", "Superhuman Strength", "Improved Reflexes", "Rapid Restore", "Rapid Heal",
        "Protect Item", "Hawk Eye", "Mystic Lore", "Steel Skin", "Ultimate Strength",
        "Incredible Reflexes", "Protect from Magic", "Protect from Missiles", "Protect from Melee",
        "Eagle Eye", "Mystic Might", "Retribution", "Redemption", "Smite",
        "Preserve", "Chivalry", "Piety", "Rigour", "Augury"
    };

    private static int getVarbitValue(Object client, int varbitId) {
        if (client == null || varbitId < 0) return -1;
        // 1. Direct 1-arg getVarbitValue/getVarbit
        for (String mName : new String[]{"getVarbitValue", "getVarbit", "getVar"}) {
            try {
                Method m = client.getClass().getMethod(mName, int.class);
                m.setAccessible(true);
                Object res = m.invoke(client, varbitId);
                if (res instanceof Number) {
                    return ((Number) res).intValue();
                }
            } catch (Throwable ignored) {}
        }

        // 2. 2-arg getVarbitValue(int[] varps, int varbitId)
        try {
            int[] varps = null;
            for (String vName : new String[]{"getVarps", "getServerVarps"}) {
                try {
                    Method vm = client.getClass().getMethod(vName);
                    vm.setAccessible(true);
                    Object r = vm.invoke(client);
                    if (r instanceof int[]) {
                        varps = (int[]) r;
                        break;
                    }
                } catch (Throwable ignored) {}
            }
            if (varps != null) {
                for (String mName : new String[]{"getVarbitValue", "getVarbit"}) {
                    try {
                        Method m = client.getClass().getMethod(mName, int[].class, int.class);
                        m.setAccessible(true);
                        Object res = m.invoke(client, varps, varbitId);
                        if (res instanceof Number) {
                            return ((Number) res).intValue();
                        }
                    } catch (Throwable ignored) {}
                }
            }
        } catch (Throwable ignored) {}

        // 3. Scan all methods matching varbit signature
        for (Method m : client.getClass().getMethods()) {
            if (m.getName().toLowerCase().contains("varbit") && m.getParameterCount() == 1 && m.getParameterTypes()[0] == int.class) {
                try {
                    m.setAccessible(true);
                    Object res = m.invoke(client, varbitId);
                    if (res instanceof Number) {
                        return ((Number) res).intValue();
                    }
                } catch (Throwable ignored) {}
            }
        }
        return -1;
    }

    private static int getVarpValue(Object client, int varpId) {
        if (client == null || varpId < 0) return -1;
        for (String mName : new String[]{"getVarpValue", "getVarp", "getVarpValueInt", "getVar"}) {
            try {
                Method m = client.getClass().getMethod(mName, int.class);
                m.setAccessible(true);
                Object res = m.invoke(client, varpId);
                if (res instanceof Number) {
                    return ((Number) res).intValue();
                }
            } catch (Throwable ignored) {}
        }
        // Direct varps array lookup fallback
        try {
            for (String vName : new String[]{"getVarps", "getServerVarps"}) {
                try {
                    Method vm = client.getClass().getMethod(vName);
                    vm.setAccessible(true);
                    Object r = vm.invoke(client);
                    if (r instanceof int[]) {
                        int[] arr = (int[]) r;
                        if (varpId >= 0 && varpId < arr.length) {
                            return arr[varpId];
                        }
                    }
                } catch (Throwable ignored) {}
            }
        } catch (Throwable ignored) {}
        return -1;
    }

    private static String getAutocastSpellName(int id) {
        switch (id) {
            case 1: return "Wind Strike";
            case 2: return "Water Strike";
            case 3: return "Earth Strike";
            case 4: return "Fire Strike";
            case 5: return "Wind Bolt";
            case 6: return "Water Bolt";
            case 7: return "Earth Bolt";
            case 8: return "Fire Bolt";
            case 9: return "Wind Blast";
            case 10: return "Water Blast";
            case 11: return "Earth Blast";
            case 12: return "Fire Blast";
            case 13: return "Wind Wave";
            case 14: return "Water Wave";
            case 15: return "Earth Wave";
            case 16: return "Fire Wave";
            case 17: return "Crumble Undead";
            case 18: return "Iban Blast";
            case 19: return "Magic Dart";
            case 20: return "Saradomin Strike";
            case 21: return "Claws of Guthix";
            case 22: return "Flames of Zamorak";
            case 23: return "Slayer Dart";
            case 31: return "Smoke Rush";
            case 32: return "Shadow Rush";
            case 33: return "Blood Rush";
            case 34: return "Ice Rush";
            case 35: return "Smoke Burst";
            case 36: return "Shadow Burst";
            case 37: return "Blood Burst";
            case 38: return "Ice Burst";
            case 39: return "Smoke Blitz";
            case 40: return "Shadow Blitz";
            case 41: return "Blood Blitz";
            case 42: return "Ice Blitz";
            case 43: return "Smoke Barrage";
            case 44: return "Shadow Barrage";
            case 45: return "Blood Barrage";
            case 46: return "Ice Barrage";
            case 48: return "Wind Surge";
            case 49: return "Water Surge";
            case 50: return "Earth Surge";
            case 51: return "Fire Surge";
            case 52: return "Infernal Teleport";
            case 53: return "Ghostly Grasp";
            case 54: return "Skeletal Grasp";
            case 55: return "Undead Grasp";
            case 56: return "Infernal Grasp";
            case 57: return "Lesser Demonbane";
            case 58: return "Superior Demonbane";
            case 59: return "Dark Demonbane";
            case 60: return "Lesser Corruption";
            case 61: return "Greater Corruption";
            case 62: return "Shadow Veil";
            case 63: return "Ward of Arceuus";
            default: return id > 0 ? ("Spell #" + id) : "None";
        }
    }

    private static String formatEnumPrayerName(String enumName) {
        if (enumName == null) return "";
        String[] parts = enumName.toLowerCase().split("_");
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < parts.length; i++) {
            String p = parts[i];
            if (p.isEmpty()) continue;
            if (i > 0) {
                sb.append(" ");
                if (p.equals("from") || p.equals("of")) {
                    sb.append(p);
                    continue;
                }
            }
            sb.append(Character.toUpperCase(p.charAt(0))).append(p.substring(1));
        }
        return sb.toString();
    }

    private static void processRuneLiteMagic(Object client, StringBuilder data) {
        if (client == null) return;
        try {
            // 1. Spellbook (Varbit 4070 / SPELLBOOK_VARBIT)
            int spellbookId = getVarbitValue(client, 4070);
            if (spellbookId < 0) {
                // Fallback to Varp 1224 (bits 0-2)
                int varp1224 = getVarpValue(client, 1224);
                if (varp1224 >= 0) {
                    spellbookId = (varp1224 & 0x7);
                }
            }

            String spellbookName;
            switch (spellbookId) {
                case 0: spellbookName = "Standard"; break;
                case 1: spellbookName = "Ancient Magicks"; break;
                case 2: spellbookName = "Lunar"; break;
                case 3: spellbookName = "Arceuus"; break;
                case 4: spellbookName = "Ancient (Swap)"; break;
                case 5: spellbookName = "Lunar (Swap)"; break;
                case 6: spellbookName = "Arceuus (Swap)"; break;
                default: spellbookName = spellbookId >= 0 ? ("Spellbook " + spellbookId) : "Standard"; break;
            }
            data.append("SPELLBOOK: ").append(spellbookName).append("\n");
            if (spellbookId >= 0) {
                data.append("SPELLBOOK_ID: ").append(spellbookId).append("\n");
            }

            // 2. Autocast Spell (VarPlayer 276)
            int autocastId = getVarpValue(client, 276);
            String autocastName = getAutocastSpellName(autocastId);
            data.append("AUTOCAST_SPELL: ").append(autocastName).append("\n");
            if (autocastId >= 0) {
                data.append("AUTOCAST_ID: ").append(autocastId).append("\n");
            }

            // 3. Selected / Targeting Spell (e.g. client.getSelectedSpellName())
            String selectedSpell = null;
            try {
                Method getSel = client.getClass().getMethod("getSelectedSpellName");
                getSel.setAccessible(true);
                Object res = getSel.invoke(client);
                if (res instanceof String) {
                    String s = ((String) res).replaceAll("<[^>]*>", "").replace('\u00A0', ' ').trim();
                    if (!s.isEmpty()) {
                        selectedSpell = s;
                    }
                }
            } catch (Throwable ignored) {}
            if (selectedSpell != null) {
                data.append("SELECTED_SPELL: ").append(selectedSpell).append("\n");
            } else {
                data.append("SELECTED_SPELL: None\n");
            }

            // 4. Spell Selected boolean
            boolean isSpellSelected = false;
            try {
                Method getSpellSel = client.getClass().getMethod("getSpellSelected");
                getSpellSel.setAccessible(true);
                Object res = getSpellSel.invoke(client);
                if (res instanceof Boolean) {
                    isSpellSelected = (Boolean) res;
                }
            } catch (Throwable ignored) {}
            data.append("SPELL_SELECTED: ").append(isSpellSelected ? "1" : "0").append("\n");
        } catch (Throwable ignored) {}
    }

    private static void processRuneLitePrayers(Object client, StringBuilder data) {
        if (client == null) return;
        try {
            // 1. Quick Prayer (Varbit 4103)
            int quickPrayer = getVarbitValue(client, 4103);
            data.append("QUICK_PRAYER: ").append(quickPrayer == 1 ? "Active" : "Inactive").append("\n");
            data.append("QUICK_PRAYER_VALUE: ").append(quickPrayer).append("\n");

            // 2. Active Prayers Detection
            List<String> activePrayers = new ArrayList<>();
            Map<String, Boolean> prayerStatus = new LinkedHashMap<>();

            // Initialize all with false
            for (String pName : STANDARD_PRAYER_NAMES) {
                prayerStatus.put(pName, false);
            }

            // Try RuneLite client.isPrayerActive(Prayer) method
            boolean usedRuneLiteEnum = false;
            try {
                ClassLoader cl = client.getClass().getClassLoader();
                Class<?> prayerClass = null;
                try {
                    prayerClass = Class.forName("net.runelite.api.Prayer", true, cl);
                } catch (Throwable t) {
                    for (Method m : client.getClass().getMethods()) {
                        if (m.getName().equals("isPrayerActive") && m.getParameterCount() == 1) {
                            prayerClass = m.getParameterTypes()[0];
                            break;
                        }
                    }
                }

                if (prayerClass != null && prayerClass.isEnum()) {
                    Method isPrayerActive = client.getClass().getMethod("isPrayerActive", prayerClass);
                    isPrayerActive.setAccessible(true);
                    for (Object pConst : prayerClass.getEnumConstants()) {
                        String enumName = pConst.toString();
                        String formattedName = formatEnumPrayerName(enumName);
                        boolean isActive = (Boolean) isPrayerActive.invoke(client, pConst);
                        prayerStatus.put(formattedName, isActive);
                        if (isActive) {
                            activePrayers.add(formattedName);
                        }
                    }
                    usedRuneLiteEnum = true;
                }
            } catch (Throwable ignored) {}

            // Fallback / complement with VarPlayer 83 bitmask
            if (!usedRuneLiteEnum) {
                int prayerMask = getVarpValue(client, 83); // VarPlayer.PRAYER_ACTIVE = 83
                if (prayerMask >= 0) {
                    for (int i = 0; i < STANDARD_PRAYER_NAMES.length; i++) {
                        String pName = STANDARD_PRAYER_NAMES[i];
                        boolean isActive = (prayerMask & (1 << i)) != 0;
                        prayerStatus.put(pName, isActive);
                        if (isActive) {
                            activePrayers.add(pName);
                        }
                    }
                }
            }

            // Telemetry: Active Prayers Summary
            if (activePrayers.isEmpty()) {
                data.append("ACTIVE_PRAYERS: None\n");
            } else {
                StringBuilder activeSb = new StringBuilder();
                for (int i = 0; i < activePrayers.size(); i++) {
                    if (i > 0) activeSb.append(", ");
                    activeSb.append(activePrayers.get(i));
                }
                data.append("ACTIVE_PRAYERS: ").append(activeSb.toString()).append("\n");
            }
            data.append("ACTIVE_PRAYER_COUNT: ").append(activePrayers.size()).append("\n");

            // Telemetry: Individual prayer statuses
            for (Map.Entry<String, Boolean> entry : prayerStatus.entrySet()) {
                data.append("PRAYER[").append(entry.getKey()).append("]: ")
                    .append(entry.getValue() ? "1" : "0").append("\n");
            }
        } catch (Throwable ignored) {}
    }

    private static void readRuneLiteItemContainer(Object client, int containerId, String prefix, int maxItems, StringBuilder data) {
        int[] itemIds = new int[maxItems];
        int[] itemQtys = new int[maxItems];
        String[] itemNames = new String[maxItems];
        for (int i = 0; i < maxItems; i++) {
            itemIds[i] = -1;
            itemQtys[i] = 0;
        }

        boolean found = false;

        // Strategy 1: Direct client.getItemContainer(int) or client.getItemContainer(InventoryID)
        try {
            Object container = null;
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
                                String eName = ((Enum<?>) enumConst).name();
                                boolean match = false;
                                if (containerId == 93 && (eName.equalsIgnoreCase("INVENTORY") || eName.contains("INV"))) match = true;
                                if (containerId == 94 && (eName.equalsIgnoreCase("EQUIPMENT") || eName.contains("EQUIP"))) match = true;
                                if (containerId == 95 && (eName.equalsIgnoreCase("BANK") || eName.contains("BANK"))) match = true;
                                if (!match) {
                                    try {
                                        Method getId = pType.getMethod("getId");
                                        getId.setAccessible(true);
                                        int id = (Integer) getId.invoke(enumConst);
                                        if (id == containerId) match = true;
                                    } catch (Throwable ignored) {}
                                }
                                if (match) {
                                    m.setAccessible(true);
                                    container = m.invoke(client, enumConst);
                                    if (container != null) break;
                                }
                            } catch (Throwable ignored) {}
                        }
                        if (container != null) break;
                    }
                }
            }

            // Strategy 2: client.getItemContainers() HashTable / Map / Collection
            if (container == null) {
                for (Method m : client.getClass().getMethods()) {
                    if (m.getName().startsWith("getItemContainer") && m.getParameterCount() == 0) {
                        try {
                            m.setAccessible(true);
                            Object res = m.invoke(client);
                            if (res != null) {
                                if (res instanceof Map) {
                                    container = ((Map<?, ?>) res).get(containerId);
                                }
                                if (container == null) {
                                    try {
                                        Method getMethod = res.getClass().getMethod("get", long.class);
                                        getMethod.setAccessible(true);
                                        container = getMethod.invoke(res, (long) containerId);
                                    } catch (Throwable ignored) {}
                                }
                                if (container == null) {
                                    try {
                                        Method getMethod = res.getClass().getMethod("get", int.class);
                                        getMethod.setAccessible(true);
                                        container = getMethod.invoke(res, containerId);
                                    } catch (Throwable ignored) {}
                                }
                                if (container == null && res instanceof Iterable) {
                                    for (Object node : (Iterable<?>) res) {
                                        if (node != null) {
                                            try {
                                                Method getId = node.getClass().getMethod("getId");
                                                getId.setAccessible(true);
                                                int nid = (Integer) getId.invoke(node);
                                                if (nid == containerId) {
                                                    container = node;
                                                    break;
                                                }
                                            } catch (Throwable ignored) {}
                                        }
                                    }
                                }
                            }
                            if (container != null) break;
                        } catch (Throwable ignored) {}
                    }
                }
            }

            if (container != null) {
                Object itemsObj = null;
                try {
                    Method getItems = container.getClass().getMethod("getItems");
                    getItems.setAccessible(true);
                    itemsObj = getItems.invoke(container);
                } catch (Throwable ignored) {}

                if (itemsObj instanceof Object[]) {
                    Object[] items = (Object[]) itemsObj;
                    for (int i = 0; i < maxItems && i < items.length; i++) {
                        if (items[i] != null) {
                            Object itm = items[i];
                            int id = -1;
                            int qty = 0;
                            try {
                                Method getId = itm.getClass().getMethod("getId");
                                getId.setAccessible(true);
                                id = (Integer) getId.invoke(itm);
                            } catch (Throwable ignored) {}
                            try {
                                Method getQty = itm.getClass().getMethod("getQuantity");
                                getQty.setAccessible(true);
                                qty = (Integer) getQty.invoke(itm);
                            } catch (Throwable ignored) {}

                            if (id > 0 && id != 65535) {
                                itemIds[i] = id;
                                itemQtys[i] = Math.max(1, qty);
                                found = true;
                            }
                        }
                    }
                } else if (itemsObj instanceof Collection) {
                    int i = 0;
                    for (Object itm : (Collection<?>) itemsObj) {
                        if (i >= maxItems) break;
                        if (itm != null) {
                            int id = -1;
                            int qty = 0;
                            try {
                                Method getId = itm.getClass().getMethod("getId");
                                getId.setAccessible(true);
                                id = (Integer) getId.invoke(itm);
                            } catch (Throwable ignored) {}
                            try {
                                Method getQty = itm.getClass().getMethod("getQuantity");
                                getQty.setAccessible(true);
                                qty = (Integer) getQty.invoke(itm);
                            } catch (Throwable ignored) {}

                            if (id > 0 && id != 65535) {
                                itemIds[i] = id;
                                itemQtys[i] = Math.max(1, qty);
                                found = true;
                            }
                        }
                        i++;
                    }
                }
            }
        } catch (Throwable ignored) {}

        // Strategy 3: RuneLite Widget fallback
        if (!found) {
            try {
                int[][] targetWidgets = containerId == 93
                        ? new int[][]{{149, 0}, {9764864, -1}, {15, 3}, {548, 58}, {161, 58}, {164, 58}}
                        : new int[][]{{387, 0}, {25362432, -1}, {84, 0}, {5505024, -1}};

                for (int[] wSpec : targetWidgets) {
                    Object widget = null;
                    if (wSpec[1] >= 0) {
                        try {
                            Method getWidget = client.getClass().getMethod("getWidget", int.class, int.class);
                            getWidget.setAccessible(true);
                            widget = getWidget.invoke(client, wSpec[0], wSpec[1]);
                        } catch (Throwable ignored) {}
                    } else {
                        try {
                            Method getWidget = client.getClass().getMethod("getWidget", int.class);
                            getWidget.setAccessible(true);
                            widget = getWidget.invoke(client, wSpec[0]);
                        } catch (Throwable ignored) {}
                    }

                    if (widget != null) {
                        // Check dynamic children
                        Object childrenObj = null;
                        for (String mName : new String[]{"getChildren", "getDynamicChildren", "getNestedChildren"}) {
                            try {
                                Method m = widget.getClass().getMethod(mName);
                                m.setAccessible(true);
                                childrenObj = m.invoke(widget);
                                if (childrenObj != null) break;
                            } catch (Throwable ignored) {}
                        }

                        if (childrenObj instanceof Object[]) {
                            Object[] children = (Object[]) childrenObj;
                            if (children.length >= maxItems) {
                                for (int i = 0; i < maxItems && i < children.length; i++) {
                                    if (children[i] != null) {
                                        Object ch = children[i];
                                        int id = -1;
                                        int qty = 0;
                                        String name = null;
                                        try {
                                            Method getId = ch.getClass().getMethod("getItemId");
                                            getId.setAccessible(true);
                                            id = (Integer) getId.invoke(ch);
                                        } catch (Throwable ignored) {}
                                        try {
                                            Method getQty = ch.getClass().getMethod("getItemQuantity");
                                            getQty.setAccessible(true);
                                            qty = (Integer) getQty.invoke(ch);
                                        } catch (Throwable ignored) {}
                                        try {
                                            Method getName = ch.getClass().getMethod("getName");
                                            getName.setAccessible(true);
                                            Object n = getName.invoke(ch);
                                            if (n instanceof String) {
                                                String s = ((String) n).replaceAll("<[^>]*>", "").replace('\u00A0', ' ').trim();
                                                if (!s.isEmpty() && !s.equalsIgnoreCase("null")) name = s;
                                            }
                                        } catch (Throwable ignored) {}

                                        if (id > 0 && id != 65535) {
                                            itemIds[i] = id;
                                            itemQtys[i] = Math.max(1, qty);
                                            itemNames[i] = name;
                                            found = true;
                                        }
                                    }
                                }
                            }
                        }

                        // Check widget.getItems() & widget.getItemQuantities()
                        if (!found) {
                            try {
                                Method getItems = widget.getClass().getMethod("getItems");
                                getItems.setAccessible(true);
                                int[] wItems = (int[]) getItems.invoke(widget);
                                int[] wQtys = null;
                                try {
                                    Method getQtys = widget.getClass().getMethod("getItemQuantities");
                                    getQtys.setAccessible(true);
                                    wQtys = (int[]) getQtys.invoke(widget);
                                } catch (Throwable ignored) {}

                                if (wItems != null && wItems.length >= maxItems) {
                                    for (int i = 0; i < maxItems && i < wItems.length; i++) {
                                        int id = wItems[i];
                                        int qty = (wQtys != null && i < wQtys.length) ? wQtys[i] : 1;
                                        if (id > 0 && id != 65535) {
                                            itemIds[i] = id;
                                            itemQtys[i] = Math.max(1, qty);
                                            found = true;
                                        }
                                    }
                                }
                            } catch (Throwable ignored) {}
                        }
                    }
                    if (found) break;
                }
            } catch (Throwable ignored) {}
        }

        // Format and append output
        for (int i = 0; i < maxItems; i++) {
            int id = itemIds[i];
            int qty = itemQtys[i];
            if (id > 0 && id != 65535) {
                String name = itemNames[i];
                if (name == null) {
                    name = resolveItemName(client, id);
                }
                if (name != null && !name.isEmpty() && !name.equalsIgnoreCase("null")) {
                    data.append(prefix).append("[").append(i).append("]: ").append(name).append(",").append(qty).append("\n");
                } else {
                    data.append(prefix).append("[").append(i).append("]: ").append(id).append(",").append(qty).append("\n");
                }
            } else {
                data.append(prefix).append("[").append(i).append("]: 0,0\n");
            }
        }
    }

    private static String resolveItemName(Object client, int id) {
        if (id <= 0) return null;
        String cached = ITEM_NAME_CACHE.get(id);
        if (cached != null) return cached;

        // 1. Try RuneLite ItemManager
        if (runeLiteItemManager != null) {
            for (String mName : new String[]{"getItemComposition", "getItemDefinition"}) {
                try {
                    Method m = runeLiteItemManager.getClass().getMethod(mName, int.class);
                    m.setAccessible(true);
                    Object comp = m.invoke(runeLiteItemManager, id);
                    if (comp != null) {
                        String name = extractNameFromItemComposition(client, comp, id);
                        if (name != null) {
                            ITEM_NAME_CACHE.put(id, name);
                            return name;
                        }
                    }
                } catch (Throwable ignored) {}
            }
        }

        // 2. Try RuneLite Client / RSClient instance
        Object targetClient = client != null ? client : runeLiteClient;
        if (targetClient != null) {
            for (String mName : new String[]{"getItemComposition", "getItemDefinition", "getRSItemDefinition", "createItemComposition"}) {
                try {
                    for (Method m : targetClient.getClass().getMethods()) {
                        if (m.getName().equalsIgnoreCase(mName) && m.getParameterCount() == 1
                                && (m.getParameterTypes()[0] == int.class || m.getParameterTypes()[0] == Integer.class)) {
                            m.setAccessible(true);
                            Object comp = m.invoke(targetClient, id);
                            if (comp != null) {
                                String name = extractNameFromItemComposition(client, comp, id);
                                if (name != null) {
                                    ITEM_NAME_CACHE.put(id, name);
                                    return name;
                                }
                            }
                            break;
                        }
                    }
                } catch (Throwable ignored) {}
            }
        }

        // 3. Built-in item lookup dictionary
        String builtin = getBuiltinItemName(id);
        if (builtin != null) {
            ITEM_NAME_CACHE.put(id, builtin);
            return builtin;
        }

        return null;
    }

    private static String extractNameFromItemComposition(Object client, Object comp, int originalId) {
        if (comp == null) return null;

        // Try direct name methods
        for (String nmMethod : new String[]{"getName", "getMembersName", "getRawName"}) {
            try {
                Method getName = comp.getClass().getMethod(nmMethod);
                getName.setAccessible(true);
                Object n = getName.invoke(comp);
                if (n instanceof String) {
                    String s = ((String) n).replaceAll("<[^>]*>", "").replace('\u00A0', ' ').trim();
                    if (!s.isEmpty() && !s.equalsIgnoreCase("null") && !s.equalsIgnoreCase("none")) {
                        return s;
                    }
                }
            } catch (Throwable ignored) {}
        }

        // Try any zero-parameter String-returning method with 'name'
        for (Method m : comp.getClass().getMethods()) {
            if (m.getParameterCount() == 0 && m.getReturnType() == String.class) {
                String mName = m.getName().toLowerCase();
                if (mName.contains("name")) {
                    try {
                        m.setAccessible(true);
                        Object n = m.invoke(comp);
                        if (n instanceof String) {
                            String s = ((String) n).replaceAll("<[^>]*>", "").replace('\u00A0', ' ').trim();
                            if (!s.isEmpty() && !s.equalsIgnoreCase("null") && !s.equalsIgnoreCase("none")) {
                                return s;
                            }
                        }
                    } catch (Throwable ignored) {}
                }
            }
        }

        // Check if this is a noted item and resolve linked unnoted item
        for (String mName : new String[]{"getLinkedNoteId", "getNote", "getUnnotedId"}) {
            try {
                Method m = comp.getClass().getMethod(mName);
                m.setAccessible(true);
                Object res = m.invoke(comp);
                if (res instanceof Integer) {
                    int linkedId = (Integer) res;
                    if (linkedId > 0 && linkedId != originalId) {
                        String unnotedName = resolveItemName(client, linkedId);
                        if (unnotedName != null && !unnotedName.isEmpty()) {
                            return unnotedName;
                        }
                    }
                }
            } catch (Throwable ignored) {}
        }

        // Check if placeholder and resolve base item
        for (String mName : new String[]{"getPlaceholderId", "getPlaceholderTemplateId"}) {
            try {
                Method m = comp.getClass().getMethod(mName);
                m.setAccessible(true);
                Object res = m.invoke(comp);
                if (res instanceof Integer) {
                    int placeholderId = (Integer) res;
                    if (placeholderId > 0 && placeholderId != originalId) {
                        String baseName = resolveItemName(client, placeholderId);
                        if (baseName != null && !baseName.isEmpty()) {
                            return baseName;
                        }
                    }
                }
            } catch (Throwable ignored) {}
        }

        // Search all fields in inheritance hierarchy
        Class<?> curr = comp.getClass();
        while (curr != null && curr != Object.class) {
            for (Field f : curr.getDeclaredFields()) {
                if (f.getType() == String.class) {
                    try {
                        f.setAccessible(true);
                        Object res = f.get(comp);
                        if (res instanceof String) {
                            String s = ((String) res).replaceAll("<[^>]*>", "").replace('\u00A0', ' ').trim();
                            if (!s.isEmpty() && !s.equalsIgnoreCase("null") && !s.equalsIgnoreCase("none") && s.length() > 1) {
                                return s;
                            }
                        }
                    } catch (Throwable ignored) {}
                }
            }
            curr = curr.getSuperclass();
        }
        return null;
    }

    private static String getBuiltinItemName(int id) {
        switch (id) {
            case 995: return "Coins";
            case 1351: return "Bronze axe";
            case 1349: return "Iron axe";
            case 1353: return "Steel axe";
            case 1355: return "Mithril axe";
            case 1357: return "Adamant axe";
            case 1359: return "Rune axe";
            case 6739: return "Dragon axe";
            case 1265: return "Bronze pickaxe";
            case 1267: return "Iron pickaxe";
            case 1269: return "Steel pickaxe";
            case 1273: return "Mithril pickaxe";
            case 1271: return "Adamant pickaxe";
            case 1275: return "Rune pickaxe";
            case 11920: return "Dragon pickaxe";
            case 303: return "Small fishing net";
            case 307: return "Fishing rod";
            case 309: return "Fly fishing rod";
            case 311: return "Harpoon";
            case 301: return "Lobster pot";
            case 313: return "Fishing bait";
            case 314: return "Feather";
            case 590: return "Tinderbox";
            case 1755: return "Chisel";
            case 2347: return "Hammer";
            case 1733: return "Needle";
            case 1734: return "Thread";
            case 946: return "Knife";
            case 1925: return "Bucket";
            case 1929: return "Bucket of water";
            case 1935: return "Jug";
            case 1937: return "Jug of water";
            case 227: return "Vial of water";
            case 229: return "Vial";
            case 554: return "Fire rune";
            case 555: return "Water rune";
            case 556: return "Air rune";
            case 557: return "Earth rune";
            case 558: return "Mind rune";
            case 559: return "Body rune";
            case 560: return "Death rune";
            case 561: return "Nature rune";
            case 562: return "Chaos rune";
            case 563: return "Law rune";
            case 564: return "Cosmic rune";
            case 565: return "Blood rune";
            case 566: return "Soul rune";
            case 21880: return "Wrath rune";
            case 9075: return "Astral rune";
            case 315: return "Shrimps";
            case 325: return "Salmon";
            case 329: return "Salmon";
            case 333: return "Trout";
            case 377: return "Lobster";
            case 379: return "Lobster";
            case 383: return "Raw shark";
            case 385: return "Shark";
            case 386: return "Shark (noted)";
            case 395: case 397: return "Sea turtle";
            case 389: case 391: return "Manta ray";
            case 3144: return "Cooked karambwan";
            case 13441: return "Anglerfish";
            case 11936: return "Dark crab";
            case 7946: return "Monkfish";
            case 2434: case 139: case 141: case 143: return "Prayer potion";
            case 6685: case 6687: case 6689: case 6691: return "Saradomin brew";
            case 3024: case 3026: case 3028: case 3030: return "Super restore";
            case 12625: case 12627: case 12629: case 12631: return "Stamina potion";
            case 2440: case 157: case 159: case 161: return "Super strength";
            case 2436: case 145: case 147: case 149: return "Super attack";
            case 2442: case 163: case 165: case 167: return "Super defence";
            case 2444: case 169: case 171: case 173: return "Ranging potion";
            case 3040: case 3042: case 3044: case 3046: return "Magic potion";
            case 12695: case 12697: case 12699: case 12701: return "Super combat potion";
            case 23685: case 23688: case 23691: case 23694: return "Divine super combat potion";
            case 4151: return "Abyssal whip";
            case 12006: return "Abyssal tentacle";
            case 1305: return "Dragon longsword";
            case 4587: return "Dragon scimitar";
            case 1377: return "Dragon battleaxe";
            case 1215: case 5698: return "Dragon dagger";
            case 11802: return "Armadyl godsword";
            case 11804: return "Bandos godsword";
            case 11806: return "Saradomin godsword";
            case 11808: return "Zamorak godsword";
            case 11832: return "Bandos chestplate";
            case 11834: return "Bandos tassets";
            case 11836: return "Bandos boots";
            case 11826: return "Armadyl helmet";
            case 11828: return "Armadyl chestplate";
            case 11830: return "Armadyl chainskirt";
            case 11840: return "Dragon boots";
            case 21736: return "Primordial boots";
            case 21742: return "Pegasian boots";
            case 21748: return "Eternal boots";
            case 6585: return "Amulet of fury";
            case 19553: return "Amulet of torture";
            case 19547: return "Necklace of anguish";
            case 19544: return "Tormented bracelet";
            case 19550: return "Ring of suffering";
            case 1704: case 1712: case 11978: return "Amulet of glory";
            case 1725: return "Amulet of strength";
            case 1727: return "Amulet of magic";
            case 1731: return "Amulet of power";
            case 6737: case 11773: return "Berserker ring";
            case 6731: case 11770: return "Seers ring";
            case 6733: case 11771: return "Archers ring";
            case 6735: case 11772: return "Warrior ring";
            case 22975: return "Brimstone ring";
            case 7462: return "Barrows gloves";
            case 7461: return "Dragon gloves";
            case 7460: return "Rune gloves";
            case 10551: return "Fighter torso";
            case 1127: return "Rune platebody";
            case 1079: return "Rune platelegs";
            case 1093: return "Rune plateskirt";
            case 1163: return "Rune full helm";
            case 1201: return "Rune kiteshield";
            case 3140: return "Dragon chainbody";
            case 4087: return "Dragon platelegs";
            case 4585: return "Dragon plateskirt";
            case 1149: return "Dragon med helm";
            case 11838: case 12954: return "Dragon defender";
            case 8850: return "Rune defender";
            case 12926: case 12924: return "Toxic blowpipe";
            case 12934: return "Zulrah's scales";
            case 11283: return "Dragonfire shield";
            case 10499: return "Ava's accumulator";
            case 22109: return "Ava's assembler";
            case 25865: case 25867: return "Bow of faerdhinen";
            case 20997: return "Twisted bow";
            case 22325: return "Scythe of vitur";
            case 27275: return "Tumeken's shadow";
            case 4716: return "Dharok's helm";
            case 4718: return "Dharok's greataxe";
            case 4720: return "Dharok's platebody";
            case 4722: return "Dharok's platelegs";
            case 4708: return "Ahrim's hood";
            case 4710: return "Ahrim's staff";
            case 4712: return "Ahrim's robetop";
            case 4714: return "Ahrim's robeskirt";
            case 4724: return "Guthan's helm";
            case 4726: return "Guthan's warspear";
            case 4728: return "Guthan's platebody";
            case 4730: return "Guthan's chainskirt";
            case 4732: return "Karil's coif";
            case 4734: return "Karil's crossbow";
            case 4736: return "Karil's leathertop";
            case 4738: return "Karil's leatherskirt";
            case 4745: return "Torag's helm";
            case 4747: return "Torag's hammers";
            case 4749: return "Torag's platebody";
            case 4751: return "Torag's platelegs";
            case 4753: return "Verac's helm";
            case 4755: return "Verac's flail";
            case 4757: return "Verac's brassard";
            case 4759: return "Verac's plateskirt";
            case 11864: case 11865: return "Slayer helmet";
            case 6570: return "Fire cape";
            case 21295: return "Infernal cape";
            case 13280: return "Max cape";
            case 436: return "Copper ore";
            case 438: return "Tin ore";
            case 440: return "Iron ore";
            case 442: return "Silver ore";
            case 444: return "Gold ore";
            case 447: return "Mithril ore";
            case 449: return "Adamantite ore";
            case 451: return "Runite ore";
            case 453: return "Coal";
            case 2349: return "Bronze bar";
            case 2351: return "Iron bar";
            case 2353: return "Steel bar";
            case 2355: return "Silver bar";
            case 2357: return "Gold bar";
            case 2359: return "Mithril bar";
            case 2361: return "Adamantite bar";
            case 2363: return "Runite bar";
            case 1511: return "Logs";
            case 1521: return "Oak logs";
            case 1519: return "Willow logs";
            case 6333: return "Teak logs";
            case 1517: return "Maple logs";
            case 6332: return "Mahogany logs";
            case 1515: return "Yew logs";
            case 1513: return "Magic logs";
            case 19669: return "Redwood logs";
            case 526: return "Bones";
            case 532: return "Big bones";
            case 536: return "Dragon bones";
            case 22124: return "Superior dragon bones";
            case 199: return "Grimy guam leaf";
            case 201: return "Grimy marrentill";
            case 203: return "Grimy tarromin";
            case 205: return "Grimy harralander";
            case 207: return "Grimy ranarr weed";
            case 209: return "Grimy irit leaf";
            case 211: return "Grimy avantoe";
            case 213: return "Grimy kwuarm";
            case 215: return "Grimy cadantine";
            case 217: return "Grimy dwarf weed";
            case 219: return "Grimy torstol";
            case 3049: return "Grimy toadflax";
            case 3051: return "Grimy snapdragon";
            case 8007: return "Varrock teleport";
            case 8008: return "Lumbridge teleport";
            case 8009: return "Falador teleport";
            case 8010: return "Camelot teleport";
            case 8011: return "Ardougne teleport";
            case 8013: return "Teleport to house";
            case 2412: return "Saradomin cape";
            case 2413: return "Guthix cape";
            case 2414: return "Zamorak cape";
            case 21791: return "Imbued saradomin cape";
            case 21793: return "Imbued guthix cape";
            case 21795: return "Imbued zamorak cape";
            case 11850: return "Graceful hood";
            case 11852: return "Graceful cape";
            case 11854: return "Graceful top";
            case 11856: return "Graceful legs";
            case 11858: return "Graceful gloves";
            case 11860: return "Graceful boots";
            case 8839: return "Void knight top";
            case 8840: return "Void knight robe";
            case 8842: return "Void knight gloves";
            case 11663: return "Void mage helm";
            case 11664: return "Void ranger helm";
            case 11665: return "Void melee helm";
            case 13072: return "Elite void top";
            case 13073: return "Elite void robe";
            case 12791: return "Rune pouch";
            case 27281: return "Divine rune pouch";
            case 12940: return "Toxic staff of the dead";
            case 12904: return "Toxic staff (uncharged)";
            case 12929: return "Serpentine helm (uncharged)";
            case 12931: return "Serpentine helm";
            case 13239: return "Primordial boots";
            case 13237: return "Pegasian boots";
            case 13235: return "Eternal boots";
            case 22978: return "Brimstone ring";
            case 20653: return "Amulet of the damned";
            case 20655: return "Amulet of the damned (full)";
            case 2452: return "Antifire potion(4)";
            case 2454: return "Antifire potion(3)";
            case 2456: return "Antifire potion(2)";
            case 2458: return "Antifire potion(1)";
            case 11951: return "Extended antifire(4)";
            case 11953: return "Extended antifire(3)";
            case 11955: return "Extended antifire(2)";
            case 11957: return "Extended antifire(1)";
            case 22209: return "Extended super antifire(4)";
            case 22212: return "Extended super antifire(3)";
            case 22215: return "Extended super antifire(2)";
            case 22218: return "Extended super antifire(1)";
            case 2446: return "Antipoison(4)";
            case 175: return "Antipoison(3)";
            case 177: return "Antipoison(2)";
            case 179: return "Antipoison(1)";
            case 2448: return "Superantipoison(4)";
            case 181: return "Superantipoison(3)";
            case 183: return "Superantipoison(2)";
            case 185: return "Superantipoison(1)";
            case 5952: return "Antidote+(4)";
            case 5954: return "Antidote+(3)";
            case 5956: return "Antidote+(2)";
            case 5958: return "Antidote+(1)";
            case 5943: return "Antidote++(4)";
            case 5945: return "Antidote++(3)";
            case 5947: return "Antidote++(2)";
            case 5949: return "Antidote++(1)";
            case 12913: return "Anti-venom(4)";
            case 12915: return "Anti-venom(3)";
            case 12917: return "Anti-venom(2)";
            case 12919: return "Anti-venom(1)";
            case 12905: return "Anti-venom+(4)";
            case 12907: return "Anti-venom+(3)";
            case 12909: return "Anti-venom+(2)";
            case 12911: return "Anti-venom+(1)";
            default: return null;
        }
    }

    private static void appendRuneLitePlayer(Object client, Object player, int index, int playerX, int playerY, Object localPlayer, StringBuilder data) {
        try {
            int id = index;
            int dist = 0;
            int combatLevel = 0;
            String name = "Unknown";

            try {
                Method getId = player.getClass().getMethod("getId");
                getId.setAccessible(true);
                id = (Integer) getId.invoke(player);
            } catch (Throwable ignored) {}

            try {
                Method getName = player.getClass().getMethod("getName");
                getName.setAccessible(true);
                Object res = getName.invoke(player);
                if (res instanceof String) {
                    name = ((String) res).replace('\u00A0', ' ').replaceAll("<[^>]*>", "").trim();
                }
            } catch (Throwable ignored) {}

            if (name.isEmpty() || name.equalsIgnoreCase("null")) {
                name = "Player " + (index + 1);
            }

            if (player != null && localPlayer != null && player.equals(localPlayer)) {
                name = name + " (You)";
            }

            try {
                Method getCombatLevel = player.getClass().getMethod("getCombatLevel");
                getCombatLevel.setAccessible(true);
                combatLevel = (Integer) getCombatLevel.invoke(player);
            } catch (Throwable ignored) {}

            try {
                Method getWorldLocation = player.getClass().getMethod("getWorldLocation");
                getWorldLocation.setAccessible(true);
                Object wp = getWorldLocation.invoke(player);
                if (wp != null && playerX > 0 && playerY > 0) {
                    Method getX = wp.getClass().getMethod("getX");
                    Method getY = wp.getClass().getMethod("getY");
                    getX.setAccessible(true);
                    getY.setAccessible(true);
                    int px = (Integer) getX.invoke(wp);
                    int py = (Integer) getY.invoke(wp);
                    dist = Math.max(Math.abs(px - playerX), Math.abs(py - playerY));
                } else {
                    Method getLocalLoc = player.getClass().getMethod("getLocalLocation");
                    getLocalLoc.setAccessible(true);
                    Object loc = getLocalLoc.invoke(player);
                    if (loc != null && localPlayer != null) {
                        Method getLocalPlayerLoc = localPlayer.getClass().getMethod("getLocalLocation");
                        getLocalPlayerLoc.setAccessible(true);
                        Object myLoc = getLocalPlayerLoc.invoke(localPlayer);
                        if (myLoc != null) {
                            Method getX = loc.getClass().getMethod("getX");
                            Method getY = loc.getClass().getMethod("getY");
                            getX.setAccessible(true);
                            getY.setAccessible(true);
                            int lx = (Integer) getX.invoke(loc);
                            int ly = (Integer) getY.invoke(loc);
                            int myX = (Integer) getX.invoke(myLoc);
                            int myY = (Integer) getY.invoke(myLoc);
                            dist = Math.max(Math.abs((lx >> 7) - (myX >> 7)), Math.abs((ly >> 7) - (myY >> 7)));
                        }
                    }
                }
            } catch (Throwable ignored) {}

            data.append("NEARBY_PLAYER[").append(index).append("]: ").append(id).append(",").append(name).append(",").append(dist).append(",").append(combatLevel).append("\n");
        } catch (Throwable ignored) {}
    }

    private static String extractPlayerName(Object player) {
        if (player == null) return "Unknown";
        try {
            Method getName = player.getClass().getMethod("getName");
            getName.setAccessible(true);
            Object res = getName.invoke(player);
            if (res instanceof String) {
                String s = ((String) res).replace('\u00A0', ' ').replaceAll("<[^>]*>", "").trim();
                if (!s.isEmpty() && !s.equalsIgnoreCase("null")) return s;
            }
        } catch (Throwable ignored) {}
        for (Field f : player.getClass().getDeclaredFields()) {
            if (f.getType() == String.class) {
                try {
                    f.setAccessible(true);
                    Object res = f.get(player);
                    if (res instanceof String) {
                        String s = ((String) res).replace('\u00A0', ' ').replaceAll("<[^>]*>", "").trim();
                        if (!s.isEmpty() && !s.equalsIgnoreCase("null") && s.length() > 1) return s;
                    }
                } catch (Throwable ignored) {}
            }
        }
        return "Player";
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
                } else {
                    Method getLocalLoc = npc.getClass().getMethod("getLocalLocation");
                    getLocalLoc.setAccessible(true);
                    Object loc = getLocalLoc.invoke(npc);
                    if (loc != null && client != null) {
                        try {
                            Method getLocalPlayer = client.getClass().getMethod("getLocalPlayer");
                            getLocalPlayer.setAccessible(true);
                            Object lp = getLocalPlayer.invoke(client);
                            if (lp != null) {
                                Method getLocalPlayerLoc = lp.getClass().getMethod("getLocalLocation");
                                getLocalPlayerLoc.setAccessible(true);
                                Object myLoc = getLocalPlayerLoc.invoke(lp);
                                if (myLoc != null) {
                                    Method getX = loc.getClass().getMethod("getX");
                                    Method getY = loc.getClass().getMethod("getY");
                                    getX.setAccessible(true);
                                    getY.setAccessible(true);
                                    int nx = (Integer) getX.invoke(loc);
                                    int ny = (Integer) getY.invoke(loc);
                                    int myX = (Integer) getX.invoke(myLoc);
                                    int myY = (Integer) getY.invoke(myLoc);
                                    dist = Math.max(Math.abs((nx >> 7) - (myX >> 7)), Math.abs((ny >> 7) - (myY >> 7)));
                                }
                            }
                        } catch (Throwable ignored) {}
                    }
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

            String category = categorizeNpc(name);
            data.append("NPC[").append(index).append("]: ").append(id).append(",").append(name).append(",").append(dist).append(",").append(health).append(",").append(category).append("\n");
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

    private static String categorizeNpc(String name) {
        if (name == null) return "NPC";
        String n = name.toLowerCase();
        if (n.contains("fishing spot") || n.contains("rod fishing spot") || n.contains("cage fishing spot") ||
            n.contains("net fishing spot") || n.contains("harpoon fishing spot") || n.contains("lava fishing spot") ||
            n.contains("cave eel fishing spot") || n.equals("fishing spot")) {
            return "Fishing Spot";
        }
        if (n.equals("turael") || n.equals("spria") || n.equals("krystilia") || n.equals("mazchna") ||
            n.equals("vannaka") || n.equals("chaeldar") || n.contains("konar") || n.equals("nieve") ||
            n.equals("steve") || n.equals("duradel") || n.equals("kuradal") || n.contains("slayer master")) {
            return "Slayer Master";
        }
        if (n.contains("banker") || n.contains("bank") || n.equals("emerald benedict") || n.equals("ghost banker") || n.contains("gnome banker")) {
            return "Banker";
        }
        if (n.contains("shopkeeper") || n.contains("assistant") || n.contains("merchant") || n.contains("trader") ||
            n.contains("general store") || n.equals("apothecary") || n.equals("horvik") || n.equals("zaff") ||
            n.equals("thessalia") || n.equals("lowe") || n.equals("brian") || n.equals("cassie") || n.equals("wyd") ||
            n.equals("grum") || n.equals("garrad") || n.equals("aubury") || n.equals("herquin")) {
            return "Shopkeeper";
        }
        if (n.contains("grand exchange clerk") || n.contains("exchange clerk") || n.contains("ge clerk")) {
            return "Grand Exchange";
        }
        return "NPC";
    }

    private static boolean isFishingSpotNpc(Object client, Object npc) {
        if (npc == null) return false;
        try {
            int id = -1;
            try {
                Method getId = npc.getClass().getMethod("getId");
                getId.setAccessible(true);
                id = (Integer) getId.invoke(npc);
            } catch (Throwable ignored) {}
            String name = extractNpcName(client, npc, id);
            if (name != null) {
                String n = name.toLowerCase();
                if (n.contains("fishing spot") || n.contains("rod fishing spot") || n.contains("cage fishing spot") ||
                    n.contains("net fishing spot") || n.contains("harpoon fishing spot") || n.contains("lava fishing spot") ||
                    n.contains("cave eel fishing spot") || n.equals("fishing spot")) {
                    return true;
                }
            }
            if ((id >= 1510 && id <= 1534) || (id >= 1542 && id <= 1544) || id == 4316 || (id >= 4712 && id <= 4714) ||
                id == 6825 || id == 6488 || id == 7676 || (id >= 7730 && id <= 7733) || (id >= 8523 && id <= 8527)) {
                return true;
            }
        } catch (Throwable ignored) {}
        return false;
    }

    private static void appendRuneLiteFishingSpot(Object client, Object npc, int index, int playerX, int playerY, StringBuilder data) {
        if (npc == null) return;
        try {
            int id = -1;
            try {
                Method getId = npc.getClass().getMethod("getId");
                getId.setAccessible(true);
                id = (Integer) getId.invoke(npc);
            } catch (Throwable ignored) {}

            String name = extractNpcName(client, npc, id);
            String spotType = resolveFishingSpotType(name, npc, id);

            int dist = 0;
            int nx = playerX;
            int ny = playerY;
            try {
                Method getWorldLocation = npc.getClass().getMethod("getWorldLocation");
                getWorldLocation.setAccessible(true);
                Object wp = getWorldLocation.invoke(npc);
                if (wp != null && playerX > 0 && playerY > 0) {
                    Method getX = wp.getClass().getMethod("getX");
                    Method getY = wp.getClass().getMethod("getY");
                    getX.setAccessible(true);
                    getY.setAccessible(true);
                    nx = (Integer) getX.invoke(wp);
                    ny = (Integer) getY.invoke(wp);
                    dist = Math.max(Math.abs(nx - playerX), Math.abs(ny - playerY));
                }
            } catch (Throwable ignored) {}

            data.append("FISHING_SPOT[").append(index).append("]: ")
                .append(id).append(",")
                .append(name).append(",")
                .append(spotType).append(",")
                .append(dist).append(",")
                .append(nx).append(",")
                .append(ny).append("\n");
        } catch (Throwable ignored) {}
    }

    private static String resolveFishingSpotType(String name, Object npc, int id) {
        String n = name != null ? name.toLowerCase() : "";
        if (npc != null) {
            try {
                Method getActions = null;
                for (String mName : new String[]{"getActions", "actions"}) {
                    try {
                        getActions = npc.getClass().getMethod(mName);
                        break;
                    } catch (Throwable ignored) {}
                }
                if (getActions != null) {
                    getActions.setAccessible(true);
                    Object actionsObj = getActions.invoke(npc);
                    if (actionsObj instanceof String[]) {
                        String[] actions = (String[]) actionsObj;
                        boolean hasCage = false, hasHarpoon = false, hasNet = false, hasBait = false, hasLure = false, hasSmallNet = false;
                        for (String a : actions) {
                            if (a != null) {
                                String act = a.toLowerCase();
                                if (act.contains("cage")) hasCage = true;
                                if (act.contains("harpoon")) hasHarpoon = true;
                                if (act.contains("small net") || act.contains("small-net")) hasSmallNet = true;
                                else if (act.contains("net")) hasNet = true;
                                if (act.contains("bait")) hasBait = true;
                                if (act.contains("lure")) hasLure = true;
                            }
                        }
                        if (hasCage && hasHarpoon) return "Lobster / Swordfish (Cage / Harpoon)";
                        if (hasLure && hasBait) return "Trout / Salmon / Pike (Lure / Bait)";
                        if (hasSmallNet) return "Monkfish / Minnows (Small Net)";
                        if (hasNet && hasBait) return "Shrimp / Anchovies (Net / Bait)";
                        if (hasHarpoon) return "Shark / Tuna (Harpoon)";
                        if (hasLure) return "Trout / Salmon (Fly Fishing)";
                        if (hasCage) return "Lobster (Lobster Pot)";
                        if (hasNet) return "Shrimp / Anchovies (Small Net)";
                        if (hasBait) return "Sardine / Herring / Karambwan (Bait)";
                    }
                }
            } catch (Throwable ignored) {}
        }

        if (id == 1518 || id == 1526 || id == 1527 || n.contains("rod")) return "Trout / Salmon / Pike (Fly Fishing / Bait)";
        if (id == 1510 || id == 1519 || id == 1522 || n.contains("cage")) return "Lobster / Swordfish (Cage / Harpoon)";
        if (id == 1511 || id == 1520 || id == 1534 || n.contains("harpoon")) return "Shark / Tuna (Harpoon)";
        if (id == 4316) return "Monkfish (Small Net)";
        if (id == 4712 || id == 4713 || id == 4714) return "Karambwan (Karambwan Vessel)";
        if (id == 6825) return "Anglerfish (Sandworm / Bait)";
        if (id == 6488) return "Sacred Eel (Bait)";
        if (id == 7676) return "Infernal Eel (Oily Rod / Bait)";
        if (id >= 7730 && id <= 7733) return "Minnows (Small Net)";
        if (id == 1542 || id == 1544) return "Barbarian Fishing (Heavy Rod)";
        if (id >= 8523 && id <= 8527) return "Aerial Fishing (Bird)";
        if (n.contains("net")) return "Shrimp / Anchovies (Small Net)";
        if (n.contains("lava")) return "Lava Eel (Oily Rod)";
        if (n.contains("cave eel")) return "Cave Eel (Bait)";
        return "Fish (Net / Bait / Harpoon)";
    }

    // -------------------------------------------------------------
    // Special Attack Detection
    // -------------------------------------------------------------
    private static void processRuneLiteSpecialAttack(Object client, StringBuilder data) {
        if (client == null) return;
        try {
            int specEnergy = getVarpValue(client, 300); // 0 - 1000 (1000 = 100%)
            int specActive = getVarpValue(client, 301); // 1 = active / enabled
            if (specEnergy >= 0) {
                int percent = specEnergy / 10;
                data.append("SPECIAL_ATTACK_PERCENT: ").append(percent).append("%\n");
                data.append("SPECIAL_ATTACK_ACTIVE: ").append(specActive == 1 ? "Active" : "Inactive").append("\n");
            }
        } catch (Throwable ignored) {}
    }

    // -------------------------------------------------------------
    // Slayer Task & Master Detection
    // -------------------------------------------------------------
    private static void processRuneLiteSlayer(Object client, StringBuilder data) {
        if (client == null) return;
        try {
            int taskCount = getVarbitValue(client, 394);
            int creatureId = getVarbitValue(client, 395);
            if (creatureId < 0) {
                int varp261 = getVarpValue(client, 261);
                if (varp261 > 0) creatureId = varp261;
            }
            int slayerPoints = getVarbitValue(client, 4068);
            int slayerStreak = getVarbitValue(client, 4067);

            String taskMonster = resolveSlayerMonster(creatureId);
            if (taskCount > 0 && !taskMonster.equals("None")) {
                data.append("SLAYER_COUNT: ").append(taskCount).append("\n");
                data.append("SLAYER_TASK: ").append(taskMonster).append("\n");
            } else if (taskCount == 0) {
                data.append("SLAYER_COUNT: 0\n");
                data.append("SLAYER_TASK: None\n");
            }

            if (slayerPoints >= 0) {
                data.append("SLAYER_POINTS: ").append(slayerPoints).append("\n");
            }
            if (slayerStreak >= 0) {
                data.append("SLAYER_STREAK: ").append(slayerStreak).append("\n");
            }
        } catch (Throwable ignored) {}
    }

    private static String resolveSlayerMonster(int id) {
        switch (id) {
            case 1: return "Monkeys";
            case 2: return "Goblins";
            case 3: return "Rats";
            case 4: return "Spiders";
            case 5: return "Birds";
            case 6: return "Cows";
            case 7: return "Scorpions";
            case 8: return "Bats";
            case 9: return "Wolves";
            case 10: return "Zombies";
            case 11: return "Skeletons";
            case 12: return "Ghosts";
            case 13: return "Bears";
            case 14: return "Hill Giants";
            case 15: return "Ice Giants";
            case 16: return "Moss Giants";
            case 17: return "Fire Giants";
            case 18: return "Cave Bugs";
            case 19: return "Cave Crawlers";
            case 20: return "Crawling Hands";
            case 21: return "Cave Slimes";
            case 22: return "Banshees";
            case 23: return "Infernal Mages";
            case 24: return "Bloodvelds";
            case 25: return "Aberrant Spectres";
            case 26: return "Gargoyles";
            case 27: return "Nechryael";
            case 28: return "Abyssal Demons";
            case 29: return "Basilisks";
            case 30: return "Cockatrice";
            case 31: return "Kurask";
            case 32: return "Dust Devils";
            case 33: return "Spiritual Creatures";
            case 34: return "Turoth";
            case 35: return "Dark Beasts";
            case 36: return "Cave Krakens";
            case 37: return "Smoke Devils";
            case 38: return "Wyrms";
            case 39: return "Drakes";
            case 40: return "Hydras";
            case 41: return "Greater Demons";
            case 42: return "Lesser Demons";
            case 43: return "Black Demons";
            case 44: return "Hellhounds";
            case 45: return "Blue Dragons";
            case 46: return "Red Dragons";
            case 47: return "Black Dragons";
            case 48: return "Iron Dragons";
            case 49: return "Steel Dragons";
            case 50: return "Mithril Dragons";
            case 51: return "Adamant Dragons";
            case 52: return "Rune Dragons";
            case 53: return "Aviansies";
            case 54: return "Dagannoth";
            case 55: return "Kalphite";
            case 56: return "Ankou";
            case 57: return "TzHaar";
            case 58: return "Suqahs";
            case 59: return "Mutated Zygomites";
            case 60: return "Fossil Island Wyverns";
            case 61: return "Basilisk Knights";
            case 62: return "Lizardmen";
            case 63: return "Vampyres";
            case 64: return "Brine Rats";
            case 65: return "Cave Horrors";
            case 66: return "Elves";
            case 67: return "Dwarves";
            case 68: return "Minotaurs";
            case 69: return "Fever Spiders";
            case 70: return "Harpie Bug Swarms";
            case 71: return "Sea Snakes";
            case 72: return "Mogres";
            case 73: return "Desert Lizards";
            case 74: return "Jungle Horrors";
            case 75: return "Zygomites";
            case 76: return "Icefiends";
            case 77: return "Minions of Scabaras";
            case 78: return "Terror Dogs";
            case 79: return "Molanisks";
            case 80: return "Waterfiends";
            case 81: return "Warped Terrorbirds";
            case 82: return "Warped Tortoises";
            case 83: return "Spiritual Rangers";
            case 84: return "Spiritual Warriors";
            case 85: return "Spiritual Mages";
            case 86: return "Skeletal Wyverns";
            default: return id > 0 ? ("Task #" + id) : "None";
        }
    }

    // -------------------------------------------------------------
    // Chat & Dialogue Scraper
    // -------------------------------------------------------------
    private static Object getWidget(Object client, int groupId, int childId) {
        if (client == null) return null;
        try {
            Method m = client.getClass().getMethod("getWidget", int.class, int.class);
            m.setAccessible(true);
            Object w = m.invoke(client, groupId, childId);
            if (w != null) return w;
        } catch (Throwable ignored) {}
        try {
            Method m = client.getClass().getMethod("getWidget", int.class);
            m.setAccessible(true);
            int packed = (groupId << 16) | childId;
            return m.invoke(client, packed);
        } catch (Throwable ignored) {}
        return null;
    }

    private static String getWidgetText(Object widget) {
        if (widget == null) return null;
        try {
            Method m = widget.getClass().getMethod("getText");
            m.setAccessible(true);
            Object res = m.invoke(widget);
            if (res instanceof String) {
                String s = ((String) res).replaceAll("<[^>]*>", "").replace('\u00A0', ' ').trim();
                if (!s.isEmpty()) return s;
            }
        } catch (Throwable ignored) {}
        return null;
    }

    private static Object[] getWidgetChildren(Object widget) {
        if (widget == null) return null;
        String[] childMethods = {"getDynamicChildren", "getChildren", "getNestedChildren"};
        for (String mName : childMethods) {
            try {
                Method m = widget.getClass().getMethod(mName);
                m.setAccessible(true);
                Object res = m.invoke(widget);
                if (res instanceof Object[]) {
                    return (Object[]) res;
                }
            } catch (Throwable ignored) {}
        }
        return null;
    }

    private static void processRuneLiteDialog(Object client, StringBuilder data) {
        if (client == null) return;
        try {
            boolean active = false;
            String type = "None";
            String title = "";
            String text = "";
            List<String> options = new ArrayList<>();

            // 1. NPC Dialogue (Group 231)
            Object npcTextWidget = getWidget(client, 231, 6);
            if (npcTextWidget == null) npcTextWidget = getWidget(client, 231, 4);
            Object npcNameWidget = getWidget(client, 231, 4);
            if (npcNameWidget == null || npcNameWidget == npcTextWidget) npcNameWidget = getWidget(client, 231, 2);

            String npcText = getWidgetText(npcTextWidget);
            if (npcText != null && !npcText.isEmpty()) {
                active = true;
                type = "NPC";
                text = npcText;
                String name = getWidgetText(npcNameWidget);
                title = (name != null && !name.isEmpty()) ? name : "NPC";
            }

            // 2. Player Dialogue (Group 217)
            if (!active) {
                Object pTextWidget = getWidget(client, 217, 6);
                if (pTextWidget == null) pTextWidget = getWidget(client, 217, 4);
                Object pNameWidget = getWidget(client, 217, 2);
                if (pNameWidget == null) pNameWidget = getWidget(client, 217, 3);

                String pText = getWidgetText(pTextWidget);
                if (pText != null && !pText.isEmpty()) {
                    active = true;
                    type = "Player";
                    text = pText;
                    String name = getWidgetText(pNameWidget);
                    title = (name != null && !name.isEmpty()) ? name : "Player";
                }
            }

            // 3. Options Dialogue (Group 219)
            if (!active) {
                Object optWidget = getWidget(client, 219, 1);
                if (optWidget != null) {
                    Object[] children = getWidgetChildren(optWidget);
                    if (children != null && children.length > 0) {
                        for (int i = 0; i < children.length; i++) {
                            String opt = getWidgetText(children[i]);
                            if (opt != null && !opt.isEmpty()) {
                                if (i == 0 && (opt.toLowerCase().contains("select") || opt.toLowerCase().contains("option") || opt.toLowerCase().contains("choose"))) {
                                    title = opt;
                                } else {
                                    options.add(opt);
                                }
                            }
                        }
                        if (!options.isEmpty() || !title.isEmpty()) {
                            active = true;
                            type = "Options";
                            if (title.isEmpty()) title = "Select an Option";
                            text = String.join(" | ", options);
                        }
                    }
                }
            }

            // 4. Message Dialogue (Group 193 / Group 229 / Group 11 / Group 633)
            if (!active) {
                int[][] msgWidgets = {{193, 2}, {193, 1}, {229, 1}, {229, 2}, {11, 2}, {11, 1}, {633, 1}};
                for (int[] mw : msgWidgets) {
                    Object w = getWidget(client, mw[0], mw[1]);
                    String msgText = getWidgetText(w);
                    if (msgText != null && !msgText.isEmpty() && msgText.length() > 2) {
                        active = true;
                        type = "Message";
                        title = "Game Message";
                        text = msgText;
                        break;
                    }
                }
            }

            data.append("DIALOG_ACTIVE: ").append(active ? "True" : "False").append("\n");
            if (active) {
                data.append("DIALOG_TYPE: ").append(type).append("\n");
                data.append("DIALOG_TITLE: ").append(title).append("\n");
                data.append("DIALOG_TEXT: ").append(text.replace('\n', ' ')).append("\n");
                if (!options.isEmpty()) {
                    StringBuilder optSb = new StringBuilder();
                    for (int i = 0; i < options.size(); i++) {
                        if (i > 0) optSb.append("|");
                        optSb.append(options.get(i));
                    }
                    data.append("DIALOG_OPTIONS: ").append(optSb.toString()).append("\n");
                }
            }
        } catch (Throwable ignored) {}
    }

    // -------------------------------------------------------------
    // Bank and Shop Container Scraper
    // -------------------------------------------------------------
    private static void processRuneLiteBankAndShop(Object client, StringBuilder data) {
        if (client == null) return;
        try {
            // 1. Bank (Container 95)
            boolean bankOpen = false;
            int bankItemsCount = 0;
            Object bankWidget = getWidget(client, 12, 13);
            if (bankWidget == null) bankWidget = getWidget(client, 12, 1);
            if (bankWidget != null) bankOpen = true;

            int maxBankItems = 100;
            int[] bankIds = new int[maxBankItems];
            int[] bankQtys = new int[maxBankItems];
            String[] bankNames = new String[maxBankItems];
            for (int i = 0; i < maxBankItems; i++) bankIds[i] = -1;

            readContainerRaw(client, 95, bankIds, bankQtys, bankNames, maxBankItems);
            for (int i = 0; i < maxBankItems; i++) {
                if (bankIds[i] > 0 && bankIds[i] != 65535) {
                    bankOpen = true;
                    bankItemsCount++;
                    String name = bankNames[i] != null ? bankNames[i] : resolveItemName(client, bankIds[i]);
                    if (name == null || name.isEmpty()) name = "Item #" + bankIds[i];
                    data.append("BANK_ITEM[").append(i).append("]: ").append(bankIds[i]).append(",").append(name).append(",").append(bankQtys[i]).append("\n");
                }
            }
            data.append("BANK_OPEN: ").append(bankOpen ? "True" : "False").append("\n");
            data.append("BANK_TOTAL_ITEMS: ").append(bankItemsCount).append("\n");

            // 2. Shop / General Store (Container 511)
            boolean shopOpen = false;
            int shopItemsCount = 0;
            String shopName = "General Store";
            Object shopWidget = getWidget(client, 300, 1);
            if (shopWidget != null) {
                shopOpen = true;
                Object shopTitleWidget = getWidget(client, 300, 2);
                String title = getWidgetText(shopTitleWidget);
                if (title != null && !title.isEmpty()) shopName = title;
            }

            int maxShopItems = 50;
            int[] shopIds = new int[maxShopItems];
            int[] shopQtys = new int[maxShopItems];
            String[] shopNames = new String[maxShopItems];
            for (int i = 0; i < maxShopItems; i++) shopIds[i] = -1;

            readContainerRaw(client, 511, shopIds, shopQtys, shopNames, maxShopItems);
            for (int i = 0; i < maxShopItems; i++) {
                if (shopIds[i] > 0 && shopIds[i] != 65535) {
                    shopOpen = true;
                    shopItemsCount++;
                    String name = shopNames[i] != null ? shopNames[i] : resolveItemName(client, shopIds[i]);
                    if (name == null || name.isEmpty()) name = "Item #" + shopIds[i];
                    data.append("SHOP_ITEM[").append(i).append("]: ").append(shopIds[i]).append(",").append(name).append(",").append(shopQtys[i]).append("\n");
                }
            }
            data.append("SHOP_OPEN: ").append(shopOpen ? "True" : "False").append("\n");
            if (shopOpen) {
                data.append("SHOP_NAME: ").append(shopName).append("\n");
            }
            data.append("SHOP_TOTAL_ITEMS: ").append(shopItemsCount).append("\n");
        } catch (Throwable ignored) {}
    }

    private static void readContainerRaw(Object client, int containerId, int[] itemIds, int[] itemQtys, String[] itemNames, int maxItems) {
        try {
            Object container = null;
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
                                String eName = ((Enum<?>) enumConst).name();
                                boolean match = false;
                                if (containerId == 93 && (eName.equalsIgnoreCase("INVENTORY") || eName.contains("INV"))) match = true;
                                if (containerId == 94 && (eName.equalsIgnoreCase("EQUIPMENT") || eName.contains("EQUIP"))) match = true;
                                if (containerId == 95 && (eName.equalsIgnoreCase("BANK") || eName.contains("BANK"))) match = true;
                                if (containerId == 511 && (eName.equalsIgnoreCase("SHOP") || eName.contains("SHOP"))) match = true;
                                if (!match) {
                                    try {
                                        Method getId = pType.getMethod("getId");
                                        getId.setAccessible(true);
                                        int id = (Integer) getId.invoke(enumConst);
                                        if (id == containerId) match = true;
                                    } catch (Throwable ignored) {}
                                }
                                if (match) {
                                    m.setAccessible(true);
                                    container = m.invoke(client, enumConst);
                                    if (container != null) break;
                                }
                            } catch (Throwable ignored) {}
                        }
                        if (container != null) break;
                    }
                }
            }

            if (container != null) {
                Object itemsObj = null;
                try {
                    Method getItems = container.getClass().getMethod("getItems");
                    getItems.setAccessible(true);
                    itemsObj = getItems.invoke(container);
                } catch (Throwable ignored) {}

                if (itemsObj instanceof Object[]) {
                    Object[] items = (Object[]) itemsObj;
                    for (int i = 0; i < maxItems && i < items.length; i++) {
                        if (items[i] != null) {
                            Object itm = items[i];
                            int id = -1;
                            int qty = 0;
                            try {
                                Method getId = itm.getClass().getMethod("getId");
                                getId.setAccessible(true);
                                id = (Integer) getId.invoke(itm);
                            } catch (Throwable ignored) {}
                            try {
                                Method getQty = itm.getClass().getMethod("getQuantity");
                                getQty.setAccessible(true);
                                qty = (Integer) getQty.invoke(itm);
                            } catch (Throwable ignored) {}

                            if (id > 0 && id != 65535) {
                                itemIds[i] = id;
                                itemQtys[i] = Math.max(1, qty);
                            }
                        }
                    }
                }
            }
        } catch (Throwable ignored) {}
    }

    // -------------------------------------------------------------
    // Scene & Tile Objects (Trees, Banks, General Stores, Altars, Shortcuts, Agility, Ground Items)
    // -------------------------------------------------------------
    private static void processRuneLiteSceneObjects(Object client, int playerX, int playerY, int plane, StringBuilder data) {
        if (client == null || playerX <= 0 || playerY <= 0) return;
        try {
            Object scene = null;
            try {
                Method getTopView = client.getClass().getMethod("getTopLevelWorldView");
                getTopView.setAccessible(true);
                Object topView = getTopView.invoke(client);
                if (topView != null) {
                    Method getScene = topView.getClass().getMethod("getScene");
                    getScene.setAccessible(true);
                    scene = getScene.invoke(topView);
                }
            } catch (Throwable ignored) {}
            if (scene == null) {
                Method getScene = client.getClass().getMethod("getScene");
                getScene.setAccessible(true);
                scene = getScene.invoke(client);
            }
            if (scene == null) return;

            Method getTilesMethod = scene.getClass().getMethod("getTiles");
            getTilesMethod.setAccessible(true);
            Object tilesObj = getTilesMethod.invoke(scene);
            if (!(tilesObj instanceof Object[][][])) return;

            Object[][][] tiles = (Object[][][]) tilesObj;
            if (plane < 0 || plane >= tiles.length) plane = 0;
            Object[][] planeTiles = tiles[plane];
            if (planeTiles == null) return;

            int baseX = 0, baseY = 0;
            try {
                Method getBaseX = client.getClass().getMethod("getBaseX");
                Method getBaseY = client.getClass().getMethod("getBaseY");
                getBaseX.setAccessible(true);
                getBaseY.setAccessible(true);
                baseX = (Integer) getBaseX.invoke(client);
                baseY = (Integer) getBaseY.invoke(client);
            } catch (Throwable ignored) {}

            int localX = playerX - baseX;
            int localY = playerY - baseY;
            if (localX < 0 || localX >= 104 || localY < 0 || localY >= 104) {
                localX = 52; localY = 52;
            }

            int treeCount = 0;
            int bankCount = 0;
            int shopCount = 0;
            int altarCount = 0;
            int rockCount = 0;
            int shortcutCount = 0;
            int obstacleCount = 0;
            int groundItemCount = 0;
            int marksOfGraceCount = 0;

            int radius = 16;
            int minX = Math.max(0, localX - radius);
            int maxX = Math.min(103, localX + radius);
            int minY = Math.max(0, localY - radius);
            int maxY = Math.min(103, localY + radius);

            for (int tx = minX; tx <= maxX; tx++) {
                for (int ty = minY; ty <= maxY; ty++) {
                    Object tile = planeTiles[tx][ty];
                    if (tile == null) continue;

                    int worldX = baseX + tx;
                    int worldY = baseY + ty;
                    int dist = Math.max(Math.abs(worldX - playerX), Math.abs(worldY - playerY));

                    // 1. GameObjects
                    try {
                        Method getGameObjects = tile.getClass().getMethod("getGameObjects");
                        getGameObjects.setAccessible(true);
                        Object[] gObjs = (Object[]) getGameObjects.invoke(tile);
                        if (gObjs != null) {
                            for (Object go : gObjs) {
                                if (go != null) {
                                    int objId = getObjectId(go);
                                    if (objId > 0) {
                                        String name = extractObjectName(client, go, objId);
                                        String cat = classifyObject(name, objId);
                                        if ("Tree".equals(cat) && treeCount < 30) {
                                            String status = isStump(name, objId) ? "Stump" : "Available";
                                            data.append("TREE[").append(treeCount).append("]: ")
                                                .append(objId).append(",").append(name).append(",").append(dist).append(",")
                                                .append(worldX).append(",").append(worldY).append(",").append(status).append("\n");
                                            treeCount++;
                                        } else if ("Bank".equals(cat) && bankCount < 15) {
                                            data.append("BANK_OBJ[").append(bankCount).append("]: ")
                                                .append(objId).append(",").append(name).append(",").append(dist).append(",")
                                                .append(worldX).append(",").append(worldY).append("\n");
                                            bankCount++;
                                        } else if ("Shop".equals(cat) && shopCount < 15) {
                                            data.append("SHOP_OBJ[").append(shopCount).append("]: ")
                                                .append(objId).append(",").append(name).append(",").append(dist).append(",")
                                                .append(worldX).append(",").append(worldY).append("\n");
                                            shopCount++;
                                        } else if ("Altar".equals(cat) && altarCount < 15) {
                                            data.append("ALTAR_OBJ[").append(altarCount).append("]: ")
                                                .append(objId).append(",").append(name).append(",").append(dist).append(",")
                                                .append(worldX).append(",").append(worldY).append("\n");
                                            altarCount++;
                                        } else if ("Rock".equals(cat) && rockCount < 20) {
                                            data.append("ROCK_OBJ[").append(rockCount).append("]: ")
                                                .append(objId).append(",").append(name).append(",").append(dist).append(",")
                                                .append(worldX).append(",").append(worldY).append("\n");
                                            rockCount++;
                                        }

                                        if (isAgilityShortcut(name, objId) && shortcutCount < 20) {
                                            String req = getShortcutReqLevel(name, objId, worldX, worldY);
                                            data.append("SHORTCUT[").append(shortcutCount).append("]: ")
                                                .append(objId).append(",").append(name).append(",").append(req).append(",")
                                                .append(dist).append(",").append(worldX).append(",").append(worldY).append("\n");
                                            shortcutCount++;
                                        }

                                        if (isAgilityObstacle(name, objId) && obstacleCount < 25) {
                                            String course = detectAgilityCourse(playerX, playerY);
                                            data.append("AGILITY_OBSTACLE[").append(obstacleCount).append("]: ")
                                                .append(objId).append(",").append(name).append(",").append(course).append(",")
                                                .append(dist).append(",").append(worldX).append(",").append(worldY).append("\n");
                                            obstacleCount++;
                                        }
                                    }
                                }
                            }
                        }
                    } catch (Throwable ignored) {}

                    // 2. WallObject
                    try {
                        Method getWall = tile.getClass().getMethod("getWallObject");
                        getWall.setAccessible(true);
                        Object wall = getWall.invoke(tile);
                        if (wall != null) {
                            int objId = getObjectId(wall);
                            if (objId > 0) {
                                String name = extractObjectName(client, wall, objId);
                                String cat = classifyObject(name, objId);
                                if ("Bank".equals(cat) && bankCount < 15) {
                                    data.append("BANK_OBJ[").append(bankCount).append("]: ")
                                        .append(objId).append(",").append(name).append(",").append(dist).append(",")
                                        .append(worldX).append(",").append(worldY).append("\n");
                                    bankCount++;
                                } else if ("Shop".equals(cat) && shopCount < 15) {
                                    data.append("SHOP_OBJ[").append(shopCount).append("]: ")
                                        .append(objId).append(",").append(name).append(",").append(dist).append(",")
                                        .append(worldX).append(",").append(worldY).append("\n");
                                    shopCount++;
                                }

                                if (isAgilityShortcut(name, objId) && shortcutCount < 20) {
                                    String req = getShortcutReqLevel(name, objId, worldX, worldY);
                                    data.append("SHORTCUT[").append(shortcutCount).append("]: ")
                                        .append(objId).append(",").append(name).append(",").append(req).append(",")
                                        .append(dist).append(",").append(worldX).append(",").append(worldY).append("\n");
                                    shortcutCount++;
                                }

                                if (isAgilityObstacle(name, objId) && obstacleCount < 25) {
                                    String course = detectAgilityCourse(playerX, playerY);
                                    data.append("AGILITY_OBSTACLE[").append(obstacleCount).append("]: ")
                                        .append(objId).append(",").append(name).append(",").append(course).append(",")
                                        .append(dist).append(",").append(worldX).append(",").append(worldY).append("\n");
                                    obstacleCount++;
                                }
                            }
                        }
                    } catch (Throwable ignored) {}

                    // 3. GroundObject
                    try {
                        Method getGroundObj = tile.getClass().getMethod("getGroundObject");
                        getGroundObj.setAccessible(true);
                        Object groundObj = getGroundObj.invoke(tile);
                        if (groundObj != null) {
                            int objId = getObjectId(groundObj);
                            if (objId > 0) {
                                String name = extractObjectName(client, groundObj, objId);
                                if (isAgilityShortcut(name, objId) && shortcutCount < 20) {
                                    String req = getShortcutReqLevel(name, objId, worldX, worldY);
                                    data.append("SHORTCUT[").append(shortcutCount).append("]: ")
                                        .append(objId).append(",").append(name).append(",").append(req).append(",")
                                        .append(dist).append(",").append(worldX).append(",").append(worldY).append("\n");
                                    shortcutCount++;
                                }
                                if (isAgilityObstacle(name, objId) && obstacleCount < 25) {
                                    String course = detectAgilityCourse(playerX, playerY);
                                    data.append("AGILITY_OBSTACLE[").append(obstacleCount).append("]: ")
                                        .append(objId).append(",").append(name).append(",").append(course).append(",")
                                        .append(dist).append(",").append(worldX).append(",").append(worldY).append("\n");
                                    obstacleCount++;
                                }
                            }
                        }
                    } catch (Throwable ignored) {}

                    // 4. DecorativeObject
                    try {
                        Method getDecObj = tile.getClass().getMethod("getDecorativeObject");
                        getDecObj.setAccessible(true);
                        Object decObj = getDecObj.invoke(tile);
                        if (decObj != null) {
                            int objId = getObjectId(decObj);
                            if (objId > 0) {
                                String name = extractObjectName(client, decObj, objId);
                                if (isAgilityShortcut(name, objId) && shortcutCount < 20) {
                                    String req = getShortcutReqLevel(name, objId, worldX, worldY);
                                    data.append("SHORTCUT[").append(shortcutCount).append("]: ")
                                        .append(objId).append(",").append(name).append(",").append(req).append(",")
                                        .append(dist).append(",").append(worldX).append(",").append(worldY).append("\n");
                                    shortcutCount++;
                                }
                            }
                        }
                    } catch (Throwable ignored) {}

                    // 5. GroundItems
                    try {
                        Method getGroundItems = tile.getClass().getMethod("getGroundItems");
                        getGroundItems.setAccessible(true);
                        Object gItemsObj = getGroundItems.invoke(tile);
                        if (gItemsObj instanceof Iterable && groundItemCount < 30) {
                            for (Object gi : (Iterable<?>) gItemsObj) {
                                if (gi != null) {
                                    int gItemId = -1;
                                    int gQty = 1;
                                    try {
                                        Method getId = gi.getClass().getMethod("getId");
                                        getId.setAccessible(true);
                                        gItemId = (Integer) getId.invoke(gi);
                                    } catch (Throwable ignored) {}
                                    try {
                                        Method getQty = gi.getClass().getMethod("getQuantity");
                                        getQty.setAccessible(true);
                                        gQty = (Integer) getQty.invoke(gi);
                                    } catch (Throwable ignored) {}
                                    if (gItemId == 11849) {
                                        marksOfGraceCount += Math.max(1, gQty);
                                    }
                                    if (gItemId > 0 && gItemId != 65535 && groundItemCount < 30) {
                                        String gName = resolveItemName(client, gItemId);
                                        if (gName == null || gName.isEmpty()) gName = "Item #" + gItemId;
                                        data.append("GROUND_ITEM[").append(groundItemCount).append("]: ")
                                            .append(gItemId).append(",")
                                            .append(gName).append(",")
                                            .append(gQty).append(",")
                                            .append(dist).append(",")
                                            .append(worldX).append(",")
                                            .append(worldY).append("\n");
                                        groundItemCount++;
                                    }
                                }
                            }
                        }
                    } catch (Throwable ignored) {}
                }
            }

            data.append("TOTAL_TREES: ").append(treeCount).append("\n");
            data.append("TOTAL_BANKS: ").append(bankCount).append("\n");
            data.append("TOTAL_SHOPS: ").append(shopCount).append("\n");
            data.append("TOTAL_ALTARS: ").append(altarCount).append("\n");
            data.append("TOTAL_ROCKS: ").append(rockCount).append("\n");
            data.append("TOTAL_SHORTCUTS: ").append(shortcutCount).append("\n");
            data.append("TOTAL_AGILITY_OBSTACLES: ").append(obstacleCount).append("\n");
            data.append("TOTAL_GROUND_ITEMS: ").append(groundItemCount).append("\n");
            data.append("MARKS_OF_GRACE_COUNT: ").append(marksOfGraceCount).append("\n");

            String courseName = detectAgilityCourse(playerX, playerY);
            data.append("AGILITY_COURSE: ").append(courseName).append("\n");
            data.append("AGILITY_COURSE_LEVEL: ").append(detectAgilityCourseLevel(courseName)).append("\n");
        } catch (Throwable ignored) {}
    }

    private static boolean isAgilityShortcut(String name, int id) {
        if (name == null) return false;
        String n = name.toLowerCase();
        if (n.contains("stepping stone") || n.contains("loose railing") || n.contains("underwall tunnel") ||
            n.contains("crevice") || n.contains("obstacle pipe") || n.contains("log balance") ||
            n.contains("stile") || n.contains("strange floor") || n.contains("spiked chain") ||
            n.contains("handholds") || n.contains("monkey bars") || n.contains("crumbling wall") ||
            n.contains("scale rock") || n.contains("climb rock") || n.contains("squeeze-through") ||
            n.contains("narrow wall") || n.contains("grapple") || n.contains("shortcut") ||
            n.contains("climb down rock") || n.contains("climb up rock") || n.contains("climb-over") ||
            n.contains("jump down") || n.contains("jump-down") || n.contains("rope swing")) {
            return true;
        }
        return false;
    }

    private static String getShortcutReqLevel(String name, int id, int worldX, int worldY) {
        if (name == null) return "1";
        String n = name.toLowerCase();
        if (n.contains("stile")) return "1";
        if (n.contains("crumbling wall")) return "5";
        if (n.contains("underwall tunnel")) {
            if (worldX >= 3070 && worldX <= 3120 && worldY >= 3240 && worldY <= 3280) return "11";
            if (worldX >= 2530 && worldX <= 2560 && worldY >= 3080 && worldY <= 3110) return "16";
            if (worldX >= 3130 && worldX <= 3155 && worldY >= 3500 && worldY <= 3520) return "21";
            if (worldX >= 3080 && worldX <= 3120 && worldY >= 3490 && worldY <= 3510) return "34";
            if (worldX >= 2870 && worldX <= 2900 && worldY >= 9800 && worldY <= 9850) return "70";
            return "15+";
        }
        if (n.contains("loose railing")) {
            if (worldX >= 3260 && worldX <= 3290 && worldY >= 3370 && worldY <= 3410) return "13";
            return "65";
        }
        if (n.contains("stepping stone")) {
            if (worldX >= 2850 && worldX <= 2880 && worldY >= 2960 && worldY <= 2990) return "12";
            if (worldX >= 3170 && worldX <= 3200 && worldY >= 3350 && worldY <= 3380) return "31";
            if (worldX >= 2850 && worldX <= 2880 && worldY >= 2950 && worldY <= 2980) return "74";
            if (worldX >= 3040 && worldX <= 3080 && worldY >= 3830 && worldY <= 3870) return "82";
            return "30+";
        }
        if (n.contains("log balance")) {
            if (worldX >= 2590 && worldX <= 2620 && worldY >= 3470 && worldY <= 3500) return "20";
            if (worldX >= 2600 && worldX <= 2620 && worldY >= 3330 && worldY <= 3350) return "33";
            return "20+";
        }
        if (n.contains("strange floor")) return "80";
        if (n.contains("spiked chain")) return "61 / 71";
        if (n.contains("handholds")) return "60";
        if (n.contains("monkey bars")) return "57";
        if (n.contains("crevice")) {
            if (worldX >= 1300 && worldX <= 1350 && worldY >= 3800 && worldY <= 3850) return "42";
            if (worldX >= 3420 && worldX <= 3450 && worldY >= 3550 && worldY <= 3580) return "62";
            if (worldX >= 2800 && worldX <= 2850 && worldY >= 3650 && worldY <= 3700) return "72";
            return "20+";
        }
        if (n.contains("obstacle pipe")) {
            if (worldX >= 3000 && worldX <= 3030 && worldY >= 3370 && worldY <= 3400) return "5";
            if (worldX >= 2880 && worldX <= 2900 && worldY >= 9790 && worldY <= 9820) return "70";
            return "35+";
        }
        if (n.contains("scale rock") || n.contains("climb rock")) return "38+";
        if (n.contains("rope swing")) return "35+";
        if (n.contains("grapple")) return "21+";
        return "1+";
    }

    private static boolean isAgilityObstacle(String name, int id) {
        if (name == null) return false;
        String n = name.toLowerCase();
        if (n.contains("rough wall") || n.contains("tightrope") || n.contains("jump-gap") ||
            (n.contains("gap") && !n.contains("glass")) || n.contains("balance beam") ||
            n.contains("obstacle net") || n.contains("balancing rope") || n.contains("zip line") ||
            n.contains("balancing ledge") || n.contains("climb ledge") || n.contains("edge") ||
            n.contains("hurdle") || n.contains("stepping pad") || n.contains("pillar") ||
            n.contains("skull slope") || n.contains("death slide") || n.contains("pyramid climbing") ||
            n.contains("jump off") || n.contains("vault") || (n.contains("tree branch") && (id >= 23000 || id <= 16000))) {
            return true;
        }
        return false;
    }

    private static String detectAgilityCourse(int playerX, int playerY) {
        int regionId = ((playerX >> 6) << 8) | (playerY >> 6);
        switch (regionId) {
            case 9781:
            case 9782: return "Gnome Stronghold";
            case 12338:
            case 12339: return "Draynor Village Rooftop";
            case 13105:
            case 13106: return "Al Kharid Rooftop";
            case 12853:
            case 12854: return "Varrock Rooftop";
            case 13878:
            case 13879: return "Canifis Rooftop";
            case 12084:
            case 12085: return "Falador Rooftop";
            case 10806:
            case 10807: return "Seers' Village Rooftop";
            case 13358:
            case 13359: return "Pollnivneach Rooftop";
            case 10553:
            case 10554: return "Rellekka Rooftop";
            case 10547:
            case 10548: return "Ardougne Rooftop";
            case 13110:
            case 13111: return "Prifddinas Course";
            case 6448:
            case 6449:
            case 6704: return "Colossal Wyrm Course";
            case 11050:
            case 11051: return "Ape Atoll Course";
            case 11836:
            case 11837: return "Wilderness Course";
            case 14134:
            case 14135: return "Werewolf Course";
            case 13356:
            case 13357: return "Agility Pyramid";
            case 10039:
            case 10040: return "Barbarian Outpost";
            case 10559: return "Penguin Course";
            case 10835: return "Dorgesh-Kaan";
            case 11157: return "Brimhaven Arena";
            default: return "None";
        }
    }

    private static String detectAgilityCourseLevel(String course) {
        if (course == null) return "1";
        switch (course) {
            case "Gnome Stronghold": return "1";
            case "Draynor Village Rooftop": return "10";
            case "Al Kharid Rooftop": return "20";
            case "Varrock Rooftop": return "30";
            case "Canifis Rooftop": return "40";
            case "Falador Rooftop": return "50";
            case "Seers' Village Rooftop": return "60";
            case "Pollnivneach Rooftop": return "70";
            case "Rellekka Rooftop": return "80";
            case "Ardougne Rooftop": return "90";
            case "Prifddinas Course": return "75";
            case "Colossal Wyrm Course": return "50";
            case "Ape Atoll Course": return "48";
            case "Wilderness Course": return "52";
            case "Werewolf Course": return "60";
            case "Agility Pyramid": return "30";
            case "Barbarian Outpost": return "35";
            case "Penguin Course": return "30";
            case "Dorgesh-Kaan": return "70";
            case "Brimhaven Arena": return "1";
            default: return "-";
        }
    }

    // -------------------------------------------------------------
    // Minigames Detection (Pest Control, Wintertodt, Tempoross, GotR, etc.)
    // -------------------------------------------------------------
    private static void processRuneLiteMinigames(Object client, int playerX, int playerY, StringBuilder data) {
        if (client == null) return;
        try {
            int regionId = ((playerX >> 6) << 8) | (playerY >> 6);
            boolean active = false;
            String name = "None";
            String status = "Inactive";
            String points = "0";
            String extra = "-";

            // 1. Pest Control (Region 10536, 10537, 10538 or widget 408 / 407)
            if (regionId == 10536 || regionId == 10537 || regionId == 10538 || regionId == 10539 || getWidget(client, 408, 0) != null || getWidget(client, 407, 0) != null) {
                active = true;
                name = "Pest Control";
                int pcPoints = getVarbitValue(client, 4087);
                if (pcPoints >= 0) points = pcPoints + " Commendation Points";
                Object gameWidget = getWidget(client, 408, 0);
                if (gameWidget != null) {
                    status = "In Game Instance";
                    extra = "Shields Active / Defending Void Knight";
                } else {
                    status = "In Lander Boat";
                    extra = regionId == 10538 ? "Veteran Boat" : (regionId == 10537 ? "Intermediate Boat" : "Novice Boat");
                }
            }
            // 2. Wintertodt (Region 6462 or widget 396)
            else if (regionId == 6462 || getWidget(client, 396, 0) != null) {
                active = true;
                name = "Wintertodt";
                int wtPoints = getVarbitValue(client, 7980);
                if (wtPoints >= 0) points = wtPoints + " Points";
                int warmth = getVarbitValue(client, 5683);
                status = warmth > 0 ? "Warmth: " + warmth + "%" : "Active Battle";
                extra = "Pyromancers Active";
            }
            // 3. Tempoross (Region 12588, 12332, 12076 or widget 437)
            else if (regionId == 12588 || regionId == 12332 || regionId == 12076 || getWidget(client, 437, 0) != null) {
                active = true;
                name = "Tempoross";
                int storm = getVarbitValue(client, 11893);
                int essence = getVarbitValue(client, 11894);
                int energy = getVarbitValue(client, 11895);
                status = "Storm: " + (storm >= 0 ? storm : 0) + "%";
                points = "Energy: " + (energy >= 0 ? energy : 0) + "%, Essence: " + (essence >= 0 ? essence : 0) + "%";
                extra = "Subduing Tempoross";
            }
            // 4. Guardians of the Rift (Region 14484, 14485 or widget 745)
            else if (regionId == 14484 || regionId == 14485 || getWidget(client, 745, 0) != null) {
                active = true;
                name = "Guardians of the Rift";
                status = "Great Guardian Active";
                extra = "Catalytic / Elemental Energy";
            }
            // 5. Barbarian Assault (Region 7508, 7509, 10332 or widget 256 / 485)
            else if (regionId == 7508 || regionId == 7509 || regionId == 10332 || getWidget(client, 256, 0) != null || getWidget(client, 485, 0) != null) {
                active = true;
                name = "Barbarian Assault";
                status = "Wave Active";
                extra = "Call Horn Active";
            }
            // 6. Castle Wars (Region 9520, 9776 or widget 58)
            else if (regionId == 9520 || regionId == 9776 || getWidget(client, 58, 0) != null) {
                active = true;
                name = "Castle Wars";
                status = "Game In Progress";
                extra = "Saradomin vs Zamorak";
            }
            // 7. Soul Wars (Region 8792, 8793, 9048, 9049 or widget 685)
            else if (regionId == 8792 || regionId == 8793 || regionId == 9048 || regionId == 9049 || getWidget(client, 685, 0) != null) {
                active = true;
                name = "Soul Wars";
                status = "Game In Progress";
                extra = "Avatar Battle";
            }
            // 8. Blast Furnace (Region 7757 or widget 474)
            else if (regionId == 7757 || getWidget(client, 474, 0) != null) {
                active = true;
                name = "Blast Furnace";
                status = "Furnace Operating";
                extra = "Coffer Active";
            }
            // 9. Barrows (Region 14131, 14231, 14232)
            else if (regionId == 14131 || regionId == 14231 || regionId == 14232) {
                active = true;
                name = "Barrows";
                int slain = getVarbitValue(client, 457);
                status = (slain >= 0 ? slain : 0) + "/6 Brothers Slain";
                points = (slain >= 0 ? (slain * 100 / 6) : 0) + "% Potential";
                extra = "Crypt / Tombs";
            }
            // 10. Fight Caves / Inferno / Fortis Colosseum
            else if (regionId == 9551) {
                active = true;
                name = "Fight Caves";
                status = "TzHaar Fight Cave";
                extra = "TzTok-Jad Challenge";
            } else if (regionId == 9043) {
                active = true;
                name = "The Inferno";
                status = "Inferno Active";
                extra = "TzKal-Zuk Challenge";
            } else if (regionId == 7216) {
                active = true;
                name = "Fortis Colosseum";
                status = "Colosseum Active";
                extra = "Sol Heredit Challenge";
            }
            // 11. Nightmare Zone (Region 9033)
            else if (regionId == 9033) {
                active = true;
                name = "Nightmare Zone";
                status = "Dream Active";
                extra = "Dominic Onion Dream";
            }
            // 12. Mage Training Arena (Region 13462, 13463)
            else if (regionId == 13462 || regionId == 13463) {
                active = true;
                name = "Mage Training Arena";
                status = "Training Active";
                extra = "Pizazz Points";
            }
            // 13. Tithe Farm (Region 7222)
            else if (regionId == 7222) {
                active = true;
                name = "Tithe Farm";
                status = "Farming Active";
                extra = "Hosidius Tithe Farm";
            }
            // 14. Volcanic Mine (Region 15263)
            else if (regionId == 15263) {
                active = true;
                name = "Volcanic Mine";
                status = "Mining Active";
                extra = "Fossil Island Volcano";
            }
            // 15. Last Man Standing (Region 13658, 13659, 13914 or widget 328)
            else if (regionId == 13658 || regionId == 13659 || regionId == 13914 || getWidget(client, 328, 0) != null) {
                active = true;
                name = "Last Man Standing";
                status = "Battle Royale";
                extra = "PvP Survival";
            }
            // 16. Mahogany Homes
            int mhContract = getVarbitValue(client, 10594);
            int mhPoints = getVarbitValue(client, 10595);
            if (mhContract > 0) {
                active = true;
                name = "Mahogany Homes";
                status = "Contract Active (Tier " + mhContract + ")";
                if (mhPoints >= 0) points = mhPoints + " Carpenter Points";
                extra = "Building Furniture";
            }

            data.append("MINIGAME_ACTIVE: ").append(active ? "True" : "False").append("\n");
            data.append("MINIGAME_NAME: ").append(name).append("\n");
            data.append("MINIGAME_STATUS: ").append(status).append("\n");
            data.append("MINIGAME_POINTS: ").append(points).append("\n");
            data.append("MINIGAME_EXTRA: ").append(extra).append("\n");
        } catch (Throwable ignored) {}
    }

    private static int getObjectId(Object obj) {
        if (obj == null) return -1;
        try {
            Method m = obj.getClass().getMethod("getId");
            m.setAccessible(true);
            return (Integer) m.invoke(obj);
        } catch (Throwable ignored) {}
        return -1;
    }

    private static String classifyObject(String name, int id) {
        if (name == null) return "Other";
        String n = name.toLowerCase();
        if (n.contains("tree") || n.contains("oak") || n.contains("willow") || n.contains("teak") ||
            n.contains("maple") || n.contains("mahogany") || n.contains("yew") || n.contains("magic") ||
            n.contains("redwood") || n.contains("blisterwood") || n.contains("pine") || n.contains("juniper") ||
            n.contains("evergreen") || n.contains("dead tree") || n.contains("sulliuscep") || n.contains("stump")) {
            return "Tree";
        }
        if (n.contains("bank") || n.contains("grand exchange booth") || n.contains("deposit box")) {
            return "Bank";
        }
        if (n.contains("shop") || n.contains("counter") || n.contains("general store") || n.contains("stall") || n.contains("trading post")) {
            return "Shop";
        }
        if (n.contains("altar") || n.contains("pool") || n.contains("fountain of rune") || n.contains("statuette")) {
            return "Altar";
        }
        if (n.contains("rocks") || n.contains("ore") || n.contains("vein") || n.contains("amethyst")) {
            return "Rock";
        }
        return "Other";
    }

    private static boolean isStump(String name, int id) {
        if (name != null && name.toLowerCase().contains("stump")) return true;
        return (id >= 1342 && id <= 1359);
    }

    private static String extractObjectName(Object client, Object obj, int id) {
        if (id <= 0) return "Object";
        String cached = OBJECT_NAME_CACHE.get(id);
        if (cached != null) return cached;

        // 1. obj.getComposition() / obj.getDefinition()
        if (obj != null) {
            for (String cm : new String[]{"getComposition", "getTransformedComposition", "getDefinition"}) {
                try {
                    Method m = obj.getClass().getMethod(cm);
                    m.setAccessible(true);
                    Object comp = m.invoke(obj);
                    if (comp != null) {
                        try {
                            Method getName = comp.getClass().getMethod("getName");
                            getName.setAccessible(true);
                            Object res = getName.invoke(comp);
                            if (res instanceof String) {
                                String s = ((String) res).replaceAll("<[^>]*>", "").replace('\u00A0', ' ').trim();
                                if (!s.isEmpty() && !s.equalsIgnoreCase("null") && !s.equalsIgnoreCase("null-name")) {
                                    OBJECT_NAME_CACHE.put(id, s);
                                    return s;
                                }
                            }
                        } catch (Throwable ignored) {}
                    }
                } catch (Throwable ignored) {}
            }
        }

        // 2. Query client for ObjectComposition
        if (client != null) {
            for (String cm : new String[]{"getObjectDefinition", "loadObjectComposition", "getObjectComposition"}) {
                try {
                    Method m = client.getClass().getMethod(cm, int.class);
                    m.setAccessible(true);
                    Object comp = m.invoke(client, id);
                    if (comp != null) {
                        try {
                            Method getName = comp.getClass().getMethod("getName");
                            getName.setAccessible(true);
                            Object res = getName.invoke(comp);
                            if (res instanceof String) {
                                String s = ((String) res).replaceAll("<[^>]*>", "").replace('\u00A0', ' ').trim();
                                if (!s.isEmpty() && !s.equalsIgnoreCase("null") && !s.equalsIgnoreCase("null-name")) {
                                    OBJECT_NAME_CACHE.put(id, s);
                                    return s;
                                }
                            }
                        } catch (Throwable ignored) {}
                    }
                } catch (Throwable ignored) {}
            }
        }

        // 3. Builtin object name dictionary
        String builtin = getBuiltinObjectName(id);
        if (builtin != null) {
            OBJECT_NAME_CACHE.put(id, builtin);
            return builtin;
        }

        return "Object_" + id;
    }

    private static String getBuiltinObjectName(int id) {
        switch (id) {
            case 1276: case 1278: return "Tree";
            case 1281: return "Oak tree";
            case 1308: case 5551: case 5552: case 5553: return "Willow tree";
            case 1307: return "Maple tree";
            case 1309: return "Yew tree";
            case 1306: return "Magic tree";
            case 34007: return "Redwood tree";
            case 9036: return "Teak tree";
            case 9034: return "Mahogany tree";
            case 1342: return "Oak tree stump";
            case 1343: return "Willow tree stump";
            case 1344: return "Maple tree stump";
            case 1345: return "Yew tree stump";
            case 1346: return "Magic tree stump";
            case 10355: case 10356: case 10357: case 10083: case 24101: case 24347: case 26711: case 27267:
            case 27292: case 28430: case 28431: case 28432: case 28433: case 28546: case 28547: case 28548:
            case 28549: case 36786: case 39239: case 4483: case 16642: return "Bank booth";
            case 10517: case 26254: case 25937: return "Bank deposit box";
            case 10060: case 10061: return "Grand Exchange booth";
            case 409: case 410: case 411: case 412: return "Altar";
            case 11360: case 11361: return "Iron rocks";
            case 11364: case 11365: return "Coal rocks";
            case 11366: case 11367: return "Mithril rocks";
            case 11368: case 11369: return "Adamantite rocks";
            case 11370: case 11371: return "Runite rocks";
            case 11362: case 11363: return "Silver rocks";
            case 11372: case 11373: return "Gold rocks";
            case 11374: case 11375: return "Clay rocks";
            case 11376: case 11377: return "Copper rocks";
            case 11378: case 11379: return "Tin rocks";
            default: return null;
        }
    }
}
