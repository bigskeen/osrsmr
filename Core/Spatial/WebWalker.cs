using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Scripting;
using OsrsMr.Core.Spatial;

namespace OsrsMr.Core.Spatial
{
    /// <summary>
    /// Supported bank destinations across Gielinor for script settings.
    /// </summary>
    public enum BankLocation
    {
        Nearest,
        GrandExchange,
        VarrockWest,
        VarrockEast,
        FaladorEast,
        FaladorWest,
        Edgeville,
        Draynor,
        AlKharid,
        SeersVillage,
        Catherby,
        ArdougneSouth,
        LumbridgeCastle
    }

    /// <summary>
    /// Major OSRS landmark locations and world web destinations.
    /// </summary>
    public static class WorldLocations
    {
        public static readonly WorldPoint LumbridgeCastle = new(3222, 3218, 0);
        public static readonly WorldPoint GrandExchange = new(3165, 3487, 0);
        public static readonly WorldPoint VarrockWestBank = new(3183, 3438, 0);
        public static readonly WorldPoint VarrockEastBank = new(3253, 3420, 0);
        public static readonly WorldPoint FaladorEastBank = new(3013, 3355, 0);
        public static readonly WorldPoint FaladorWestBank = new(2946, 3368, 0);
        public static readonly WorldPoint EdgevilleBank = new(3093, 3493, 0);
        public static readonly WorldPoint DraynorBank = new(3093, 3244, 0);
        public static readonly WorldPoint AlKharidBank = new(3269, 3167, 0);
        public static readonly WorldPoint SeersVillageBank = new(2725, 3492, 0);
        public static readonly WorldPoint CatherbyBank = new(2808, 3441, 0);
        public static readonly WorldPoint ArdougneSouthBank = new(2655, 3283, 0);
        public static readonly WorldPoint BarbarianVillage = new(3082, 3420, 0);

        /// <summary>
        /// Resolves a world coordinate into a human-readable OSRS area/location name.
        /// </summary>
        public static string ResolveAreaName(int x, int y, int plane = 0, int regionId = 0)
        {
            if (x <= 0 || y <= 0) return "Unknown";

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
            if (y >= 3520 && x >= 2940 && x <= 3400)
            {
                int wildyLevel = (y - 3520) / 8 + 1;
                if (x >= 3125 && x <= 3160 && y >= 3620 && y <= 3650) return "Ferox Enclave (Safe)";
                if (x >= 3075 && x <= 3125 && y >= 3940 && y <= 3970) return $"Mage Arena (Wildy Lvl {wildyLevel})";
                if (x >= 3225 && x <= 3245 && y >= 3630 && y <= 3650) return $"Chaos Temple (Wildy Lvl {wildyLevel})";
                if (x >= 3050 && x <= 3100 && y >= 3830 && y <= 3880) return $"Lava Maze (Wildy Lvl {wildyLevel})";
                if (x >= 3275 && x <= 3305 && y >= 3925 && y <= 3950) return $"Rogues' Castle (Wildy Lvl {wildyLevel})";
                if (x >= 3360 && x <= 3390 && y >= 3885 && y <= 3910) return $"Fountain of Rune (Wildy Lvl {wildyLevel})";
                return $"Wilderness (Lvl {wildyLevel})";
            }

            int calcRegion = regionId > 0 ? regionId : ((x >> 6) << 8) | (y >> 6);
            return calcRegion switch
            {
                12597 => "West Varrock",
                12853 => "East Varrock",
                12598 => "Grand Exchange",
                12850 => "Lumbridge",
                11828 => "Falador",
                12342 => "Edgeville",
                12338 => "Draynor",
                13105 or 13106 => "Al Kharid",
                12341 => "Barbarian Village",
                12082 => "Port Sarim",
                11826 => "Rimmington",
                10806 => "Seers' Village",
                11062 => "Catherby",
                10291 or 10292 or 10547 or 10548 => "Ardougne",
                11571 or 11572 => "Taverley",
                11573 or 11829 => "Burthorpe",
                10288 => "Yanille",
                11568 or 11569 or 11824 or 11825 => "Karamja",
                13878 or 13877 or 14134 => "Canifis",
                6963 or 6964 or 7219 or 7220 => "Hosidius",
                _ => (calcRegion > 0 ? $"Region #{calcRegion}" : "Gielinor")
            };
        }
    }

    /// <summary>
    /// Global WebWalker managing long-distance path generation and multi-tile traversal.
    /// </summary>
    public static class WebWalker
    {
        private static GameState State => BrainEngine.Instance.State;

        /// <summary>
        /// Walks the player step-by-step to a destination WorldPoint using local path slicing and minimap navigation.
        /// </summary>
        public static async Task<bool> WalkToAsync(WorldPoint target, int reachDistance = 3)
        {
            if (State.Player == null) return false;

            int safetyCounter = 0;
            const int maxSteps = 150;

            while (safetyCounter++ < maxSteps)
            {
                var playerPt = new WorldPoint(State.Player.WorldX, State.Player.WorldY, State.Player.Plane);
                int dist = (int)Math.Sqrt(Math.Pow(playerPt.X - target.X, 2) + Math.Pow(playerPt.Y - target.Y, 2));

                if (dist <= reachDistance)
                {
                    return true;
                }

                // If target is far, pick an intermediate stepping stone within ~12 tiles
                WorldPoint nextStep = target;
                if (dist > 12)
                {
                    double ratio = 12.0 / dist;
                    int stepX = (int)(playerPt.X + ((target.X - playerPt.X) * ratio));
                    int stepY = (int)(playerPt.Y + ((target.Y - playerPt.Y) * ratio));
                    nextStep = new WorldPoint(stepX, stepY, target.Plane);
                }

                // Walk to the next waypoint
                await Movement.WalkToAsync(nextStep.X, nextStep.Y);

                // Wait until we reach near the step or stop moving
                await Condition.WaitAsync(() =>
                {
                    if (State.Player == null) return true;
                    int curDist = (int)Math.Sqrt(Math.Pow(State.Player.WorldX - nextStep.X, 2) + Math.Pow(State.Player.WorldY - nextStep.Y, 2));
                    return curDist <= 3 || !State.Player.IsMoving;
                }, timeoutMs: 8000, pollIntervalMs: 200);

                await Condition.SleepAsync(100, 250);
            }

            return false;
        }

        /// <summary>
        /// Walks directly to the specified or nearest known bank.
        /// </summary>
        public static async Task<bool> WalkToBankAsync(BankLocation location = BankLocation.Nearest)
        {
            if (location == BankLocation.Nearest)
            {
                return await WalkToNearestBankAsync();
            }

            WorldPoint target = location switch
            {
                BankLocation.GrandExchange => WorldLocations.GrandExchange,
                BankLocation.VarrockWest => WorldLocations.VarrockWestBank,
                BankLocation.VarrockEast => WorldLocations.VarrockEastBank,
                BankLocation.FaladorEast => WorldLocations.FaladorEastBank,
                BankLocation.FaladorWest => WorldLocations.FaladorWestBank,
                BankLocation.Edgeville => WorldLocations.EdgevilleBank,
                BankLocation.Draynor => WorldLocations.DraynorBank,
                BankLocation.AlKharid => WorldLocations.AlKharidBank,
                BankLocation.SeersVillage => WorldLocations.SeersVillageBank,
                BankLocation.Catherby => WorldLocations.CatherbyBank,
                BankLocation.ArdougneSouth => WorldLocations.ArdougneSouthBank,
                BankLocation.LumbridgeCastle => WorldLocations.LumbridgeCastle,
                _ => WorldLocations.GrandExchange
            };

            return await WalkToAsync(target, reachDistance: 4);
        }

        /// <summary>
        /// Walks directly to the nearest known bank.
        /// </summary>
        public static async Task<bool> WalkToNearestBankAsync()
        {
            if (State.Player == null) return false;

            var banks = new[]
            {
                WorldLocations.GrandExchange,
                WorldLocations.VarrockWestBank,
                WorldLocations.VarrockEastBank,
                WorldLocations.EdgevilleBank,
                WorldLocations.FaladorEastBank,
                WorldLocations.FaladorWestBank,
                WorldLocations.DraynorBank,
                WorldLocations.AlKharidBank,
                WorldLocations.SeersVillageBank,
                WorldLocations.CatherbyBank,
                WorldLocations.ArdougneSouthBank
            };

            var nearestBank = banks
                .OrderBy(b => Math.Pow(b.X - State.Player.WorldX, 2) + Math.Pow(b.Y - State.Player.WorldY, 2))
                .First();

            return await WalkToAsync(nearestBank, reachDistance: 4);
        }
    }
}
