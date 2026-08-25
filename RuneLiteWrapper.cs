using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace RuneLiteWrapper
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string realExe = Path.Combine(baseDir, "RuneLite_real.exe");
                if (!File.Exists(realExe))
                {
                    realExe = Path.Combine(baseDir, "RuneLite_orig.exe");
                }

                if (!File.Exists(realExe))
                {
                    return 1;
                }

                string agentJar = Path.Combine(baseDir, "agent.jar");
                if (!File.Exists(agentJar))
                {
                    string candidate = @"C:\Users\bigsk\RiderProjects\osrsmr\agent.jar";
                    if (File.Exists(candidate)) agentJar = candidate;
                }

                var newArgs = new List<string>();
                bool hasAgent = false;
                bool hasAttachFlag = false;

                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    if (arg.Equals("-XX:+DisableAttachMechanism", StringComparison.OrdinalIgnoreCase) ||
                        arg.Equals("+DisableAttachMechanism", StringComparison.OrdinalIgnoreCase))
                    {
                        newArgs.Add("-XX:-DisableAttachMechanism");
                        hasAttachFlag = true;
                    }
                    else if (arg.StartsWith("-XX:+DisableAttachMechanism", StringComparison.OrdinalIgnoreCase))
                    {
                        newArgs.Add("-XX:-DisableAttachMechanism");
                        hasAttachFlag = true;
                    }
                    else if (arg.StartsWith("-javaagent", StringComparison.OrdinalIgnoreCase))
                    {
                        hasAgent = true;
                        newArgs.Add(arg);
                    }
                    else
                    {
                        newArgs.Add(arg);
                    }
                }

                if (File.Exists(agentJar) && !hasAgent)
                {
                    newArgs.Add("-J");
                    newArgs.Add("-javaagent:\"" + agentJar.Replace('/', '\\') + "\"");
                }

                if (!hasAttachFlag)
                {
                    newArgs.Add("-J");
                    newArgs.Add("-XX:-DisableAttachMechanism");
                }

                var sb = new StringBuilder();
                foreach (var a in newArgs)
                {
                    if (sb.Length > 0) sb.Append(" ");
                    if (a.Contains(" ") && !a.StartsWith("\""))
                    {
                        sb.Append("\"").Append(a).Append("\"");
                    }
                    else
                    {
                        sb.Append(a);
                    }
                }

                var psi = new ProcessStartInfo
                {
                    FileName = realExe,
                    Arguments = sb.ToString(),
                    WorkingDirectory = baseDir,
                    UseShellExecute = false
                };

                if (File.Exists(agentJar))
                {
                    string toolOpts = "-XX:-DisableAttachMechanism -javaagent:\"" + agentJar.Replace('/', '\\') + "\"";
                    psi.EnvironmentVariables["JAVA_TOOL_OPTIONS"] = toolOpts;
                    psi.EnvironmentVariables["_JAVA_OPTIONS"] = toolOpts;
                }

                using (var proc = Process.Start(psi))
                {
                    if (proc != null)
                    {
                        proc.WaitForExit();
                        return proc.ExitCode;
                    }
                }
                return 0;
            }
            catch (Exception)
            {
                return -1;
            }
        }
    }
}
