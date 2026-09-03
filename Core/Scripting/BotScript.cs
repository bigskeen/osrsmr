using System;
using System.Windows.Media;
using OsrsMr.Core;

namespace OsrsMr.Core.Scripting
{
    public enum ScriptStatus
    {
        Stopped,
        Running,
        Paused,
        Crashed
    }

    public enum ScriptHealthState
    {
        Healthy,
        Warning,
        Issue
    }

    public abstract class BotScript
    {
        public ScriptStatus Status { get; internal set; } = ScriptStatus.Stopped;
        public ScriptHealthState HealthState { get; protected set; } = ScriptHealthState.Healthy;
        public string CurrentTaskName { get; protected set; } = "Initializing";
        public string CurrentAction { get; protected set; } = "Starting bot engine...";
        public string CurrentSubTask { get; protected set; } = "";
        public string? LastIssueText { get; protected set; }
        public DateTime StartTime { get; internal set; }
        public TimeSpan RunningTime => Status == ScriptStatus.Stopped ? TimeSpan.Zero : DateTime.UtcNow - StartTime;
        public long LoopCount { get; internal set; }
        public int ActionsCompleted { get; internal set; }
        public ScriptManifestAttribute? Manifest => (ScriptManifestAttribute?)Attribute.GetCustomAttribute(GetType(), typeof(ScriptManifestAttribute));

        public GameState State => BrainEngine.Instance.State;

        public event Action<string>? OnLog;

        public virtual void OnStart() { }
        public virtual void OnStop() { }
        public virtual void OnPause() { }
        public virtual void OnResume() { }
        public virtual void OnPaint(DrawingContext dc) { }

        protected void Log(string message)
        {
            OnLog?.Invoke($"[{GetType().Name}] {message}");
        }

        public void SetTask(string taskName)
        {
            CurrentTaskName = taskName;
            CurrentAction = taskName;
        }

        public void SetAction(string action, string? subTask = null)
        {
            CurrentAction = action;
            if (subTask != null)
            {
                CurrentSubTask = subTask;
            }
        }

        public void ReportWarning(string warning)
        {
            HealthState = ScriptHealthState.Warning;
            LastIssueText = warning;
            Log($"[WARNING] {warning}");
        }

        public void ReportIssue(string issue)
        {
            HealthState = ScriptHealthState.Issue;
            LastIssueText = issue;
            Log($"[ISSUE DETECTED] {issue}");
        }

        public void ClearIssue()
        {
            HealthState = ScriptHealthState.Healthy;
            LastIssueText = null;
        }
    }
}
