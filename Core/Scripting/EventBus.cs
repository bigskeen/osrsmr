using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace OsrsMr.Core.Scripting
{
    public record TickEvent(int GameTick);
    public record HitsplatEvent(string Target, int Damage, string HitType);
    public record InventoryChangedEvent(int Slot, int ItemId, int Quantity);
    public record ChatMessageEvent(string Sender, string Message, string Channel);

    public static class EventBus
    {
        private static readonly ConcurrentDictionary<Type, List<Delegate>> Subscriptions = new();
        private static readonly object LockObj = new();

        public static void Subscribe<T>(Action<T> handler)
        {
            lock (LockObj)
            {
                var list = Subscriptions.GetOrAdd(typeof(T), _ => new List<Delegate>());
                list.Add(handler);
            }
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            lock (LockObj)
            {
                if (Subscriptions.TryGetValue(typeof(T), out var list))
                {
                    list.Remove(handler);
                }
            }
        }

        public static void Publish<T>(T eventArgs)
        {
            Delegate[]? targets = null;
            lock (LockObj)
            {
                if (Subscriptions.TryGetValue(typeof(T), out var list))
                {
                    targets = list.ToArray();
                }
            }

            if (targets != null)
            {
                foreach (var del in targets)
                {
                    try
                    {
                        ((Action<T>)del).Invoke(eventArgs);
                    }
                    catch { }
                }
            }
        }
    }
}
