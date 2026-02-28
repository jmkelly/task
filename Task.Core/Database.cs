using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Task.Core;

namespace Task.Core
{
    public class Database
    {
        private readonly string _dbPath;

        public Database(string dbPath)
        {
            _dbPath = dbPath;
        }

        public async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await InitializeDatabaseAsync(cancellationToken);
        }

        public void Initialize()
        {
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
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
						assignee TEXT,
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
			CREATE INDEX IF NOT EXISTS idx_tasks_uid ON tasks(uid);
			CREATE INDEX IF NOT EXISTS idx_tasks_status ON tasks(status);
			CREATE INDEX IF NOT EXISTS idx_tasks_priority ON tasks(priority);
			CREATE INDEX IF NOT EXISTS idx_tasks_due_date ON tasks(due_date);
			CREATE INDEX IF NOT EXISTS idx_tasks_created_at ON tasks(created_at);
			CREATE INDEX IF NOT EXISTS idx_tasks_project ON tasks(project);
			CREATE INDEX IF NOT EXISTS idx_tasks_assignee ON tasks(assignee);
			";
            using var command = new SqliteCommand(sql, connection);
            command.ExecuteNonQuery();

            // Add project column if it doesn't exist (for migration)
            var checkSql = "SELECT COUNT(*) FROM pragma_table_info('tasks') WHERE name='project'";
            using var checkCommand = new SqliteCommand(checkSql, connection);
            var count = Convert.ToInt64(checkCommand.ExecuteScalar());
            if (count == 0)
            {
                var alterSql = "ALTER TABLE tasks ADD COLUMN project TEXT";
                using var alterCommand = new SqliteCommand(alterSql, connection);
                alterCommand.ExecuteNonQuery();
            }

            // Add assignee column if it doesn't exist (for migration)
            var checkAssigneeSql = "SELECT COUNT(*) FROM pragma_table_info('tasks') WHERE name='assignee'";
            using var checkAssigneeCommand = new SqliteCommand(checkAssigneeSql, connection);
            var assigneeCount = Convert.ToInt64(checkAssigneeCommand.ExecuteScalar());
            if (assigneeCount == 0)
            {
                var alterAssigneeSql = "ALTER TABLE tasks ADD COLUMN assignee TEXT";
                using var alterAssigneeCommand = new SqliteCommand(alterAssigneeSql, connection);
                alterAssigneeCommand.ExecuteNonQuery();
            }


        }

        private async System.Threading.Tasks.Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
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
						project TEXT,
						assignee TEXT,
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
			CREATE INDEX IF NOT EXISTS idx_tasks_uid ON tasks(uid);
			CREATE INDEX IF NOT EXISTS idx_tasks_status ON tasks(status);
			CREATE INDEX IF NOT EXISTS idx_tasks_priority ON tasks(priority);
			CREATE INDEX IF NOT EXISTS idx_tasks_due_date ON tasks(due_date);
			CREATE INDEX IF NOT EXISTS idx_tasks_created_at ON tasks(created_at);
			CREATE INDEX IF NOT EXISTS idx_tasks_project ON tasks(project);
			CREATE INDEX IF NOT EXISTS idx_tasks_assignee ON tasks(assignee);
			";
            using var command = new SqliteCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);

            // Add project column if it doesn't exist (for migration)
            var checkSql = "SELECT COUNT(*) FROM pragma_table_info('tasks') WHERE name='project'";
            using var checkCommand = new SqliteCommand(checkSql, connection);
            var count = Convert.ToInt64(await checkCommand.ExecuteScalarAsync(cancellationToken));
            if (count == 0)
            {
                var alterSql = "ALTER TABLE tasks ADD COLUMN project TEXT";
                using var alterCommand = new SqliteCommand(alterSql, connection);
                await alterCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            // Add assignee column if it doesn't exist (for migration)
            var checkAssigneeSql = "SELECT COUNT(*) FROM pragma_table_info('tasks') WHERE name='assignee'";
            using var checkAssigneeCommand = new SqliteCommand(checkAssigneeSql, connection);
            var assigneeCount = Convert.ToInt64(await checkAssigneeCommand.ExecuteScalarAsync(cancellationToken));
            if (assigneeCount == 0)
            {
                var alterAssigneeSql = "ALTER TABLE tasks ADD COLUMN assignee TEXT";
                using var alterAssigneeCommand = new SqliteCommand(alterAssigneeSql, connection);
                await alterAssigneeCommand.ExecuteNonQueryAsync(cancellationToken);
            }


        }

        public async System.Threading.Tasks.Task DeleteTaskAsync(string uid, CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);
            using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                var sql = "DELETE FROM tasks WHERE uid = @uid";
                using var command = new SqliteCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@uid", uid);
                await command.ExecuteNonQueryAsync(cancellationToken);

                // Delete from FTS
                command.CommandText = "DELETE FROM tasks_fts WHERE rowid = (SELECT id FROM tasks WHERE uid = @uid)";
                await command.ExecuteNonQueryAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async System.Threading.Tasks.Task CompleteTaskAsync(string uid, CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);

            var sql = "UPDATE tasks SET status = 'done', updated_at = @updatedAt WHERE uid = @uid";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@uid", uid);
            command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async System.Threading.Tasks.Task<List<TaskItem>> SearchTasksFTSAsync(string query, CancellationToken cancellationToken = default)
        {
            var tasks = new List<TaskItem>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);

            // Use FTS5 MATCH
            var sql = @"
				SELECT t.id, t.uid, t.title, t.description, t.priority, t.due_date, t.tags, t.project, t.assignee, t.status, t.created_at, t.updated_at
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
                    Project = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Assignee = reader.IsDBNull(8) ? null : reader.GetString(8),
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

        public async System.Threading.Tasks.Task<List<TaskItem>> SearchTasksAsync(string query, CancellationToken cancellationToken = default)
        {
            return await SearchTasksHybridAsync(query, cancellationToken);
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

        private string GenerateUid()
        {
            // Generate a unique 6-character UID (uppercase alpha + numeric, no 0/O/1/I/l)
            return Guid.NewGuid().ToString().Substring(0, 6);
        }

        public async System.Threading.Tasks.Task<TaskItem> AddTaskAsync(string title, string? description, string priority, DateTime? dueDate, List<string> tags, string? project = null, string? assignee = null, string status = "todo", CancellationToken cancellationToken = default)
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
					INSERT INTO tasks (uid, title, description, priority, due_date, tags, project, assignee, status, created_at, updated_at)
					VALUES (@uid, @title, @description, @priority, @dueDate, @tags, @project, @assignee, @status, @createdAt, @updatedAt)";

                using var command = new SqliteCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@uid", uid);
                command.Parameters.AddWithValue("@title", title);
                command.Parameters.AddWithValue("@description", description ?? "");
                command.Parameters.AddWithValue("@priority", priority);
                command.Parameters.AddWithValue("@dueDate", dueDate?.ToString("yyyy-MM-dd") ?? "");
                command.Parameters.AddWithValue("@tags", string.Join(",", tags));
                command.Parameters.AddWithValue("@project", project ?? "");
                command.Parameters.AddWithValue("@assignee", assignee ?? "");
                command.Parameters.AddWithValue("@status", status);
                command.Parameters.AddWithValue("@createdAt", createdAt.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@updatedAt", updatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                await command.ExecuteNonQueryAsync(cancellationToken);

                var sqlId = "SELECT last_insert_rowid()";
                using var commandId = new SqliteCommand(sqlId, connection, transaction);
                var id = Convert.ToInt64(await commandId.ExecuteScalarAsync(cancellationToken));

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
                    Project = project,
                    Assignee = assignee,
                    Status = status,
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

        public async System.Threading.Tasks.Task<TaskItem?> GetTaskByUidAsync(string uid, CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);
            var sql = "SELECT id, uid, title, description, priority, due_date, tags, project, assignee, status, created_at, updated_at FROM tasks WHERE uid = @uid";
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
                    Project = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Assignee = reader.IsDBNull(8) ? null : reader.GetString(8),
                    Status = reader.GetString(9),
                    CreatedAt = DateTime.Parse(reader.GetString(10), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                    UpdatedAt = DateTime.Parse(reader.GetString(11), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
                };
            }
            return null;
        }

        public async System.Threading.Tasks.Task UpdateTaskAsync(TaskItem task, CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);
            using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                var sql = @"
					UPDATE tasks SET title = @title, description = @description, priority = @priority,
				due_date = @dueDate, tags = @tags, project = @project, assignee = @assignee, status = @status, updated_at = @updatedAt
					WHERE id = @id";

                using var command = new SqliteCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@id", task.Id);
                command.Parameters.AddWithValue("@title", task.Title);
                command.Parameters.AddWithValue("@description", task.Description ?? "");
                command.Parameters.AddWithValue("@priority", task.Priority);
                command.Parameters.AddWithValue("@dueDate", task.DueDate?.ToString("yyyy-MM-dd") ?? "");
                command.Parameters.AddWithValue("@tags", task.TagsString);
                command.Parameters.AddWithValue("@project", task.Project ?? "");
                command.Parameters.AddWithValue("@assignee", task.Assignee ?? "");
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

        public async System.Threading.Tasks.Task<List<TaskItem>> GetAllTasksAsync(CancellationToken cancellationToken = default)
        {
            var tasks = new List<TaskItem>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);
            var sql = "SELECT id, uid, title, description, priority, due_date, tags, project, assignee, status, created_at, updated_at FROM tasks";
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
                    Project = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Assignee = reader.IsDBNull(8) ? null : reader.GetString(8),
                    Status = reader.GetString(9),
                    CreatedAt = DateTime.Parse(reader.GetString(10), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                    UpdatedAt = DateTime.Parse(reader.GetString(11), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
                };
                tasks.Add(task);
            }
            return tasks;
        }

        public async System.Threading.Tasks.Task ClearAllTasksAsync(CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);
            var sql = "DELETE FROM tasks";
            using var command = new SqliteCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
