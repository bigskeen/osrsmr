using System;
using System.Threading.Tasks;
using OsrsMr.Core.Input;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Interaction
{
    public enum Spell
    {
        // Standard Combat
        WindStrike,
        WaterStrike,
        EarthStrike,
        FireStrike,
        WindBolt,
        WaterBolt,
        EarthBolt,
        FireBolt,
        WindBlast,
        WaterBlast,
        EarthBlast,
        FireBlast,
        WindWave,
        WaterWave,
        EarthWave,
        FireWave,
        WindSurge,
        WaterSurge,
        EarthSurge,
        FireSurge,
        CrumbleUndead,

        // Utility & Teleports
        VarrockTeleport,
        LumbridgeTeleport,
        FaladorTeleport,
        CamelotTeleport,
        ArdougneTeleport,
        WatchtowerTeleport,
        TrollheimTeleport,
        TeleportToHouse,
        LowLevelAlchemy,
        HighLevelAlchemy,
        SuperheatItem,
        BonesToBananas,
        BonesToPeaches,
        ChargeWaterOrb,
        ChargeEarthOrb,
        ChargeFireOrb,
        ChargeAirOrb,

        // Ancient Magicks
        SmokeRush,
        ShadowRush,
        BloodRush,
        IceRush,
        SmokeBurst,
        ShadowBurst,
        BloodBurst,
        IceBurst,
        SmokeBlitz,
        ShadowBlitz,
        BloodBlitz,
        IceBlitz,
        SmokeBarrage,
        ShadowBarrage,
        BloodBarrage,
        IceBarrage,
        PaddewwaTeleport,
        SenntistenTeleport,
        KharyrllTeleport,
        LassarTeleport,
        DareeyakTeleport,
        CarrallangarTeleport,
        AnnakarlTeleport,
        GhorrockTeleport
    }

    /// <summary>
    /// Interaction controller for magic, spellcasting, high alchemy, and teleports.
    /// </summary>
    public static class Magic
    {
        private static GameState State => BrainEngine.Instance.State;

        /// <summary>
        /// Selects or casts a spell from the magic tab.
        /// </summary>
        public static async Task<bool> CastAsync(Spell spell)
        {
            // Magic Tab widget group in standard OSRS is 218
            var spellWidget = Queries.Queries.Widgets
                .InGroup(218)
                .Filter(w => w.Name.Contains(spell.ToString(), StringComparison.OrdinalIgnoreCase) ||
                             w.Text.Contains(spell.ToString(), StringComparison.OrdinalIgnoreCase))
                .VisibleOnly()
                .First();

            if (spellWidget != null)
            {
                await spellWidget.ClickAsync();
                await Condition.SleepAsync(150, 300);
                return true;
            }

            // Fallback: switch to Magic Tab (F6) and click
            await Win32Input.SendKeyAsync(Win32Input.VK_F6);
            await Condition.SleepAsync(100, 250);
            return false;
        }

        /// <summary>
        /// Casts a targeted spell onto an NPC.
        /// </summary>
        public static async Task<bool> CastOnNpcAsync(Spell spell, NpcSnapshot npc)
        {
            if (npc == null) return false;

            if (await CastAsync(spell))
            {
                await Condition.SleepAsync(100, 250);
                return await npc.InteractAsync("Cast");
            }
            return false;
        }

        /// <summary>
        /// Casts High Alchemy on an item in inventory by instance.
        /// </summary>
        public static async Task<bool> HighAlchAsync(ItemSnapshot item)
        {
            if (item == null) return false;

            if (await CastAsync(Spell.HighLevelAlchemy))
            {
                await Condition.SleepAsync(100, 250);
                return await item.InteractAsync("Cast");
            }
            return false;
        }

        /// <summary>
        /// Casts High Alchemy on an item in inventory by name.
        /// </summary>
        public static async Task<bool> CastHighAlchAsync(string itemName)
        {
            var item = Queries.Queries.Inventory.Named(itemName).First();
            if (item == null) return false;
            return await HighAlchAsync(item);
        }
    }
}
