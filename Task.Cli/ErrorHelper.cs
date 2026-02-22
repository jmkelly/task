using Spectre.Console;

namespace TaskApp
{
    public static class ErrorHelper
    {
        public static void ShowError(string message, string? suggestion = null, string? helpCommand = null)
        {
            var fullMessage = $"Error: {message}";
            
            if (!string.IsNullOrEmpty(suggestion))
            {
                fullMessage += $" Try: {suggestion}";
            }
            
            if (!string.IsNullOrEmpty(helpCommand))
            {
                fullMessage += $" See: {helpCommand}";
            }
            
            Console.Error.WriteLine(fullMessage);
        }

        public static bool ValidatePriority(string? priority, out string? errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(priority))
            {
                return true;
            }

            var validPriorities = new[] { "low", "medium", "high" };
            if (!validPriorities.Contains(priority.ToLower()))
            {
                errorMessage = $"'{priority}' is not a valid priority (must be: low, medium, high). See: task add --help";
                return false;
            }
            return true;
        }

        public static bool ValidateStatus(string? status, out string? errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(status))
            {
                return true;
            }

            var validStatuses = new[] { "todo", "done", "in_progress" };
            if (!validStatuses.Contains(status.ToLower()))
            {
                errorMessage = $"'{status}' is not a valid status (must be: todo, done, in_progress). See: task add --help";
                return false;
            }
            return true;
        }

        public static bool ValidateDate(string? dateStr, out string? errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(dateStr))
            {
                return true;
            }

            if (!DateTime.TryParse(dateStr, out _))
            {
                errorMessage = $"'{dateStr}' is not a valid date. Use YYYY-MM-DD format. See: task add --help";
                return false;
            }
            return true;
        }
    }
}
