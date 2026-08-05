namespace Skight.AgentPlatform.FSharp

open System
open System.Collections.Concurrent
open System.Diagnostics
open System.Text
open System.Threading.Tasks

module TerminalTool =
    type BackgroundCommandHandle = {
        Id: string
        Command: string
        StartedAtUtc: DateTime
    }

    let private defaultTimeoutMs = 60_000
    let private defaultMaxOutputBytes = 100 * 1024

    let private backgroundProcesses = ConcurrentDictionary<string, Process * StringBuilder * StringBuilder>()

    let private isWindows = Environment.OSVersion.Platform = PlatformID.Win32NT

    let getShellName () =
        if isWindows then "powershell.exe" else "/bin/bash"

    let getToolDescription () =
        sprintf "Execute terminal command in %s environment with timeout handling and background process support." (getShellName ())

    let private createProcessStartInfo (cmdStr: string) =
        let startInfo = ProcessStartInfo()
        startInfo.FileName <- getShellName ()
        startInfo.Arguments <- if isWindows then sprintf "-NoProfile -ExecutionPolicy Bypass -Command \"%s\"" cmdStr else sprintf "-c \"%s\"" cmdStr
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true
        startInfo.RedirectStandardInput <- false
        startInfo.UseShellExecute <- false
        startInfo.CreateNoWindow <- true
        startInfo

    let private combineOutput (stdout: string) (stderr: string) =
        if String.IsNullOrWhiteSpace(stderr) then stdout
        elif String.IsNullOrWhiteSpace(stdout) then sprintf "[stderr]\n%s" stderr
        else sprintf "%s\n\n[stderr]\n%s" stdout stderr

    let executeCommandAsync (timeoutMs: int) (maxOutputBytes: int) (cmdStr: string) : Async<string> =
        async {
            if String.IsNullOrWhiteSpace(cmdStr) then
                return "Error: Command cannot be empty."
            else
                use proc = new Process()
                proc.StartInfo <- createProcessStartInfo cmdStr

                try
                    if not (proc.Start()) then
                        return "Error: Failed to start command process."
                    else
                        let stdoutTask = proc.StandardOutput.ReadToEndAsync()
                        let stderrTask = proc.StandardError.ReadToEndAsync()
                        let waitTask = proc.WaitForExitAsync()

                        let! completed =
                            Task.WhenAny(waitTask, Task.Delay(timeoutMs))
                            |> Async.AwaitTask

                        if obj.ReferenceEquals(completed, waitTask) then
                            let! stdout = stdoutTask |> Async.AwaitTask
                            let! stderr = stderrTask |> Async.AwaitTask
                            let combined = combineOutput stdout stderr
                            return ToolSecurity.truncateOutput maxOutputBytes combined
                        else
                            try
                                if not proc.HasExited then
                                    proc.Kill(true)
                            with _ -> ()

                            return sprintf "Error: Command '%s' timed out after %d ms and was terminated." cmdStr timeoutMs
                with ex ->
                    return sprintf "Error executing command: %s" ex.Message
        }

    let executeCommandDefaultAsync (cmdStr: string) : Async<string> =
        executeCommandAsync defaultTimeoutMs defaultMaxOutputBytes cmdStr

    let startBackgroundCommand (cmdStr: string) : Result<BackgroundCommandHandle, string> =
        try
            if String.IsNullOrWhiteSpace(cmdStr) then
                Error "Command cannot be empty."
            else
                let proc = new Process()
                proc.StartInfo <- createProcessStartInfo cmdStr
                proc.EnableRaisingEvents <- true

                let stdoutBuffer = StringBuilder()
                let stderrBuffer = StringBuilder()

                proc.OutputDataReceived.Add(fun args ->
                    if not (isNull args.Data) then
                        lock stdoutBuffer (fun () -> stdoutBuffer.AppendLine(args.Data) |> ignore))

                proc.ErrorDataReceived.Add(fun args ->
                    if not (isNull args.Data) then
                        lock stderrBuffer (fun () -> stderrBuffer.AppendLine(args.Data) |> ignore))

                if not (proc.Start()) then
                    Error "Failed to start background command process."
                else
                    proc.BeginOutputReadLine()
                    proc.BeginErrorReadLine()

                    let id = Guid.NewGuid().ToString("N")
                    backgroundProcesses.[id] <- (proc, stdoutBuffer, stderrBuffer)

                    Ok {
                        Id = id
                        Command = cmdStr
                        StartedAtUtc = DateTime.UtcNow
                    }
        with ex ->
            Error (sprintf "Failed to start background command: %s" ex.Message)

    let getBackgroundCommandOutput (id: string) (maxOutputBytes: int) : string =
        match backgroundProcesses.TryGetValue(id) with
        | true, (proc, stdoutBuffer, stderrBuffer) ->
            let stdout = lock stdoutBuffer (fun () -> stdoutBuffer.ToString())
            let stderr = lock stderrBuffer (fun () -> stderrBuffer.ToString())
            let status = if proc.HasExited then sprintf "completed (exit code: %d)" proc.ExitCode else "running"
            let combined = combineOutput stdout stderr
            let content = sprintf "Background command %s status: %s\n\n%s" id status combined
            ToolSecurity.truncateOutput maxOutputBytes content
        | false, _ -> sprintf "Error: Background command '%s' not found." id

    let getBackgroundCommandOutputDefault (id: string) : string =
        getBackgroundCommandOutput id defaultMaxOutputBytes

    let stopBackgroundCommand (id: string) : string =
        match backgroundProcesses.TryRemove(id) with
        | true, (proc, _, _) ->
            try
                if not proc.HasExited then
                    proc.Kill(true)
                proc.Dispose()
                sprintf "Background command '%s' stopped." id
            with ex ->
                sprintf "Error stopping background command '%s': %s" id ex.Message
        | false, _ ->
            sprintf "Error: Background command '%s' not found." id

    let cleanupCompletedBackgroundCommands () =
        for kvp in backgroundProcesses do
            let id = kvp.Key
            let proc, _, _ = kvp.Value
            if proc.HasExited then
                match backgroundProcesses.TryRemove(id) with
                | true, (completedProc, _, _) -> completedProc.Dispose()
                | false, _ -> ()
