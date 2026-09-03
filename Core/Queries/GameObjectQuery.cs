using System;
using System.Collections.Generic;
using System.Linq;

namespace OsrsMr.Core.Queries
{
    public class GameObjectQuery : EntityQuery<SceneObjectSnapshot, GameObjectQuery>
    {
        public GameObjectQuery(IEnumerable<SceneObjectSnapshot> source) : base(source) { }

        public GameObjectQuery Named(params string[] names)
        {
            if (names == null || names.Length == 0) return this;
            var set = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            return Filter(obj => set.Contains(obj.Name));
        }

        public GameObjectQuery WithIds(params int[] ids)
        {
            if (ids == null || ids.Length == 0) return this;
            var set = new HashSet<int>(ids);
            return Filter(obj => set.Contains(obj.Id));
        }

        public GameObjectQuery InCategory(params string[] categories)
        {
            if (categories == null || categories.Length == 0) return this;
            var set = new HashSet<string>(categories, StringComparer.OrdinalIgnoreCase);
            return Filter(obj => set.Contains(obj.Category));
        }

        public GameObjectQuery WithinDistance(int maxDistance)
        {
            return Filter(obj => obj.Distance <= maxDistance);
        }

        public GameObjectQuery OnPlane(int plane)
        {
            return Filter(obj => obj.Plane == plane);
        }

        public SceneObjectSnapshot? Nearest()
        {
            return OrderBy(obj => obj.Distance).First();
        }
    }
}
