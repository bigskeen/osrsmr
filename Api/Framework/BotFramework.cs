using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OsrsMr.Api.Framework
{
    public enum ScriptStatus
    {
        Stopped,
        Starting,
        Running,
        Paused,
        Stopping,
        Error
    }

    public enum TreeStatus
    {
        Success,
        Failure,
        Running
    }

    public abstract class TreeTask
    {
        public string Name { get; set; } = "Task";
        public abstract Task<TreeStatus> ExecuteAsync(CancellationToken ct);
    }

    public class LeafTask : TreeTask
    {
        private readonly Func<CancellationToken, Task<TreeStatus>> _action;

        public LeafTask(string name, Func<CancellationToken, Task<TreeStatus>> action)
        {
            Name = name;
            _action = action;
        }

        public LeafTask(string name, Func<Task<bool>> action)
        {
            Name = name;
            _action = async (ct) => (await action()) ? TreeStatus.Success : TreeStatus.Failure;
        }

        public override async Task<TreeStatus> ExecuteAsync(CancellationToken ct)
        {
            return await _action(ct);
        }
    }

    public class Selector : TreeTask
    {
        private readonly List<TreeTask> _children = new();

        public Selector(string name, params TreeTask[] children)
        {
            Name = name;
            _children.AddRange(children);
        }

        public void Add(TreeTask task) => _children.Add(task);

        public override async Task<TreeStatus> ExecuteAsync(CancellationToken ct)
        {
            foreach (var child in _children)
            {
                if (ct.IsCancellationRequested) return TreeStatus.Failure;
                var status = await child.ExecuteAsync(ct);
                if (status != TreeStatus.Failure) return status;
            }
            return TreeStatus.Failure;
        }
    }

    public class Sequence : TreeTask
    {
        private readonly List<TreeTask> _children = new();

        public Sequence(string name, params TreeTask[] children)
        {
            Name = name;
            _children.AddRange(children);
        }

        public void Add(TreeTask task) => _children.Add(task);

        public override async Task<TreeStatus> ExecuteAsync(CancellationToken ct)
        {
            foreach (var child in _children)
            {
                if (ct.IsCancellationRequested) return TreeStatus.Failure;
                var status = await child.ExecuteAsync(ct);
                if (status != TreeStatus.Success) return status;
            }
            return TreeStatus.Success;
        }
    }

    public abstract class Bot
    {
        public string Name { get; set; } = "Base Bot";
        public string Description { get; set; } = "";
        public string Author { get; set; } = "OsrsMr";
        public string Version { get; set; } = "1.0.0";
        public string Category { get; set; } = "General";
        public string StatusText { get; set; } = "Idle";

        public event Action<string>? OnLog;

        public virtual Task<bool> OnStartAsync() => Task.FromResult(true);
        public abstract Task<int> OnLoopAsync(CancellationToken ct);
        public virtual Task OnStopAsync() => Task.CompletedTask;

        protected void Log(string message)
        {
            OnLog?.Invoke($"[{Name}] {message}");
        }
    }

    public abstract class TreeBot : Bot
    {
        private TreeTask? _rootTree;

        public abstract TreeTask BuildTree();

        public override Task<bool> OnStartAsync()
        {
            _rootTree = BuildTree();
            Log("Behavior tree initialized.");
            return Task.FromResult(true);
        }

        public override async Task<int> OnLoopAsync(CancellationToken ct)
        {
            if (_rootTree == null)
            {
                _rootTree = BuildTree();
            }

            if (_rootTree != null)
            {
                await _rootTree.ExecuteAsync(ct);
            }

            return 600; // Standard 1 tick (600ms) game cycle delay
        }
    }

    public class ScriptRunner
    {
        private static ScriptRunner? _instance;
        public static ScriptRunner Instance => _instance ??= new ScriptRunner();

        public Bot? ActiveBot { get; private set; }
        public ScriptStatus Status { get; private set; } = ScriptStatus.Stopped;
        public TimeSpan Runtime => _stopwatch.Elapsed;
        public int LoopIterations { get; private set; }

        public event Action<ScriptStatus>? OnStatusChanged;
        public event Action<string>? OnLogMessage;
        public event Action? OnTick;

        private readonly Stopwatch _stopwatch = new();
        private CancellationTokenSource? _cts;
        private Task? _runnerTask;
        private readonly List<Bot> _registeredBots = new();

        public IReadOnlyList<Bot> RegisteredBots => _registeredBots;

        public void RegisterBot(Bot bot)
        {
            if (!_registeredBots.Contains(bot))
                _registeredBots.Add(bot);
        }

        public void UnregisterBot(Bot bot)
        {
            _registeredBots.Remove(bot);
        }

        public async Task<bool> StartAsync(Bot bot)
        {
            if (Status == ScriptStatus.Running || Status == ScriptStatus.Paused)
            {
                await StopAsync();
            }

            ActiveBot = bot;
            Status = ScriptStatus.Starting;
            OnStatusChanged?.Invoke(Status);

            ActiveBot.OnLog += Log;

            try
            {
                bool started = await ActiveBot.OnStartAsync();
                if (!started)
                {
                    Log($"Failed to start script '{ActiveBot.Name}'");
                    Status = ScriptStatus.Error;
                    OnStatusChanged?.Invoke(Status);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log($"Error in OnStartAsync: {ex.Message}");
                Status = ScriptStatus.Error;
                OnStatusChanged?.Invoke(Status);
                return false;
            }

            _cts = new CancellationTokenSource();
            _stopwatch.Restart();
            LoopIterations = 0;
            Status = ScriptStatus.Running;
            OnStatusChanged?.Invoke(Status);
            Log($"Started script '{ActiveBot.Name}' v{ActiveBot.Version}");

            _runnerTask = Task.Run(() => ExecutionLoop(_cts.Token));
            return true;
        }

        public void Pause()
        {
            if (Status == ScriptStatus.Running)
            {
                Status = ScriptStatus.Paused;
                _stopwatch.Stop();
                OnStatusChanged?.Invoke(Status);
                Log("Script paused");
            }
        }

        public void Resume()
        {
            if (Status == ScriptStatus.Paused)
            {
                Status = ScriptStatus.Running;
                _stopwatch.Start();
                OnStatusChanged?.Invoke(Status);
                Log("Script resumed");
            }
        }

        public async Task StopAsync()
        {
            if (Status == ScriptStatus.Stopped) return;

            Status = ScriptStatus.Stopping;
            OnStatusChanged?.Invoke(Status);
            _cts?.Cancel();

            if (_runnerTask != null)
            {
                try { await _runnerTask; } catch { }
            }

            if (ActiveBot != null)
            {
                try { await ActiveBot.OnStopAsync(); } catch { }
                ActiveBot.OnLog -= Log;
            }

            _stopwatch.Stop();
            Status = ScriptStatus.Stopped;
            OnStatusChanged?.Invoke(Status);
            Log($"Stopped script '{ActiveBot?.Name}'");
        }

        private async Task ExecutionLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (Status == ScriptStatus.Paused)
                {
                    await Task.Delay(200, ct).ContinueWith(_ => { });
                    continue;
                }

                int delay = 600;
                try
                {
                    if (ActiveBot != null)
                    {
                        delay = await ActiveBot.OnLoopAsync(ct);
                        LoopIterations++;
                        OnTick?.Invoke();
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Log($"Exception in bot loop: {ex.Message}");
                    delay = 1000;
                }

                if (delay <= 0) delay = 50;
                try
                {
                    await Task.Delay(delay, ct);
                }
                catch (OperationCanceledException) { break; }
            }
        }

        private void Log(string msg)
        {
            OnLogMessage?.Invoke(msg);
        }
    }
}
