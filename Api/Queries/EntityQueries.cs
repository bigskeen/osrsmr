using System;
using System.Collections.Generic;
using System.Linq;
using OsrsMr.Api.Entities;

namespace OsrsMr.Api.Queries
{
    public class EntityQuery<T> where T : class
    {
        private IEnumerable<T> _source;

        public EntityQuery(IEnumerable<T> source)
        {
            _source = source ?? Enumerable.Empty<T>();
        }

        public EntityQuery<T> Filter(Func<T, bool> predicate)
        {
            _source = _source.Where(predicate);
            return this;
        }

        public EntityQuery<T> Named(string name, bool exact = false)
        {
            return Filter(item =>
            {
                string itemName = "";
                if (item is NpcEntity npc) itemName = npc.Name;
                else if (item is GameObjectEntity obj) itemName = obj.Name;
                else if (item is GroundItemEntity gi) itemName = gi.Name;
                else if (item is ItemEntity it) itemName = it.Name;
                else if (item is FishingSpotEntity fs) itemName = fs.Name;
                else if (item is ShortcutEntity sc) itemName = sc.Name;
                else if (item is AgilityObstacleEntity ao) itemName = ao.Name;

                return exact ? itemName.Equals(name, StringComparison.OrdinalIgnoreCase)
                             : itemName.Contains(name, StringComparison.OrdinalIgnoreCase);
            });
        }

        public EntityQuery<T> WithId(int id)
        {
            return Filter(item =>
            {
                if (item is NpcEntity npc) return npc.Id == id;
                if (item is GameObjectEntity obj) return obj.Id == id;
                if (item is GroundItemEntity gi) return gi.Id == id;
                if (item is ItemEntity it) return it.Id == id;
                if (item is FishingSpotEntity fs) return fs.Id == id;
                if (item is ShortcutEntity sc) return sc.Id == id;
                if (item is AgilityObstacleEntity ao) return ao.Id == id;
                return false;
            });
        }

        public EntityQuery<T> WithinDistance(int maxDistance)
        {
            return Filter(item =>
            {
                if (item is LocatableEntity loc) return loc.Distance <= maxDistance;
                return true;
            });
        }

        public T? Nearest()
        {
            if (typeof(LocatableEntity).IsAssignableFrom(typeof(T)))
            {
                return _source.OrderBy(i => (i as LocatableEntity)!.Distance).FirstOrDefault();
            }
            return _source.FirstOrDefault();
        }

        public T? First() => _source.FirstOrDefault();
        public List<T> ToList() => _source.ToList();
        public int Count() => _source.Count();
        public bool Exists() => _source.Any();
    }
}
