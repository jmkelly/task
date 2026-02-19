using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace TaskApp
{
    public class Database
    {
        private readonly string _dbPath;

        public Database(string dbPath)
        {
            _dbPath = dbPath;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await InitializeDatabaseAsync(cancellationToken);
        }

        private async Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);
            var sql = @"
CREATE TABLE IF NOT EXISTS tasks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    uid TEXT UNIQUE NOT NULL,
    title TEXT NOT NULL,
    description TEXT,
    priority TEXT NOT NULL,
    due_date TEXT,
    tags TEXT,
    status TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);
CREATE VIRTUAL TABLE IF NOT EXISTS tasks_fts USING fts5(title, description, tags, content='', contentless_delete=1);
CREATE TRIGGER IF NOT EXISTS tasks_fts_insert AFTER INSERT ON tasks
BEGIN
    INSERT INTO tasks_fts(rowid, title, description, tags) VALUES (new.id, new.title, new.description, new.tags);
END;
CREATE TRIGGER IF NOT EXISTS tasks_fts_delete AFTER DELETE ON tasks
BEGIN
    DELETE FROM tasks_fts WHERE rowid = old.id;
END;
CREATE TRIGGER IF NOT EXISTS tasks_fts_update AFTER UPDATE ON tasks
BEGIN
    UPDATE tasks_fts SET title = new.title, description = new.description, tags = new.tags WHERE rowid = new.id;
END;
";
            using var command = new SqliteCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task DeleteTask(string id, CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);
            using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                var sql = "DELETE FROM tasks WHERE id = @id";
                using var command = new SqliteCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@id", id);
                await command.ExecuteNonQueryAsync(cancellationToken);

                // Delete from FTS
                command.CommandText = "DELETE FROM tasks_fts WHERE rowid = @id";
                await command.ExecuteNonQueryAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task CompleteTask(string id, CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);

            var sql = "UPDATE tasks SET status = 'completed', updated_at = @updatedAt WHERE id = @id";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<List<TaskItem>> SearchTasksFTS(string query, CancellationToken cancellationToken = default)
        {
            var tasks = new List<TaskItem>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);

            // Use FTS5 MATCH
            var sql = @"
SELECT t.id, t.uid, t.title, t.description, t.priority, t.due_date, t.tags, t.status, t.created_at, t.updated_at
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
                    Status = reader.GetString(7),
                    CreatedAt = DateTime.Parse(reader.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                    UpdatedAt = DateTime.Parse(reader.GetString(9), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
                };
                tasks.Add(task);
            }

            return tasks;
        }

        public async Task<List<TaskItem>> SearchTasksSemantic(string query, CancellationToken cancellationToken = default)
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

        public async Task<List<TaskItem>> SearchTasksHybrid(string query, CancellationToken cancellationToken = default)
        {
            var ftsTasks = await SearchTasksFTS(query, cancellationToken);
            var semanticTasks = await SearchTasksSemantic(query, cancellationToken);

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

        public async Task<List<string>> GetAllUniqueTags(CancellationToken cancellationToken = default)
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

        private string GenerateUid()
        {
            // Generate a unique 8-character ID
            return Guid.NewGuid().ToString().Substring(0, 8);
        }

        public async Task<TaskItem> AddTask(string title, string? description, string priority, DateTime? dueDate, List<string> tags, CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);
            using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                var uid = GenerateUid();
                var createdAt = DateTime.UtcNow;
                var updatedAt = createdAt;
                var sql = @"
INSERT INTO tasks (uid, title, description, priority, due_date, tags, status, created_at, updated_at)
VALUES (@uid, @title, @description, @priority, @dueDate, @tags, 'pending', @createdAt, @updatedAt)";

                using var command = new SqliteCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@uid", uid);
                command.Parameters.AddWithValue("@title", title);
                command.Parameters.AddWithValue("@description", description ?? "");
                command.Parameters.AddWithValue("@priority", priority);
                command.Parameters.AddWithValue("@dueDate", dueDate?.ToString("yyyy-MM-dd") ?? "");
                command.Parameters.AddWithValue("@tags", string.Join(",", tags));
                command.Parameters.AddWithValue("@createdAt", createdAt.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@updatedAt", updatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                await command.ExecuteNonQueryAsync(cancellationToken);

                var sqlId = "SELECT last_insert_rowid()";
                using var commandId = new SqliteCommand(sqlId, connection, transaction);
                var id = (long)await commandId.ExecuteScalarAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return new TaskItem
                {
                    Id = (int)id,
                    Uid = uid,
                    Title = title,
                    Description = description,
                    Priority = priority,
                    DueDate = dueDate,
                    Tags = tags,
                    Status = "pending",
                    CreatedAt = createdAt,
                    UpdatedAt = updatedAt
                };
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<TaskItem?> GetTaskByUid(string uid, CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);
            var sql = "SELECT id, uid, title, description, priority, due_date, tags, status, created_at, updated_at FROM tasks WHERE uid = @uid";
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
                    Status = reader.GetString(7),
                    CreatedAt = DateTime.Parse(reader.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                    UpdatedAt = DateTime.Parse(reader.GetString(9), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
                };
            }
            return null;
        }

        public async Task UpdateTask(TaskItem task, CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);
            using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                var sql = @"
UPDATE tasks SET title = @title, description = @description, priority = @priority,
                  due_date = @dueDate, tags = @tags, status = @status, updated_at = @updatedAt
WHERE id = @id";

                using var command = new SqliteCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@id", task.Id);
                command.Parameters.AddWithValue("@title", task.Title);
                command.Parameters.AddWithValue("@description", task.Description ?? "");
                command.Parameters.AddWithValue("@priority", task.Priority);
                command.Parameters.AddWithValue("@dueDate", task.DueDate?.ToString("yyyy-MM-dd") ?? "");
                command.Parameters.AddWithValue("@tags", task.TagsString);
                command.Parameters.AddWithValue("@status", task.Status);
                command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                await command.ExecuteNonQueryAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<List<TaskItem>> GetAllTasks(CancellationToken cancellationToken = default)
        {
            var tasks = new List<TaskItem>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);
            var sql = "SELECT id, uid, title, description, priority, due_date, tags, status, created_at, updated_at FROM tasks";
            using var command = new SqliteCommand(sql, connection);
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
                    Status = reader.GetString(7),
                    CreatedAt = DateTime.Parse(reader.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                    UpdatedAt = DateTime.Parse(reader.GetString(9), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
                };
                tasks.Add(task);
            }
            return tasks;
        }
    }
}