using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace OsrsMr.Core.Queries
{
    /// <summary>
    /// Base fluent query builder providing RuneMate-style entity querying, filtering, and sorting.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TQuery">The concrete query builder type.</typeparam>
    public abstract class EntityQuery<T, TQuery> : IEnumerable<T> where TQuery : EntityQuery<T, TQuery>
    {
        protected IEnumerable<T> Source { get; }
        protected List<Func<T, bool>> Predicates { get; } = new();
        protected Func<T, object>? OrderKeySelector { get; set; }
        protected bool OrderDescending { get; set; }

        protected EntityQuery(IEnumerable<T> source)
        {
            Source = source ?? Enumerable.Empty<T>();
        }

        protected TQuery Self => (TQuery)this;

        /// <summary>
        /// Filters entities using a custom predicate.
        /// </summary>
        public TQuery Filter(Func<T, bool> predicate)
        {
            if (predicate != null)
            {
                Predicates.Add(predicate);
            }
            return Self;
        }

        /// <summary>
        /// Orders entities ascending by a key selector.
        /// </summary>
        public TQuery OrderBy<TKey>(Func<T, TKey> keySelector)
        {
            OrderKeySelector = x => keySelector(x)!;
            OrderDescending = false;
            return Self;
        }

        /// <summary>
        /// Orders entities descending by a key selector.
        /// </summary>
        public TQuery OrderByDescending<TKey>(Func<T, TKey> keySelector)
        {
            OrderKeySelector = x => keySelector(x)!;
            OrderDescending = true;
            return Self;
        }

        /// <summary>
        /// Evaluates the query pipeline and returns matching entities.
        /// </summary>
        public IEnumerable<T> Results()
        {
            var query = Source;
            foreach (var predicate in Predicates)
            {
                query = query.Where(predicate);
            }

            if (OrderKeySelector != null)
            {
                query = OrderDescending
                    ? query.OrderByDescending(OrderKeySelector)
                    : query.OrderBy(OrderKeySelector);
            }

            return query;
        }

        /// <summary>
        /// Returns the first matching entity, or default if none.
        /// </summary>
        public T? First() => Results().FirstOrDefault();

        /// <summary>
        /// Returns the count of matching entities.
        /// </summary>
        public int Count() => Results().Count();

        /// <summary>
        /// Checks if any entity matches the query.
        /// </summary>
        public bool Any() => Results().Any();

        /// <summary>
        /// Returns all matching entities as a list.
        /// </summary>
        public List<T> ToList() => Results().ToList();

        public IEnumerator<T> GetEnumerator() => Results().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
