namespace TaskApp
{
    public interface ITaskService
    {
        Task InitializeAsync(CancellationToken cancellationToken = default);
        Task<List<TaskItem>> GetAllTasksAsync(
            string? status = null,
            string? priority = null,
            string? project = null,
            string? tags = null,
            DateTime? dueBefore = null,
            DateTime? dueAfter = null,
            int? limit = null,
            int? offset = null,
            string? sortBy = null,
            string? sortOrder = null,
            CancellationToken cancellationToken = default);
        Task<TaskItem?> GetTaskByUidAsync(string uid, CancellationToken cancellationToken = default);
        Task<TaskItem> AddTaskAsync(string title, string? description, string priority, DateTime? dueDate, List<string> tags, string? project = null, List<string>? dependsOn = null, string? status = "todo", CancellationToken cancellationToken = default);
        Task UpdateTaskAsync(TaskItem task, CancellationToken cancellationToken = default);
        Task DeleteTaskAsync(string uid, CancellationToken cancellationToken = default);
        Task CompleteTaskAsync(string uid, CancellationToken cancellationToken = default);
        Task<List<TaskItem>> SearchTasksAsync(string query, string type = "fts", CancellationToken cancellationToken = default);
        Task<List<string>> GetAllUniqueTagsAsync(CancellationToken cancellationToken = default);
        Task<List<string>> GetAllUniqueProjectsAsync(CancellationToken cancellationToken = default);
        Task<List<TaskItem>> GetTasksDependingOnAsync(string uid, CancellationToken cancellationToken = default);
        Task<bool> ValidateDependenciesAsync(string uid, List<string> dependsOn, CancellationToken cancellationToken = default);
        Task<bool> UndoLastActionAsync(CancellationToken cancellationToken = default);
        List<UndoAction> GetUndoStack();
    }
}
