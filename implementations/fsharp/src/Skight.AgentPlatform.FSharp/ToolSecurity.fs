namespace Skight.AgentPlatform.FSharp

open System
open System.IO
open System.Text

module ToolSecurity =
    let private comparison =
        if Environment.OSVersion.Platform = PlatformID.Win32NT then
            StringComparison.OrdinalIgnoreCase
        else
            StringComparison.Ordinal

    let private normalizeRootPath (workspaceRoot: string) =
        let canonical = Path.GetFullPath(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        canonical + string Path.DirectorySeparatorChar

    let private canonicalizeTargetPath (workspaceRoot: string) (targetPath: string) =
        if Path.IsPathRooted(targetPath) then
            Path.GetFullPath(targetPath)
        else
            Path.GetFullPath(Path.Combine(workspaceRoot, targetPath))

    /// Validates that path is inside the workspace sandbox.
    let validatePathInSandbox (workspaceRoot: string) (targetPath: string) : Result<string, string> =
        try
            if String.IsNullOrWhiteSpace(workspaceRoot) then
                Error "Workspace root cannot be empty."
            elif String.IsNullOrWhiteSpace(targetPath) then
                Error "Target path cannot be empty."
            else
                let rootWithSeparator = normalizeRootPath workspaceRoot
                let targetCanonical = canonicalizeTargetPath workspaceRoot targetPath
                let targetWithSeparator = targetCanonical + string Path.DirectorySeparatorChar

                if targetWithSeparator.StartsWith(rootWithSeparator, comparison) then
                    Ok targetCanonical
                else
                    Error (sprintf "Access denied: Path '%s' is outside workspace sandbox." targetPath)
        with ex ->
            Error (sprintf "Invalid path '%s': %s" targetPath ex.Message)

    /// Backward-compatible alias used by design drafts.
    let validateSandboxPath (workspaceRoot: string) (targetPath: string) : Result<string, string> =
        validatePathInSandbox workspaceRoot targetPath

    let truncateOutputWithLimits (maxBytes: int) (maxLines: int) (output: string) : string =
        if String.IsNullOrEmpty(output) || maxBytes <= 0 || maxLines <= 0 then
            output
        else
            let normalized = output.Replace("\r\n", "\n")
            let splitLines = normalized.Split('\n')

            let lineTrimmed, lineTruncatedBy =
                if splitLines.Length > maxLines then
                    let kept = splitLines |> Array.take maxLines |> String.concat "\n"
                    kept, splitLines.Length - maxLines
                else
                    normalized, 0

            let withLineMarker =
                if lineTruncatedBy > 0 then
                    sprintf "%s\n\n[Output truncated: %d lines hidden to protect context window]" lineTrimmed lineTruncatedBy
                else
                    lineTrimmed

            let currentBytes = Encoding.UTF8.GetByteCount(withLineMarker)
            if currentBytes <= maxBytes then
                withLineMarker
            else
                let marker = sprintf "\n\n[Output truncated: exceeds %d bytes safety limit]" maxBytes
                let markerBytes = Encoding.UTF8.GetByteCount(marker)
                let allowedBytes = max 0 (maxBytes - markerBytes)

                let rec findMaxChars low high =
                    if low >= high then low
                    else
                        let mid = (low + high + 1) / 2
                        let bytes = Encoding.UTF8.GetByteCount(withLineMarker.Substring(0, mid))
                        if bytes <= allowedBytes then findMaxChars mid high
                        else findMaxChars low (mid - 1)

                let length = findMaxChars 0 withLineMarker.Length
                withLineMarker.Substring(0, length) + marker

    /// Truncates large tool outputs by bytes and line count.
    let truncateOutput (maxBytes: int) (output: string) : string =
        truncateOutputWithLimits maxBytes 500 output
