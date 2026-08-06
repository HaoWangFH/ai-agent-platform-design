using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Skight.AgentPlatform
{
    public class BackgroundCommandHandle
    {
        public string Id { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public DateTime StartedAtUtc { get; set; }
    }

    public static class TerminalTool
    {
        private const int DefaultTimeoutMs = 60_000;
        private const int DefaultMaxOutputBytes = 100 * 1024;
        private static readonly ConcurrentDictionary<string, (Process Process, StringBuilder Stdout, StringBuilder Stderr)> BackgroundProcesses = new();
        private static readonly bool IsWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

        public static string GetShellName() => IsWindows ? "powershell.exe" : "/bin/bash";

        public static string GetToolDescription() => $"Execute terminal command in {GetShellName()} environment with timeout handling and background process support.";

        private static ProcessStartInfo CreateProcessStartInfo(string cmdStr)
        {
            var escapedCmd = IsWindows ? cmdStr.Replace("\"", "\\\"") : cmdStr;
            return new ProcessStartInfo
            {
                FileName = GetShellName(),
                Arguments = IsWindows ? $"-NoProfile -ExecutionPolicy Bypass -Command \"{escapedCmd}\"" : $"-c \"{cmdStr}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        private static string CombineOutput(string stdout, string stderr)
        {
            if (string.IsNullOrWhiteSpace(stderr)) return stdout;
            if (string.IsNullOrWhiteSpace(stdout)) return $"[stderr]\n{stderr}";
            return $"{stdout}\n\n[stderr]\n{stderr}";
        }

        public static async Task<string> ExecuteCommandAsync(string cmdStr, int timeoutMs = DefaultTimeoutMs, int maxOutputBytes = DefaultMaxOutputBytes)
        {
            if (string.IsNullOrWhiteSpace(cmdStr)) return "Error: Command cannot be empty.";
            
            using var proc = new Process { StartInfo = CreateProcessStartInfo(cmdStr) };
            try
            {
                if (!proc.Start()) return "Error: Failed to start command process.";

                var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                var stderrTask = proc.StandardError.ReadToEndAsync();
                var waitTask = proc.WaitForExitAsync();

                if (await Task.WhenAny(waitTask, Task.Delay(timeoutMs)) == waitTask)
                {
                    var stdout = await stdoutTask;
                    var stderr = await stderrTask;
                    return ToolSecurity.TruncateOutput(maxOutputBytes, CombineOutput(stdout, stderr));
                }
                
                try { if (!proc.HasExited) proc.Kill(true); } catch { /* ignore */ }
                return $"Error: Command '{cmdStr}' timed out after {timeoutMs} ms and was terminated.";
            }
            catch (Exception ex)
            {
                return $"Error executing command: {ex.Message}";
            }
        }

        public static string StartBackgroundCommand(string cmdStr)
        {
            if (string.IsNullOrWhiteSpace(cmdStr)) throw new ArgumentException("Command cannot be empty.");
            
            var proc = new Process { StartInfo = CreateProcessStartInfo(cmdStr), EnableRaisingEvents = true };
            var stdoutBuffer = new StringBuilder();
            var stderrBuffer = new StringBuilder();

            proc.OutputDataReceived += (_, e) => { if (e.Data != null) lock (stdoutBuffer) stdoutBuffer.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (stderrBuffer) stderrBuffer.AppendLine(e.Data); };

            if (!proc.Start()) throw new Exception("Failed to start background command process.");
            
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            var id = Guid.NewGuid().ToString("N");
            BackgroundProcesses[id] = (proc, stdoutBuffer, stderrBuffer);
            return id;
        }

        public static string GetBackgroundCommandOutput(string id, int maxOutputBytes = DefaultMaxOutputBytes)
        {
            if (BackgroundProcesses.TryGetValue(id, out var data))
            {
                string stdout, stderr;
                lock (data.Stdout) stdout = data.Stdout.ToString();
                lock (data.Stderr) stderr = data.Stderr.ToString();
                
                var status = data.Process.HasExited ? $"completed (exit code: {data.Process.ExitCode})" : "running";
                var combined = CombineOutput(stdout, stderr);
                var content = $"Background command {id} status: {status}\n\n{combined}";
                return ToolSecurity.TruncateOutput(maxOutputBytes, content);
            }
            return $"Error: Background command '{id}' not found.";
        }

        public static string StopBackgroundCommand(string id)
        {
            if (BackgroundProcesses.TryRemove(id, out var data))
            {
                try
                {
                    if (!data.Process.HasExited) data.Process.Kill(true);
                    data.Process.Dispose();
                    return $"Background command '{id}' stopped.";
                }
                catch (Exception ex)
                {
                    return $"Error stopping background command '{id}': {ex.Message}";
                }
            }
            return $"Error: Background command '{id}' not found.";
        }
    }
}
