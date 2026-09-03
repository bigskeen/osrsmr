using System;
using System.Collections.Generic;
using System.Linq;

namespace OsrsMr.Core.Queries
{
    public enum EquipmentSlot
    {
        Head = 0,
        Cape = 1,
        Amulet = 2,
        Weapon = 3,
        Body = 4,
        Shield = 5,
        Legs = 7,
        Gloves = 9,
        Boots = 10,
        Ring = 12,
        Ammo = 13
    }

    /// <summary>
    /// Fluent query engine for querying worn/equipped equipment items.
    /// </summary>
    public class EquipmentQuery : EntityQuery<ItemSnapshot, EquipmentQuery>
    {
        public EquipmentQuery(IEnumerable<ItemSnapshot> source) : base(source) { }

        public EquipmentQuery InSlot(EquipmentSlot slot)
        {
            int slotIdx = (int)slot;
            return Filter(i => i.Slot == slotIdx);
        }

        public EquipmentQuery InSlot(int slot)
        {
            return Filter(i => i.Slot == slot);
        }

        public EquipmentQuery Named(params string[] names)
        {
            if (names == null || names.Length == 0) return this;
            var set = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            return Filter(i => set.Contains(i.Name));
        }

        public EquipmentQuery ContainingName(string substring)
        {
            if (string.IsNullOrEmpty(substring)) return this;
            return Filter(i => i.Name.Contains(substring, StringComparison.OrdinalIgnoreCase));
        }

        public EquipmentQuery WithIds(params int[] ids)
        {
            if (ids == null || ids.Length == 0) return this;
            var set = new HashSet<int>(ids);
            return Filter(i => set.Contains(i.Id));
        }

        public EquipmentQuery MinQuantity(int qty)
        {
            return Filter(i => i.Quantity >= qty);
        }
    }
}
