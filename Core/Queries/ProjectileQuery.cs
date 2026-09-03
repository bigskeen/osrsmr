using System;
using System.Collections.Generic;
using System.Linq;

namespace OsrsMr.Core.Queries
{
    /// <summary>
    /// Fluent query engine for finding active in-flight projectiles.
    /// </summary>
    public class ProjectileQuery : EntityQuery<ProjectileSnapshot, ProjectileQuery>
    {
        public ProjectileQuery(IEnumerable<ProjectileSnapshot> source) : base(source) { }

        public ProjectileQuery WithIds(params int[] ids)
        {
            if (ids == null || ids.Length == 0) return this;
            var set = new HashSet<int>(ids);
            return Filter(p => set.Contains(p.Id));
        }

        public ProjectileQuery TargetingPlayer()
        {
            var state = BrainEngine.Instance.State;
            // TargetIndex -1 indicates player in RS protocol
            return Filter(p => p.TargetIndex == -1);
        }

        public ProjectileQuery TargetingIndex(int targetIndex)
        {
            return Filter(p => p.TargetIndex == targetIndex);
        }

        public ProjectileQuery NearPlayer(int maxDistance = 15)
        {
            var player = BrainEngine.Instance.State.Player;
            if (player == null) return this;
            return Filter(p => Math.Abs(p.TargetX - player.WorldX) <= maxDistance && Math.Abs(p.TargetY - player.WorldY) <= maxDistance);
        }

        public ProjectileQuery WithCyclesRemaining(int minCycles)
        {
            return Filter(p => p.RemainingCycles >= minCycles);
        }
    }
}
