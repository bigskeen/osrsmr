using System;
using System.Collections.Generic;

namespace OsrsMr.Core.Scripting
{
    /// <summary>
    /// Executes a collection of TreeTasks in priority order.
    /// In each cycle, the first task whose Validate() returns true is executed.
    /// </summary>
    public abstract class TaskScript : LoopScript
    {
        private readonly List<TreeTask> _tasks = new();
        public IReadOnlyList<TreeTask> Tasks => _tasks;

        protected void AddTasks(params TreeTask[] tasks)
        {
            if (tasks != null)
            {
                _tasks.AddRange(tasks);
            }
        }

        public override int OnLoop()
        {
            foreach (var task in _tasks)
            {
                try
                {
                    if (task.Validate())
                    {
                        SetTask(task.Name);
                        int delay = task.Execute();
                        return delay >= 0 ? delay : 100;
                    }
                }
                catch (Exception ex)
                {
                    Log($"Task '{task.Name}' error: {ex.Message}");
                    return 500;
                }
            }

            SetTask("Idle / Waiting");
            return 300;
        }
    }
}
