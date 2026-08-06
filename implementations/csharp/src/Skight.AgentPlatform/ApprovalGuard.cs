using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Skight.AgentPlatform
{
    public class ApprovalRequest
    {
        public string Action { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
    }

    public static class ApprovalGuard
    {
        private static readonly Regex RiskyCommandPattern = new(@"\b(rm\s+-rf|del\s+/f|format\b|shutdown\b|reboot\b|mkfs\b|diskpart\b|sudo\b)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RiskyEditPattern = new(@"(?i)(password|secret|token|private[_-]?key|connectionstring)", RegexOptions.Compiled);

        public static bool IsHighRiskCommand(string command)
        {
            return !string.IsNullOrWhiteSpace(command) && RiskyCommandPattern.IsMatch(command);
        }

        public static bool IsHighRiskFileEdit(string path, string contentPreview)
        {
            var sensitiveFile = !string.IsNullOrWhiteSpace(path) && 
                (path.EndsWith(".env", StringComparison.OrdinalIgnoreCase) || 
                 path.EndsWith("secrets.json", StringComparison.OrdinalIgnoreCase) || 
                 path.Contains("credential", StringComparison.OrdinalIgnoreCase));

            return sensitiveFile || (!string.IsNullOrWhiteSpace(contentPreview) && RiskyEditPattern.IsMatch(contentPreview));
        }

        public static async Task<bool> RequestConsoleApprovalAsync(ApprovalRequest request)
        {
            Console.WriteLine($"\nApproval required for action: {request.Action}");
            Console.WriteLine($"Reason: {request.Reason}");
            Console.WriteLine($"Payload: {request.Payload}");
            Console.Write("Allow? (y/N): ");
            
            // In a real app this would read asynchronously, but for console this is synchronous mostly
            var input = await Task.Run(() => Console.ReadLine());
            return !string.IsNullOrEmpty(input) && input.Trim().Equals("y", StringComparison.OrdinalIgnoreCase);
        }

        public static async Task EnforceCommandApprovalAsync(string command, Func<ApprovalRequest, Task<bool>> prompter)
        {
            if (IsHighRiskCommand(command))
            {
                var request = new ApprovalRequest
                {
                    Action = "execute_command",
                    Reason = "Command classified as high-risk.",
                    Payload = command
                };
                if (!await prompter(request)) throw new UnauthorizedAccessException("User rejected action.");
            }
        }

        public static async Task EnforceFileEditApprovalAsync(string path, string contentPreview, Func<ApprovalRequest, Task<bool>> prompter)
        {
            if (IsHighRiskFileEdit(path, contentPreview))
            {
                var request = new ApprovalRequest
                {
                    Action = "edit_file",
                    Reason = "File edit classified as sensitive/high-risk.",
                    Payload = $"{path} | {contentPreview}"
                };
                if (!await prompter(request)) throw new UnauthorizedAccessException("User rejected action.");
            }
        }
    }
}
