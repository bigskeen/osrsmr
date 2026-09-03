using System;
using OsrsMr.Core;

namespace OsrsMr.Core.Scripting
{
    /// <summary>
    /// A single modular task inside a TaskScript or Behavior Tree.
    /// </summary>
    public abstract class TreeTask
    {
        public virtual string Name => GetType().Name;
        protected GameState State => BrainEngine.Instance.State;

        /// <summary>
        /// Validates whether the conditions are met to execute this task.
        /// </summary>
        public abstract bool Validate();

        /// <summary>
        /// Executes the action.
        /// </summary>
        /// <returns>Milliseconds delay before evaluating next task, or -1 to yield.</returns>
        public abstract int Execute();
    }
}
