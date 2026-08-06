using System;
using System.IO;
using System.Text;

namespace Skight.AgentPlatform
{
    public static class ToolSecurity
    {
        private static readonly StringComparison Comparison = 
            Environment.OSVersion.Platform == PlatformID.Win32NT ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        private static string NormalizeRootPath(string workspaceRoot)
        {
            var canonical = Path.GetFullPath(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return canonical + Path.DirectorySeparatorChar;
        }

        private static string CanonicalizeTargetPath(string workspaceRoot, string targetPath)
        {
            return Path.IsPathRooted(targetPath) 
                ? Path.GetFullPath(targetPath) 
                : Path.GetFullPath(Path.Combine(workspaceRoot, targetPath));
        }

        public static string ValidatePathInSandbox(string workspaceRoot, string targetPath)
        {
            if (string.IsNullOrWhiteSpace(workspaceRoot)) throw new ArgumentException("Workspace root cannot be empty.");
            if (string.IsNullOrWhiteSpace(targetPath)) throw new ArgumentException("Target path cannot be empty.");

            var rootWithSeparator = NormalizeRootPath(workspaceRoot);
            var targetCanonical = CanonicalizeTargetPath(workspaceRoot, targetPath);
            var targetWithSeparator = targetCanonical + Path.DirectorySeparatorChar;

            if (targetWithSeparator.StartsWith(rootWithSeparator, Comparison))
            {
                return targetCanonical;
            }
            
            throw new UnauthorizedAccessException($"Access denied: Path '{targetPath}' is outside workspace sandbox.");
        }

        public static string TruncateOutputWithLimits(int maxBytes, int maxLines, string output)
        {
            if (string.IsNullOrEmpty(output) || maxBytes <= 0 || maxLines <= 0) return output;

            var normalized = output.Replace("\r\n", "\n");
            var splitLines = normalized.Split('\n');
            
            string lineTrimmed;
            int lineTruncatedBy;
            
            if (splitLines.Length > maxLines)
            {
                var kept = new string[maxLines];
                Array.Copy(splitLines, kept, maxLines);
                lineTrimmed = string.Join("\n", kept);
                lineTruncatedBy = splitLines.Length - maxLines;
            }
            else
            {
                lineTrimmed = normalized;
                lineTruncatedBy = 0;
            }

            var withLineMarker = lineTruncatedBy > 0 
                ? $"{lineTrimmed}\n\n[Output truncated: {lineTruncatedBy} lines hidden to protect context window]" 
                : lineTrimmed;

            var currentBytes = Encoding.UTF8.GetByteCount(withLineMarker);
            if (currentBytes <= maxBytes) return withLineMarker;

            var marker = $"\n\n[Output truncated: exceeds {maxBytes} bytes safety limit]";
            var markerBytes = Encoding.UTF8.GetByteCount(marker);
            var allowedBytes = Math.Max(0, maxBytes - markerBytes);
            
            int low = 0, high = withLineMarker.Length;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                if (Encoding.UTF8.GetByteCount(withLineMarker.Substring(0, mid)) <= allowedBytes)
                    low = mid;
                else
                    high = mid - 1;
            }
            
            return withLineMarker.Substring(0, low) + marker;
        }

        public static string TruncateOutput(int maxBytes, string output) => TruncateOutputWithLimits(maxBytes, 500, output);
    }
}
