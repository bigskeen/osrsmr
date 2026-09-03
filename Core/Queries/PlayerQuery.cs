using System;
using System.Collections.Generic;
using System.Linq;

namespace OsrsMr.Core.Queries
{
    public class PlayerQuery : EntityQuery<NearbyPlayerSnapshot, PlayerQuery>
    {
        public PlayerQuery(IEnumerable<NearbyPlayerSnapshot> source) : base(source) { }

        public PlayerQuery Named(params string[] names)
        {
            if (names == null || names.Length == 0) return this;
            var set = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            return Filter(p => set.Contains(p.Name));
        }

        public PlayerQuery WithinDistance(int maxDistance)
        {
            return Filter(p => p.Distance <= maxDistance);
        }

        public PlayerQuery InCombatLevelRange(int minLevel, int maxLevel)
        {
            return Filter(p => p.CombatLevel >= minLevel && p.CombatLevel <= maxLevel);
        }

        public NearbyPlayerSnapshot? Nearest()
        {
            return OrderBy(p => p.Distance).First();
        }
    }
}
