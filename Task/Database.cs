using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace TaskApp
{
    public enum UndoActionType
    {
        Complete,
        Delete,
        Create,
        Edit
    }

    public class UndoAction
    {
        public UndoActionType Type { get; set; }
        public string Uid { get; set; } = "";
        public TaskItem? PreviousState { get; set; }
    }

    public class Database : ITaskService
    {
        private readonly string _dbPath;
        private readonly List<UndoAction> _undoStack = new();

        public Database(string dbPath)
        {
            _dbPath = dbPath;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await InitializeDatabaseAsync(cancellationToken);
        }

        public void RecordAction(UndoAction action)
        {
            _undoStack.Add(action);
        }

        public void ClearUndoStack()
        {
            _undoStack.Clear();
        }

        public List<UndoAction> GetUndoStack()
        {
            return _undoStack.ToList();
        }

        public async Task<bool> UndoLastActionAsync(CancellationToken cancellationToken = default)
        {
            if (_undoStack.Count == 0)
            {
                return false;
            }

            var lastAction = _undoStack[^1];
            _undoStack.RemoveAt(_undoStack.Count - 1);

            switch (lastAction.Type)
            {
                case UndoActionType.Complete:
                    var taskToIncomplete = await GetTaskByUidAsync(lastAction.Uid, cancellationToken);
                    if (taskToIncomplete != null)
                    {
                        taskToIncomplete.Status = "todo";
                        await UpdateTaskAsync(taskToIncomplete, cancellationToken);
                    }
                    break;

                case UndoActionType.Delete:
                    if (lastAction.PreviousState != null)
                    {
                        await RestoreTaskAsync(lastAction.PreviousState, cancellationToken);
                    }
                    break;

                case UndoActionType.Create:
                    await DeleteTaskAsync(lastAction.Uid, cancellationToken);
                    break;

                case UndoActionType.Edit:
                    if (lastAction.PreviousState != null)
                    {
                        await UpdateTaskAsync(lastAction.PreviousState, cancellationToken);
                    }
                    break;
            }

            return true;
        }

        private async Task RestoreTaskAsync(TaskItem task, CancellationToken cancellationToken)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);
            using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                var sql = @"
INSERT INTO tasks (uid, title, description, priority, due_date, tags, project, depends_on, status, created_at, updated_at)
VALUES (@uid, @title, @description, @priority, @dueDate, @tags, @project, @dependsOn, @status, @createdAt, @updatedAt)";

                using var command = new SqliteCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@uid", task.Uid);
                command.Parameters.AddWithValue("@title", task.Title);
                command.Parameters.AddWithValue("@description", task.Description ?? "");
                command.Parameters.AddWithValue("@priority", task.Priority);
                command.Parameters.AddWithValue("@dueDate", task.DueDate?.ToString("yyyy-MM-dd") ?? "");
                command.Parameters.AddWithValue("@tags", task.TagsString);
                command.Parameters.AddWithValue("@project", task.Project ?? "");
                command.Parameters.AddWithValue("@dependsOn", task.DependsOnString);
                command.Parameters.AddWithValue("@status", task.Status);
                command.Parameters.AddWithValue("@createdAt", task.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@updatedAt", task.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                await command.ExecuteNonQueryAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        private async Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);
            
            using var pragmaCmd = new SqliteCommand("PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA cache_size=-64000; PRAGMA temp_store=MEMORY; PRAGMA mmap_size=268435456;", connection);
            await pragmaCmd.ExecuteNonQueryAsync(cancellationToken);
            
            var sql = @"
CREATE TABLE IF NOT EXISTS tasks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    uid TEXT UNIQUE NOT NULL,
    title TEXT NOT NULL,
    description TEXT,
    priority TEXT NOT NULL,
    due_date TEXT,
    tags TEXT,
    project TEXT,
    depends_on TEXT,
    status TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);
CREATE VIRTUAL TABLE IF NOT EXISTS tasks_fts USING fts5(title, description, tags, project, content='', contentless_delete=1);
CREATE TRIGGER IF NOT EXISTS tasks_fts_insert AFTER INSERT ON tasks
BEGIN
    INSERT INTO tasks_fts(rowid, title, description, tags, project) VALUES (new.id, new.title, new.description, new.tags, new.project);
END;
CREATE TRIGGER IF NOT EXISTS tasks_fts_delete AFTER DELETE ON tasks
BEGIN
    DELETE FROM tasks_fts WHERE rowid = old.id;
END;
CREATE TRIGGER IF NOT EXISTS tasks_fts_update AFTER UPDATE ON tasks
BEGIN
    UPDATE tasks_fts SET title = new.title, description = new.description, tags = new.tags, project = new.project WHERE rowid = new.id;
END;
";
            using var command = new SqliteCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
            
            // Migration: Add columns if they don't exist (for backward compatibility)
            await MigrateIfNeededAsync(connection, cancellationToken);
        }
        
        private async Task MigrateIfNeededAsync(SqliteConnection connection, CancellationToken cancellationToken)
        {
            // Check if project column exists
            var checkProjectSql = "SELECT COUNT(*) FROM pragma_table_info('tasks') WHERE name = 'project'";
            using var checkCmd = new SqliteCommand(checkProjectSql, connection);
            var projectExists = (long)(await checkCmd.ExecuteScalarAsync(cancellationToken) ?? 0) > 0;
            
            if (!projectExists)
            {
                var alterSql = @"
ALTER TABLE tasks ADD COLUMN project TEXT;
ALTER TABLE tasks ADD COLUMN depends_on TEXT;
";
                using var alterCmd = new SqliteCommand(alterSql, connection);
                await alterCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        public async System.Threading.Tasks.Task DeleteTaskAsync(string uid, CancellationToken cancellationToken = default)
        {
            var task = await GetTaskByUidAsync(uid, cancellationToken);
            if (task == null)
            {
                throw new InvalidOperationException($"Task with UID {uid} not found");
            }

            var previousState = new TaskItem
            {
                Id = task.Id,
                Uid = task.Uid,
                Title = task.Title,
                Description = task.Description,
                Priority = task.Priority,
                DueDate = task.DueDate,
                Tags = task.Tags,
                Project = task.Project,
                DependsOn = task.DependsOn,
                Status = task.Status,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt
            };

            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);
            using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                var sql = "DELETE FROM tasks WHERE id = @id";
                using var command = new SqliteCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@id", task.Id);
                await command.ExecuteNonQueryAsync(cancellationToken);

                // Delete from FTS
                command.CommandText = "DELETE FROM tasks_fts WHERE rowid = @id";
                await command.ExecuteNonQueryAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                ClearUndoStack();
                RecordAction(new UndoAction { Type = UndoActionType.Delete, Uid = uid, PreviousState = previousState });
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async System.Threading.Tasks.Task CompleteTaskAsync(string uid, CancellationToken cancellationToken = default)
        {
            var task = await GetTaskByUidAsync(uid, cancellationToken);
            if (task == null)
            {
                throw new InvalidOperationException($"Task with UID {uid} not found");
            }

            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);

            var sql = "UPDATE tasks SET status = 'done', updated_at = @updatedAt WHERE id = @id";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@id", task.Id);
            command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            await command.ExecuteNonQueryAsync(cancellationToken);

            ClearUndoStack();
            RecordAction(new UndoAction { Type = UndoActionType.Complete, Uid = uid });
        }

        public async System.Threading.Tasks.Task<List<TaskItem>> SearchTasksAsync(string query, string type = "fts", CancellationToken cancellationToken = default)
        {
            switch (type.ToLower())
            {
                case "semantic":
                    return await SearchTasksSemanticAsync(query, cancellationToken);
                case "hybrid":
                    return await SearchTasksHybridAsync(query, cancellationToken);
                case "fts":
                default:
                    return await SearchTasksFTSAsync(query, cancellationToken);
            }
        }

        public async System.Threading.Tasks.Task<List<TaskItem>> SearchTasksFTSAsync(string query, CancellationToken cancellationToken = default)
        {
            var tasks = new List<TaskItem>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);

            // Use FTS5 MATCH
            var sql = @"
SELECT t.id, t.uid, t.title, t.description, t.priority, t.due_date, t.tags, t.project, t.depends_on, t.status, t.created_at, t.updated_at
FROM tasks_fts fts
JOIN tasks t ON t.id = fts.rowid
WHERE tasks_fts MATCH @query
ORDER BY rank";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@query", query);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var task = new TaskItem
                {
                    Id = reader.GetInt32(0),
                    Uid = reader.GetString(1),
                    Title = reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Priority = reader.GetString(4),
                    DueDate = reader.IsDBNull(5) || string.IsNullOrEmpty(reader.GetString(5)) ? null : DateTime.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                    Tags = reader.IsDBNull(6) ? new List<string>() : reader.GetString(6).Split(',').Where(t => !string.IsNullOrEmpty(t)).ToList(),
                    Project = reader.IsDBNull(7) || string.IsNullOrEmpty(reader.GetString(7)) ? null : reader.GetString(7),
                    DependsOn = reader.IsDBNull(8) || string.IsNullOrEmpty(reader.GetString(8)) ? new List<string>() : reader.GetString(8).Split(',').Where(t => !string.IsNullOrEmpty(t)).ToList(),
                    Status = reader.GetString(9),
                    CreatedAt = DateTime.Parse(reader.GetString(10), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                    UpdatedAt = DateTime.Parse(reader.GetString(11), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
                };
                tasks.Add(task);
            }

            return tasks;
        }

        public async System.Threading.Tasks.Task<List<TaskItem>> SearchTasksSemanticAsync(string query, CancellationToken cancellationToken = default)
        {
            // Temporarily return empty for testing
            return new List<TaskItem>();
            // var tasks = new List<TaskItem>();
            // using var connection = new SqliteConnection($"Data Source={_dbPath}");
            // connection.Open();

            // // Load extensions if not already loaded (in case)
            // connection.EnableExtensions();
            // var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            // try
            // {
            //     connection.LoadExtension(Path.Combine(exeDir, "vector0"));
            //     connection.LoadExtension(Path.Combine(exeDir, "vss0"));
            // }
            // catch { } // Ignore if already loaded

            // // Generate embedding for query
            // var queryText = query; // Use query directly as text
            // var queryEmbedding = Embeddings.GenerateEmbedding(queryText);
            // var embeddingJson = "[" + string.Join(",", queryEmbedding) + "]";

            // var sql = @"
            // SELECT t.id, t.uid, t.title, t.description, t.priority, t.due_date, t.tags, t.status, t.created_at, t.updated_at
            // FROM vss_tasks vss
            // JOIN tasks t ON t.id = vss.rowid
            // WHERE vss_search(embedding, @embedding)
            // ORDER BY distance LIMIT 50";

            // using var command = new SqliteCommand(sql, connection);
            // command.Parameters.AddWithValue("@embedding", embeddingJson);
            // using var reader = command.ExecuteReader();

            // while (reader.Read())
            // {
            //     var task = new TaskItem
            //     {
            //         Id = reader.GetInt32(0),
            //         Uid = reader.GetString(1),
            //         Title = reader.GetString(2),
            //         Description = reader.IsDBNull(3) ? null : reader.GetString(3),
            //         Priority = reader.GetString(4),
            //         DueDate = reader.IsDBNull(5) || string.IsNullOrEmpty(reader.GetString(5)) ? null : DateTime.Parse(reader.GetString(5)),
            //         Tags = reader.IsDBNull(6) ? new List<string>() : reader.GetString(6).Split(',').Where(t => !string.IsNullOrEmpty(t)).ToList(),
            //         Status = reader.GetString(7),
            //         CreatedAt = DateTime.Parse(reader.GetString(8)),
            //         UpdatedAt = DateTime.Parse(reader.GetString(9))
            //     };
            //     tasks.Add(task);
            // }

            // return tasks;
        }

        public async System.Threading.Tasks.Task<List<TaskItem>> SearchTasksHybridAsync(string query, CancellationToken cancellationToken = default)
        {
            var ftsTasks = await SearchTasksFTSAsync(query, cancellationToken);
            var semanticTasks = await SearchTasksSemanticAsync(query, cancellationToken);

            // Combine results, remove duplicates by id, prioritize FTS results first
            var combined = new Dictionary<int, TaskItem>();
            foreach (var task in ftsTasks)
            {
                combined[task.Id] = task;
            }
            foreach (var task in semanticTasks)
            {
                if (!combined.ContainsKey(task.Id))
                {
                    combined[task.Id] = task;
                }
            }

            return combined.Values.ToList();
        }

        public async System.Threading.Tasks.Task<List<string>> GetAllUniqueTagsAsync(CancellationToken cancellationToken = default)
        {
            var tags = new HashSet<string>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);

            var sql = "SELECT tags FROM tasks WHERE tags IS NOT NULL AND tags != ''";
            using var command = new SqliteCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var tagString = reader.GetString(0);
                if (!string.IsNullOrEmpty(tagString))
                {
                    foreach (var tag in tagString.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)))
                    {
                        tags.Add(tag);
                    }
                }
            }

            return tags.OrderBy(t => t).ToList();
        }

        private async Task<string> GenerateUidAsync(CancellationToken cancellationToken)
        {
            const string chars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
            var random = new Random();
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var uid = new string(Enumerable.Range(0, 6)
                    .Select(_ => chars[random.Next(chars.Length)])
                    .ToArray());
                if (!await UidExistsAsync(uid, cancellationToken))
                {
                    return uid;
                }
            }
            return Guid.NewGuid().ToString().Substring(0, 8);
        }

        private async Task<bool> UidExistsAsync(string uid, CancellationToken cancellationToken)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);
            var sql = "SELECT COUNT(*) FROM tasks WHERE uid = @uid";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@uid", uid);
            var count = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
            return count > 0;
        }

        public async System.Threading.Tasks.Task<TaskItem> AddTaskAsync(string title, string? description, string priority, DateTime? dueDate, List<string> tags, string? project = null, List<string>? dependsOn = null, string? status = "todo", CancellationToken cancellationToken = default)
        {
            return await AddTaskAsyncREAL(title, description, priority, dueDate, tags, project, dependsOn, status, cancellationToken);
        }
        
        public async System.Threading.Tasks.Task<TaskItem> AddTaskAsyncREAL(string title, string? description, string priority, DateTime? dueDate, List<string> tags, string? project = null, List<string>? dependsOn = null, string? status = "todo", CancellationToken cancellationToken = default)
        {
            System.IO.File.AppendAllText("/tmp/debug.txt", "AddTaskAsyncREAL called project=" + project + "\n");
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);
            using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                var uid = await GenerateUidAsync(cancellationToken);
                var createdAt = DateTime.UtcNow;
                var updatedAt = createdAt;
                var taskStatus = !string.IsNullOrEmpty(status) && new[] { "todo", "done", "in_progress" }.Contains(status.ToLower()) 
                    ? status.ToLower() 
                    : "todo";
                var dependsOnList = dependsOn ?? new List<string>();
                var sql = @"
INSERT INTO tasks (uid, title, description, priority, due_date, tags, project, depends_on, status, created_at, updated_at)
VALUES (@uid, @title, @description, @priority, @dueDate, @tags, @project, @dependsOn, @status, @createdAt, @updatedAt)";

                using var command = new SqliteCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@uid", uid);
                command.Parameters.AddWithValue("@title", title);
                command.Parameters.AddWithValue("@description", description ?? "");
                command.Parameters.AddWithValue("@priority", priority);
                command.Parameters.AddWithValue("@dueDate", dueDate?.ToString("yyyy-MM-dd") ?? "");
                command.Parameters.AddWithValue("@tags", string.Join(",", tags));
                command.Parameters.AddWithValue("@project", project ?? "");
                command.Parameters.AddWithValue("@dependsOn", string.Join(",", dependsOnList));
                command.Parameters.AddWithValue("@status", taskStatus);
                command.Parameters.AddWithValue("@createdAt", createdAt.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@updatedAt", updatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                await command.ExecuteNonQueryAsync(cancellationToken);

                var sqlId = "SELECT last_insert_rowid()";
                using var commandId = new SqliteCommand(sqlId, connection, transaction);
                var result = await commandId.ExecuteScalarAsync(cancellationToken);
                var id = (long)(result ?? 0);

                await transaction.CommitAsync(cancellationToken);

                var newTask = new TaskItem
                {
                    Id = (int)id,
                    Uid = uid,
                    Title = title,
                    Description = description,
                    Priority = priority,
                    DueDate = dueDate,
                    Tags = tags,
                    Project = project,
                    DependsOn = dependsOnList,
                    Status = taskStatus,
                    CreatedAt = createdAt,
                    UpdatedAt = updatedAt
                };
                
                Console.Error.WriteLine($"DEBUG AddTaskAsync: Before return, newTask.Project='{newTask.Project}'");

                ClearUndoStack();
                RecordAction(new UndoAction { Type = UndoActionType.Create, Uid = uid });

                return newTask;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async System.Threading.Tasks.Task<TaskItem?> GetTaskByUidAsync(string uid, CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);
            var sql = "SELECT id, uid, title, description, priority, due_date, tags, project, depends_on, status, created_at, updated_at FROM tasks WHERE uid = @uid";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@uid", uid);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new TaskItem
                {
                    Id = reader.GetInt32(0),
                    Uid = reader.GetString(1),
                    Title = reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Priority = reader.GetString(4),
                    DueDate = reader.IsDBNull(5) || string.IsNullOrEmpty(reader.GetString(5)) ? null : DateTime.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                    Tags = reader.IsDBNull(6) ? new List<string>() : reader.GetString(6).Split(',').Where(t => !string.IsNullOrEmpty(t)).ToList(),
                    Project = reader.IsDBNull(7) || string.IsNullOrEmpty(reader.GetString(7)) ? null : reader.GetString(7),
                    DependsOn = reader.IsDBNull(8) || string.IsNullOrEmpty(reader.GetString(8)) ? new List<string>() : reader.GetString(8).Split(',').Where(t => !string.IsNullOrEmpty(t)).ToList(),
                    Status = reader.GetString(9),
                    CreatedAt = DateTime.Parse(reader.GetString(10), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                    UpdatedAt = DateTime.Parse(reader.GetString(11), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
                };
            }
            return null;
        }

        public async System.Threading.Tasks.Task UpdateTaskAsync(TaskItem task, CancellationToken cancellationToken = default)
        {
            var previousTask = await GetTaskByUidAsync(task.Uid, cancellationToken);
            TaskItem? previousState = null;
            if (previousTask != null)
            {
                previousState = new TaskItem
                {
                    Id = previousTask.Id,
                    Uid = previousTask.Uid,
                    Title = previousTask.Title,
                    Description = previousTask.Description,
                    Priority = previousTask.Priority,
                    DueDate = previousTask.DueDate,
                    Tags = previousTask.Tags,
                    Project = previousTask.Project,
                    DependsOn = previousTask.DependsOn,
                    Status = previousTask.Status,
                    CreatedAt = previousTask.CreatedAt,
                    UpdatedAt = previousTask.UpdatedAt
                };
            }

            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);
            using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                var sql = @"
UPDATE tasks SET title = @title, description = @description, priority = @priority,
                  due_date = @dueDate, tags = @tags, project = @project, depends_on = @dependsOn,
                  status = @status, updated_at = @updatedAt
WHERE id = @id";

                using var command = new SqliteCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@id", task.Id);
                command.Parameters.AddWithValue("@title", task.Title);
                command.Parameters.AddWithValue("@description", task.Description ?? "");
                command.Parameters.AddWithValue("@priority", task.Priority);
                command.Parameters.AddWithValue("@dueDate", task.DueDate?.ToString("yyyy-MM-dd") ?? "");
                command.Parameters.AddWithValue("@tags", task.TagsString);
                command.Parameters.AddWithValue("@project", task.Project ?? "");
                command.Parameters.AddWithValue("@dependsOn", task.DependsOnString);
                command.Parameters.AddWithValue("@status", task.Status);
                command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                await command.ExecuteNonQueryAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                if (previousState != null)
                {
                    ClearUndoStack();
                    RecordAction(new UndoAction { Type = UndoActionType.Edit, Uid = task.Uid, PreviousState = previousState });
                }
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async System.Threading.Tasks.Task<List<TaskItem>> GetAllTasksAsync(
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
            CancellationToken cancellationToken = default)
        {
            var tasks = new List<TaskItem>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);

            // Build dynamic query
            var whereClauses = new List<string>();
            if (!string.IsNullOrEmpty(status)) whereClauses.Add("status = @status");
            if (!string.IsNullOrEmpty(priority)) whereClauses.Add("priority = @priority");
            if (!string.IsNullOrEmpty(project)) whereClauses.Add("project = @project");
            if (!string.IsNullOrEmpty(tags)) whereClauses.Add("tags LIKE @tags");
            if (dueBefore.HasValue) whereClauses.Add("due_date <= @dueBefore");
            if (dueAfter.HasValue) whereClauses.Add("due_date >= @dueAfter");

            var whereClause = whereClauses.Any() ? "WHERE " + string.Join(" AND ", whereClauses) : "";
            var orderByClause = "";
            if (!string.IsNullOrEmpty(sortBy))
            {
                var column = sortBy.ToLower() switch
                {
                    "priority" => "priority",
                    "duedate" => "due_date",
                    "createdat" => "created_at",
                    "title" => "title",
                    _ => "created_at"
                };
                var order = sortOrder?.ToLower() == "desc" ? "DESC" : "ASC";
                orderByClause = $"ORDER BY {column} {order}";
            }

            var sql = $"SELECT id, uid, title, description, priority, due_date, tags, project, depends_on, status, created_at, updated_at FROM tasks {whereClause} {orderByClause}";
            using var command = new SqliteCommand(sql, connection);

            if (!string.IsNullOrEmpty(status)) command.Parameters.AddWithValue("@status", status);
            if (!string.IsNullOrEmpty(priority)) command.Parameters.AddWithValue("@priority", priority);
            if (!string.IsNullOrEmpty(project)) command.Parameters.AddWithValue("@project", project);
            if (!string.IsNullOrEmpty(tags)) command.Parameters.AddWithValue("@tags", $"%{tags}%");
            if (dueBefore.HasValue) command.Parameters.AddWithValue("@dueBefore", dueBefore.Value.ToString("yyyy-MM-dd"));
            if (dueAfter.HasValue) command.Parameters.AddWithValue("@dueAfter", dueAfter.Value.ToString("yyyy-MM-dd"));

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var task = new TaskItem
                {
                    Id = reader.GetInt32(0),
                    Uid = reader.GetString(1),
                    Title = reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Priority = reader.GetString(4),
                    DueDate = reader.IsDBNull(5) || string.IsNullOrEmpty(reader.GetString(5)) ? null : DateTime.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                    Tags = reader.IsDBNull(6) ? new List<string>() : reader.GetString(6).Split(',').Where(t => !string.IsNullOrEmpty(t)).ToList(),
                    Project = reader.IsDBNull(7) || string.IsNullOrEmpty(reader.GetString(7)) ? null : reader.GetString(7),
                    DependsOn = reader.IsDBNull(8) || string.IsNullOrEmpty(reader.GetString(8)) ? new List<string>() : reader.GetString(8).Split(',').Where(t => !string.IsNullOrEmpty(t)).ToList(),
                    Status = reader.GetString(9),
                    CreatedAt = DateTime.Parse(reader.GetString(10), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                    UpdatedAt = DateTime.Parse(reader.GetString(11), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
                };
                tasks.Add(task);
            }

            // Apply pagination after fetching (simple implementation)
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
        
        public async System.Threading.Tasks.Task<List<string>> GetAllUniqueProjectsAsync(CancellationToken cancellationToken = default)
        {
            var projects = new HashSet<string>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);

            var sql = "SELECT project FROM tasks WHERE project IS NOT NULL AND project != ''";
            using var command = new SqliteCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var project = reader.GetString(0);
                if (!string.IsNullOrEmpty(project))
                {
                    projects.Add(project);
                }
            }

            return projects.OrderBy(p => p).ToList();
        }
        
        public async System.Threading.Tasks.Task<List<TaskItem>> GetTasksDependingOnAsync(string uid, CancellationToken cancellationToken = default)
        {
            var tasks = new List<TaskItem>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);

            var sql = "SELECT id, uid, title, description, priority, due_date, tags, project, depends_on, status, created_at, updated_at FROM tasks WHERE depends_on LIKE @uid";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@uid", $"%{uid}%");
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var task = new TaskItem
                {
                    Id = reader.GetInt32(0),
                    Uid = reader.GetString(1),
                    Title = reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Priority = reader.GetString(4),
                    DueDate = reader.IsDBNull(5) || string.IsNullOrEmpty(reader.GetString(5)) ? null : DateTime.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                    Tags = reader.IsDBNull(6) ? new List<string>() : reader.GetString(6).Split(',').Where(t => !string.IsNullOrEmpty(t)).ToList(),
                    Project = reader.IsDBNull(7) || string.IsNullOrEmpty(reader.GetString(7)) ? null : reader.GetString(7),
                    DependsOn = reader.IsDBNull(8) || string.IsNullOrEmpty(reader.GetString(8)) ? new List<string>() : reader.GetString(8).Split(',').Where(t => !string.IsNullOrEmpty(t)).ToList(),
                    Status = reader.GetString(9),
                    CreatedAt = DateTime.Parse(reader.GetString(10), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                    UpdatedAt = DateTime.Parse(reader.GetString(11), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
                };
                if (task.DependsOn.Contains(uid))
                {
                    tasks.Add(task);
                }
            }

            return tasks;
        }
        
        public async System.Threading.Tasks.Task<bool> ValidateDependenciesAsync(string uid, List<string> dependsOn, CancellationToken cancellationToken = default)
        {
            if (dependsOn == null || dependsOn.Count == 0)
            {
                return true;
            }

            // Check for self-reference
            if (dependsOn.Contains(uid))
            {
                return false;
            }

            // Check if all dependencies exist and detect circular dependencies
            var visited = new HashSet<string> { uid };
            return !await HasCircularDependencyAsync(uid, dependsOn, visited, cancellationToken);
        }
        
        private async Task<bool> HasCircularDependencyAsync(string taskUid, List<string> dependsOn, HashSet<string> visited, CancellationToken cancellationToken)
        {
            foreach (var depUid in dependsOn)
            {
                if (visited.Contains(depUid))
                {
                    return true;
                }

                visited.Add(depUid);
                var depTask = await GetTaskByUidAsync(depUid, cancellationToken);
                if (depTask != null && depTask.DependsOn.Count > 0)
                {
                    if (await HasCircularDependencyAsync(taskUid, depTask.DependsOn, new HashSet<string>(visited), cancellationToken))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}