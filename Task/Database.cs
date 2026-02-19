using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Globalization;

namespace TaskApp
{
    public class Database
    {
        private readonly string _dbPath;

        public Database(string dbPath)
        {
            _dbPath = dbPath;
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            // Set pragmas for performance optimization
            // using var pragmaCommand = new SqliteCommand("PRAGMA journal_mode = WAL;", connection);
            // pragmaCommand.ExecuteNonQuery();
            // pragmaCommand.CommandText = "PRAGMA synchronous = NORMAL;";
            // pragmaCommand.ExecuteNonQuery();
            // pragmaCommand.CommandText = "PRAGMA cache_size = -64000;"; // 64MB
            // pragmaCommand.ExecuteNonQuery();
            // pragmaCommand.CommandText = "PRAGMA temp_store = memory;";
            // pragmaCommand.ExecuteNonQuery();
            // pragmaCommand.CommandText = "PRAGMA mmap_size = 268435456;"; // 256MB
            // pragmaCommand.ExecuteNonQuery();

            // Load sqlite-vss extensions
            // connection.EnableExtensions();
            // var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            // connection.LoadExtension(Path.Combine(exeDir, "vector0"));
            // connection.LoadExtension(Path.Combine(exeDir, "vss0"));

            var schemaSql = @"
-- Main tasks table
CREATE TABLE IF NOT EXISTS tasks (
    id INTEGER PRIMARY KEY,
    uid TEXT UNIQUE NOT NULL,
    title TEXT NOT NULL,
    description TEXT,
    priority TEXT CHECK(priority IN ('high', 'medium', 'low')) DEFAULT 'medium',
    due_date TEXT,
    tags TEXT,
    status TEXT CHECK(status IN ('pending', 'completed')) DEFAULT 'pending',
    created_at TEXT DEFAULT (datetime('now')),
    updated_at TEXT DEFAULT (datetime('now'))
);

-- FTS5 virtual table for full-text search on title, description, tags
CREATE VIRTUAL TABLE IF NOT EXISTS tasks_fts USING fts5(title, description, tags);

-- Trigger to keep FTS table in sync
-- CREATE TRIGGER IF NOT EXISTS tasks_fts_insert AFTER INSERT ON tasks
-- BEGIN
--     INSERT INTO tasks_fts(rowid, title, description, tags) VALUES (new.id, new.title, new.description, new.tags);
-- END;

-- CREATE TRIGGER IF NOT EXISTS tasks_fts_delete AFTER DELETE ON tasks
-- BEGIN
--     DELETE FROM tasks_fts WHERE rowid = old.id;
-- END;

-- CREATE TRIGGER IF NOT EXISTS tasks_fts_update AFTER UPDATE ON tasks
-- BEGIN
--     UPDATE tasks_fts SET title = new.title, description = new.description, tags = new.tags WHERE rowid = new.id;
-- END;

-- vss0 virtual table for vector search (requires sqlite-vss extension)
-- This will store vector embeddings for semantic search
-- CREATE VIRTUAL TABLE IF NOT EXISTS vss_tasks USING vss0(
--     embedding(384) -- Assuming 384-dimensional embeddings from SentenceTransformers
-- );

-- Indexes for performance
CREATE INDEX IF NOT EXISTS idx_tasks_status ON tasks(status);
CREATE INDEX IF NOT EXISTS idx_tasks_priority ON tasks(priority);
CREATE INDEX IF NOT EXISTS idx_tasks_due_date ON tasks(due_date);
";

            using var command = new SqliteCommand(schemaSql, connection);
            command.ExecuteNonQuery();
        }

        // Generate unique ID: 6-character base30 (lowercase alphabet: 2-9, a-h,j-z except i,l,o)
        // First 4 from timestamp, last 2 random, sortable, unique
        private string GenerateId()
        {
            var chars = "23456789abcdefghjkmnpqrstuvwxyz";
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var timestampPart = Base30Encode(timestamp % 10000, chars, 4);
            var randomPart = new string(Enumerable.Range(0, 2).Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
            return timestampPart + randomPart;
        }

        private string Base30Encode(long value, string chars, int length)
        {
            var result = "";
            for (int i = 0; i < length; i++)
            {
                result = chars[(int)(value % 30)] + result;
                value /= 30;
            }
            return result;
        }

        public TaskItem AddTask(string title, string? description = null, string priority = "medium", DateTime? dueDate = null, List<string>? tags = null)
        {
            var uid = GenerateId();
            var task = new TaskItem
            {
                Uid = uid,
                Title = title,
                Description = description,
                Priority = priority,
                DueDate = dueDate,
                Tags = tags ?? new List<string>(),
                Status = "pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                var sql = @"
INSERT INTO tasks (uid, title, description, priority, due_date, tags, status, created_at, updated_at)
VALUES (@uid, @title, @description, @priority, @dueDate, @tags, @status, @createdAt, @updatedAt)";

                using var command = new SqliteCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@uid", task.Uid);
                command.Parameters.AddWithValue("@title", task.Title);
                command.Parameters.AddWithValue("@description", task.Description ?? "");
                command.Parameters.AddWithValue("@priority", task.Priority);
                command.Parameters.AddWithValue("@dueDate", task.DueDate?.ToString("yyyy-MM-dd") ?? "");
                command.Parameters.AddWithValue("@tags", task.TagsString);
                command.Parameters.AddWithValue("@status", task.Status);
                command.Parameters.AddWithValue("@createdAt", task.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@updatedAt", task.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                command.ExecuteNonQuery();

                // Get the inserted id
                command.CommandText = "SELECT last_insert_rowid()";
                task.Id = Convert.ToInt32(command.ExecuteScalar());

                // Insert into FTS
                command.CommandText = "INSERT INTO tasks_fts(rowid, title, description, tags) VALUES (@rowid, @title, @description, @tags)";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("@rowid", task.Id);
                command.Parameters.AddWithValue("@title", task.Title);
                command.Parameters.AddWithValue("@description", task.Description ?? "");
                command.Parameters.AddWithValue("@tags", task.TagsString);
                command.ExecuteNonQuery();

                // Generate and store embedding
                // var taskText = Embeddings.GenerateTaskText(task);
                // var embedding = Embeddings.GenerateEmbedding(taskText);
                // var embeddingJson = "[" + string.Join(",", embedding) + "]";
                // command.CommandText = "INSERT INTO vss_tasks(rowid, embedding) VALUES (@rowid, @embedding)";
                // command.Parameters.Clear();
                // command.Parameters.AddWithValue("@rowid", task.Id);
                // command.Parameters.AddWithValue("@embedding", embeddingJson);
                // command.ExecuteNonQuery();

                transaction.Commit();
                return task;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public List<TaskItem> GetAllTasks()
        {
            var tasks = new List<TaskItem>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var sql = "SELECT id, uid, title, description, priority, due_date, tags, status, created_at, updated_at FROM tasks ORDER BY created_at DESC";
            using var command = new SqliteCommand(sql, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
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

        public TaskItem? GetTaskByUid(string uid)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var sql = "SELECT id, uid, title, description, priority, due_date, tags, status, created_at, updated_at FROM tasks WHERE uid = @uid";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@uid", uid);
            using var reader = command.ExecuteReader();

            if (reader.Read())
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

        public void UpdateTask(TaskItem task)
        {
            task.UpdatedAt = DateTime.UtcNow;

            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            using var transaction = connection.BeginTransaction();

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
                command.Parameters.AddWithValue("@updatedAt", task.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                command.ExecuteNonQuery();

                // Update FTS
                command.CommandText = "UPDATE tasks_fts SET title = @title, description = @description, tags = @tags WHERE rowid = @rowid";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("@rowid", task.Id);
                command.Parameters.AddWithValue("@title", task.Title);
                command.Parameters.AddWithValue("@description", task.Description ?? "");
                command.Parameters.AddWithValue("@tags", task.TagsString);
                command.ExecuteNonQuery();

                // Update embedding
                // var taskText = Embeddings.GenerateTaskText(task);
                // var embedding = Embeddings.GenerateEmbedding(taskText);
                // var embeddingJson = "[" + string.Join(",", embedding) + "]";
                // command.CommandText = "INSERT OR REPLACE INTO vss_tasks(rowid, embedding) VALUES (@rowid, @embedding)";
                // command.Parameters.Clear();
                // command.Parameters.AddWithValue("@rowid", task.Id);
                // command.Parameters.AddWithValue("@embedding", embeddingJson);
                // command.ExecuteNonQuery();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public void DeleteTask(string id)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                var sql = "DELETE FROM tasks WHERE id = @id";
                using var command = new SqliteCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@id", id);
                command.ExecuteNonQuery();

                // Delete from FTS
                command.CommandText = "DELETE FROM tasks_fts WHERE rowid = @id";
                command.ExecuteNonQuery();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public void CompleteTask(string id)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var sql = "UPDATE tasks SET status = 'completed', updated_at = @updatedAt WHERE id = @id";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            command.ExecuteNonQuery();
        }

        public List<TaskItem> SearchTasksFTS(string query)
        {
            var tasks = new List<TaskItem>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            // Use FTS5 MATCH
            var sql = @"
SELECT t.id, t.uid, t.title, t.description, t.priority, t.due_date, t.tags, t.status, t.created_at, t.updated_at
FROM tasks_fts fts
JOIN tasks t ON t.id = fts.rowid
WHERE tasks_fts MATCH @query
ORDER BY rank";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@query", query);
            using var reader = command.ExecuteReader();

            while (reader.Read())
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

        public List<TaskItem> SearchTasksSemantic(string query)
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

        public List<TaskItem> SearchTasksHybrid(string query)
        {
            var ftsTasks = SearchTasksFTS(query);
            var semanticTasks = SearchTasksSemantic(query);

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

        public List<string> GetAllUniqueTags()
        {
            var tags = new HashSet<string>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var sql = "SELECT tags FROM tasks WHERE tags IS NOT NULL AND tags != ''";
            using var command = new SqliteCommand(sql, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
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

        // Benchmarking methods for performance testing
        public TimeSpan BenchmarkAddTasks(int count)
        {
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                AddTask($"Benchmark Task {i}", $"Description for task {i}", "medium", null, new List<string> { "benchmark" });
            }
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }

        public TimeSpan BenchmarkSearch(string query, int iterations = 1)
        {
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                SearchTasksFTS(query);
            }
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }
    }
}