using System;
using System.Threading.Tasks;

namespace OsrsMr.Core.Scripting
{
    /// <summary>
    /// Base class for scripts executing in a continuous loop with sync and async support.
    /// </summary>
    public abstract class LoopScript : BotScript
    {
        /// <summary>
        /// Synchronous loop iteration. Returns milliseconds to sleep before the next iteration.
        /// </summary>
        public virtual int OnLoop() => 1000;

        /// <summary>
        /// Asynchronous loop iteration. Allows awaiting interactions and conditions.
        /// Returns milliseconds to sleep before the next iteration, or -1 to stop.
        /// </summary>
        public virtual Task<int> OnLoopAsync() => Task.FromResult(OnLoop());
    }
}
