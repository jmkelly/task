using Task.Core;
using System.Threading.Tasks;

namespace Task.Core
{
    public interface ITaskService
    {
        System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task<List<TaskItem>> GetAllTasksAsync(
            string? status = null,
            string? priority = null,
            string? project = null,
            string? assignee = null,
            string? tags = null,
            DateTime? dueBefore = null,
            DateTime? dueAfter = null,
            int? limit = null,
            int? offset = null,
            string? sortBy = null,
            string? sortOrder = null,
            string? userId = null,
            CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task<TaskItem?> GetTaskByUidAsync(string uid, string? userId = null, CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task<TaskItem> AddTaskAsync(string uid, string title, string? description, string priority, DateTime? dueDate, List<string> tags, string? project = null, List<string>? dependsOn = null, string? assignee = null, string status = "todo", string? blockReason = null, string? userId = null, CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task UpdateTaskAsync(TaskItem task, string? userId = null, CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task DeleteTaskAsync(string uid, string? userId = null, CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task CompleteTaskAsync(string uid, string? userId = null, CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task<List<TaskItem>> SearchTasksAsync(string query, string type = "fts", string? userId = null, CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task<List<string>> GetAllUniqueTagsAsync(string? userId = null, CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task<List<string>> GetAllUniqueProjectsAsync(string? userId = null, CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task<List<string>> GetAllUniqueAssigneesAsync(string? userId = null, CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task<List<TaskItem>> GetTasksDependingOnAsync(string uid, string? userId = null, CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task<bool> ValidateDependenciesAsync(string uid, List<string> dependsOn, string? userId = null, CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task ArchiveAllTasksAsync(string? userId = null, CancellationToken cancellationToken = default);
    }
}
