using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ST = global::System.Threading.Tasks;

namespace Task.Core
{
    public class TaskService : ITaskService
    {
        private readonly Database _database;

        public TaskService(string dbPath)
        {
            _database = new Database(dbPath);
        }

        public async ST.Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await _database.InitializeAsync(cancellationToken);
        }

        public async ST.Task<List<TaskItem>> GetAllTasksAsync(
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
            CancellationToken cancellationToken = default)
        {
            var tasks = await _database.GetAllTasksAsync(cancellationToken);

            // Apply filters
            if (!string.IsNullOrEmpty(status))
            {
                tasks = tasks.Where(t => t.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            if (!string.IsNullOrEmpty(priority))
            {
                tasks = tasks.Where(t => t.Priority.Equals(priority, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            if (!string.IsNullOrEmpty(project))
            {
                tasks = tasks.Where(t => t.Project?.Equals(project, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }
            if (!string.IsNullOrEmpty(assignee))
            {
                tasks = tasks.Where(t => t.Assignee?.Equals(assignee, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }
            if (!string.IsNullOrEmpty(tags))
            {
                var tagList = tags.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();
                tasks = tasks.Where(t => tagList.Any(tag => t.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))).ToList();
            }
            if (dueBefore.HasValue)
            {
                tasks = tasks.Where(t => t.DueDate.HasValue && t.DueDate.Value <= dueBefore.Value).ToList();
            }
            if (dueAfter.HasValue)
            {
                tasks = tasks.Where(t => t.DueDate.HasValue && t.DueDate.Value >= dueAfter.Value).ToList();
            }

            // Apply sorting
            if (!string.IsNullOrEmpty(sortBy))
            {
                tasks = sortBy.ToLower() switch
                {
                    "title" => sortOrder?.ToLower() == "desc" ? tasks.OrderByDescending(t => t.Title).ToList() : tasks.OrderBy(t => t.Title).ToList(),
                    "priority" => sortOrder?.ToLower() == "desc" ? tasks.OrderByDescending(t => t.Priority).ToList() : tasks.OrderBy(t => t.Priority).ToList(),
                    "due_date" => sortOrder?.ToLower() == "desc" ? tasks.OrderByDescending(t => t.DueDate).ToList() : tasks.OrderBy(t => t.DueDate).ToList(),
                    "created_at" => sortOrder?.ToLower() == "desc" ? tasks.OrderByDescending(t => t.CreatedAt).ToList() : tasks.OrderBy(t => t.CreatedAt).ToList(),
                    "updated_at" => sortOrder?.ToLower() == "desc" ? tasks.OrderByDescending(t => t.UpdatedAt).ToList() : tasks.OrderBy(t => t.UpdatedAt).ToList(),
                    _ => tasks
                };
            }

            // Apply limit and offset
            if (offset.HasValue)
            {
                tasks = tasks.Skip(offset.Value).ToList();
            }
            if (limit.HasValue)
            {
                tasks = tasks.Take(limit.Value).ToList();
            }

            return tasks;
        }

        public async ST.Task<TaskItem?> GetTaskByUidAsync(string uid, CancellationToken cancellationToken = default)
        {
            return await _database.GetTaskByUidAsync(uid, cancellationToken);
        }

        public async ST.Task<TaskItem> AddTaskAsync(string title, string? description, string priority, DateTime? dueDate, List<string> tags, string? project = null, List<string>? dependsOn = null, string? assignee = null, string status = "todo", CancellationToken cancellationToken = default)
        {
            return await _database.AddTaskAsync(title, description, priority, dueDate, tags, project, assignee, status, cancellationToken);
        }

        public async ST.Task UpdateTaskAsync(TaskItem task, CancellationToken cancellationToken = default)
        {
            await _database.UpdateTaskAsync(task, cancellationToken);
        }

        public async ST.Task DeleteTaskAsync(string uid, CancellationToken cancellationToken = default)
        {
            // Archive instead of hard delete
            var task = await _database.GetTaskByUidAsync(uid, cancellationToken);
            if (task == null) return;
            if (!task.Archived)
            {
                task.Archived = true;
                task.ArchivedAt = DateTime.UtcNow;
                await _database.UpdateTaskAsync(task, cancellationToken);
            }
        }

        public async ST.Task CompleteTaskAsync(string uid, CancellationToken cancellationToken = default)
        {
            await _database.CompleteTaskAsync(uid, cancellationToken);
        }

        public async ST.Task<List<TaskItem>> SearchTasksAsync(string query, string type = "fts", CancellationToken cancellationToken = default)
        {
            return await _database.SearchTasksAsync(query, cancellationToken);
        }

        public async ST.Task<List<string>> GetAllUniqueTagsAsync(CancellationToken cancellationToken = default)
        {
            return await _database.GetAllUniqueTagsAsync(cancellationToken);
        }

        public async ST.Task<List<string>> GetAllUniqueProjectsAsync(CancellationToken cancellationToken = default)
        {
            // Database doesn't have this method, implement it
            var tasks = await _database.GetAllTasksAsync(cancellationToken);
            var projects = tasks.Where(t => !string.IsNullOrEmpty(t.Project)).Select(t => t.Project!).Distinct().OrderBy(p => p).ToList();
            return projects;
        }

        public async ST.Task<List<string>> GetAllUniqueAssigneesAsync(CancellationToken cancellationToken = default)
        {
            // Database doesn't have this method, implement it
            var tasks = await _database.GetAllTasksAsync(cancellationToken);
            var assignees = tasks.Where(t => !string.IsNullOrEmpty(t.Assignee)).Select(t => t.Assignee!).Distinct().OrderBy(a => a).ToList();
            return assignees;
        }

        public async ST.Task<List<TaskItem>> GetTasksDependingOnAsync(string uid, CancellationToken cancellationToken = default)
        {
            // For now, return empty as dependencies are not implemented in DB
            return new List<TaskItem>();
        }

        public async ST.Task<bool> ValidateDependenciesAsync(string uid, List<string> dependsOn, CancellationToken cancellationToken = default)
        {
            // Simple validation: check if all dependsOn exist
            foreach (var dep in dependsOn)
            {
                var task = await _database.GetTaskByUidAsync(dep, cancellationToken);
                if (task == null)
                {
                    return false;
                }
            }
            return true;
        }
        public async ST.Task ArchiveAllTasksAsync(CancellationToken cancellationToken = default)
        {
            var tasks = await _database.GetAllTasksAsync(cancellationToken);
            var now = DateTime.UtcNow;
            foreach (var task in tasks)
            {
                if (!task.Archived)
                {
                    task.Archived = true;
                    task.ArchivedAt = now;
                    await _database.UpdateTaskAsync(task, cancellationToken);
                }
            }
        }
    }
}