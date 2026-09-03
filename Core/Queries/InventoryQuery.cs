using System;
using System.Collections.Generic;
using System.Linq;

namespace OsrsMr.Core.Queries
{
    public class InventoryQuery : EntityQuery<ItemSnapshot, InventoryQuery>
    {
        public InventoryQuery(IEnumerable<ItemSnapshot> source) : base(source) { }

        public InventoryQuery Named(params string[] names)
        {
            if (names == null || names.Length == 0) return this;
            var set = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            return Filter(item => set.Contains(item.Name));
        }

        public InventoryQuery NameContains(string text)
        {
            if (string.IsNullOrEmpty(text)) return this;
            return Filter(item => item.Name.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        public InventoryQuery WithIds(params int[] ids)
        {
            if (ids == null || ids.Length == 0) return this;
            var set = new HashSet<int>(ids);
            return Filter(item => set.Contains(item.Id));
        }

        public InventoryQuery InSlot(int slot)
        {
            return Filter(item => item.Slot == slot);
        }

        public InventoryQuery MinQuantity(int minQty)
        {
            return Filter(item => item.Quantity >= minQty);
        }

        public int TotalQuantity()
        {
            return Results().Sum(item => item.Quantity);
        }
    }
}
