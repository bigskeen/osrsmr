using System;
using System.Threading;
using System.Threading.Tasks;
using OsrsMr.Core.Profiles;

namespace OsrsMr.Core.Scripting
{
    /// <summary>
    /// Thread runner and lifecycle manager for currently executing bot scripts.
    /// </summary>
    public class ScriptEngine
    {
        private static ScriptEngine? _instance;
        public static ScriptEngine Instance => _instance ??= new ScriptEngine();

        public BotScript? ActiveScript { get; private set; }
        public BreakHandler BreakHandler { get; } = new();

        public bool IsRunning => ActiveScript != null && ActiveScript.Status == ScriptStatus.Running;
        public bool IsPaused => ActiveScript != null && ActiveScript.Status == ScriptStatus.Paused;

        public event Action<BotScript, ScriptStatus>? OnScriptStatusChanged;
        public event Action<string>? OnScriptLog;

        private CancellationTokenSource? _cts;
        private Task? _executionTask;

        public ScriptEngine()
        {
            BreakHandler.OnBreakEvent += msg => OnScriptLog?.Invoke(msg);
        }

        public void StartScript(BotScript script)
        {
            if (IsRunning)
            {
                StopScript();
            }

            ActiveScript = script;
            ActiveScript.Status = ScriptStatus.Running;
            ActiveScript.StartTime = DateTime.UtcNow;
            ActiveScript.OnLog += msg => OnScriptLog?.Invoke(msg);

            XpTracker.Instance.Reset(ActiveScript.State);
            BreakHandler.Initialize(ProfileManager.Instance.ActiveProfile);

            _cts = new CancellationTokenSource();
            CancellationToken ct = _cts.Token;

            OnScriptStatusChanged?.Invoke(ActiveScript, ScriptStatus.Running);

            _executionTask = Task.Run(async () =>
            {
                try
                {
                    ActiveScript.OnStart();

                    if (ActiveScript is LoopScript loopScript)
                    {
                        while (!ct.IsCancellationRequested)
                        {
                            if (ActiveScript.Status == ScriptStatus.Paused)
                            {
                                await Task.Delay(200, ct);
                                continue;
                            }

                            // Evaluate auto-pause / safety triggers
                            if (BreakHandler.CheckSafetyTriggers(ActiveScript.State, ProfileManager.Instance.ActiveProfile, out string safetyReason))
                            {
                                PauseScript();
                                OnScriptLog?.Invoke($"[SAFETY PAUSE] {safetyReason}");
                                continue;
                            }

                            // Evaluate scheduled breaks
                            if (BreakHandler.CheckBreakCondition(ProfileManager.Instance.ActiveProfile, out int breakSecs))
                            {
                                ActiveScript.Status = ScriptStatus.Paused;
                                OnScriptStatusChanged?.Invoke(ActiveScript, ScriptStatus.Paused);
                                await Task.Delay(breakSecs * 1000, ct);
                                BreakHandler.CompleteBreak(ProfileManager.Instance.ActiveProfile);
                                ActiveScript.Status = ScriptStatus.Running;
                                OnScriptStatusChanged?.Invoke(ActiveScript, ScriptStatus.Running);
                            }

                            ActiveScript.LoopCount++;
                            int delay = await loopScript.OnLoopAsync();
                            if (delay < 0) break;

                            await Task.Delay(Math.Max(20, delay), ct);
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    ActiveScript.Status = ScriptStatus.Crashed;
                    ActiveScript.ReportIssue($"Script crashed: {ex.Message}");
                    OnScriptLog?.Invoke($"[CRASH] Script faulted: {ex.Message}");
                    OnScriptStatusChanged?.Invoke(ActiveScript, ScriptStatus.Crashed);
                    return;
                }
                finally
                {
                    try { ActiveScript.OnStop(); } catch { }
                    ActiveScript.Status = ScriptStatus.Stopped;
                    OnScriptStatusChanged?.Invoke(ActiveScript, ScriptStatus.Stopped);
                }
            });
        }

        public void PauseScript()
        {
            if (ActiveScript != null && ActiveScript.Status == ScriptStatus.Running)
            {
                ActiveScript.Status = ScriptStatus.Paused;
                ActiveScript.OnPause();
                OnScriptStatusChanged?.Invoke(ActiveScript, ScriptStatus.Paused);
            }
        }

        public void ResumeScript()
        {
            if (ActiveScript != null && ActiveScript.Status == ScriptStatus.Paused)
            {
                ActiveScript.Status = ScriptStatus.Running;
                ActiveScript.OnResume();
                OnScriptStatusChanged?.Invoke(ActiveScript, ScriptStatus.Running);
            }
        }

        public void StopScript()
        {
            if (ActiveScript == null) return;

            _cts?.Cancel();
            try { _executionTask?.Wait(1000); } catch { }

            ActiveScript.Status = ScriptStatus.Stopped;
            OnScriptStatusChanged?.Invoke(ActiveScript, ScriptStatus.Stopped);
            ActiveScript = null;
        }
    }
}
