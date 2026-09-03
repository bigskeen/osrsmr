using System;
using System.Collections.Generic;
using System.Linq;

namespace OsrsMr.Core.Queries
{
    public class NpcQuery : EntityQuery<NpcSnapshot, NpcQuery>
    {
        public NpcQuery(IEnumerable<NpcSnapshot> source) : base(source) { }

        public NpcQuery Named(params string[] names)
        {
            if (names == null || names.Length == 0) return this;
            var set = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            return Filter(npc => set.Contains(npc.Name));
        }

        public NpcQuery WithIds(params int[] ids)
        {
            if (ids == null || ids.Length == 0) return this;
            var set = new HashSet<int>(ids);
            return Filter(npc => set.Contains(npc.Id));
        }

        public NpcQuery WithinDistance(int maxDistance)
        {
            return Filter(npc => npc.Distance <= maxDistance);
        }

        public NpcQuery InteractingWithMe()
        {
            return Filter(npc => npc.IsInteractingWithMe);
        }

        public NpcQuery WithAnimation(int animId)
        {
            return Filter(npc => npc.Animation == animId);
        }

        public NpcQuery Alive()
        {
            return Filter(npc => npc.CurrentHp > 0 || npc.Health != "0%");
        }

        public NpcSnapshot? Nearest()
        {
            return OrderBy(npc => npc.Distance).First();
        }
    }
}
