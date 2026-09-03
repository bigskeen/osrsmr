using System;
using System.Collections.Generic;
using System.Linq;

namespace OsrsMr.Core.Queries
{
    public class GroundItemQuery : EntityQuery<GroundItemSnapshot, GroundItemQuery>
    {
        public GroundItemQuery(IEnumerable<GroundItemSnapshot> source) : base(source) { }

        public GroundItemQuery Named(params string[] names)
        {
            if (names == null || names.Length == 0) return this;
            var set = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            return Filter(item => set.Contains(item.Name));
        }

        public GroundItemQuery WithIds(params int[] ids)
        {
            if (ids == null || ids.Length == 0) return this;
            var set = new HashSet<int>(ids);
            return Filter(item => set.Contains(item.Id));
        }

        public GroundItemQuery WithinDistance(int maxDistance)
        {
            return Filter(item => item.Distance <= maxDistance);
        }

        public GroundItemQuery MinGeValue(int minPrice)
        {
            return Filter(item => item.GePrice >= minPrice);
        }

        public GroundItemSnapshot? Nearest()
        {
            return OrderBy(item => item.Distance).First();
        }
    }
}
