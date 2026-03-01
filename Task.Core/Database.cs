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
					updated_at TEXT NOT NULL,
					archived INTEGER NOT NULL DEFAULT 0,
					archived_at TEXT
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

			// Add archived column if it doesn't exist (for migration)
			var checkArchivedSql = "SELECT COUNT(*) FROM pragma_table_info('tasks') WHERE name='archived'";
			using var checkArchivedCommand = new SqliteCommand(checkArchivedSql, connection);
			var archivedCount = Convert.ToInt64(checkArchivedCommand.ExecuteScalar());
			if (archivedCount == 0)
			{
				var alterArchivedSql = "ALTER TABLE tasks ADD COLUMN archived INTEGER NOT NULL DEFAULT 0";
				using var alterArchivedCommand = new SqliteCommand(alterArchivedSql, connection);
				alterArchivedCommand.ExecuteNonQuery();
			}

			// Add archived_at column if it doesn't exist (for migration)
			var checkArchivedAtSql = "SELECT COUNT(*) FROM pragma_table_info('tasks') WHERE name='archived_at'";
			using var checkArchivedAtCommand = new SqliteCommand(checkArchivedAtSql, connection);
			var archivedAtCount = Convert.ToInt64(checkArchivedAtCommand.ExecuteScalar());
			if (archivedAtCount == 0)
			{
				var alterArchivedAtSql = "ALTER TABLE tasks ADD COLUMN archived_at TEXT";
				using var alterArchivedAtCommand = new SqliteCommand(alterArchivedAtSql, connection);
				alterArchivedAtCommand.ExecuteNonQuery();
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
					updated_at TEXT NOT NULL,
					archived INTEGER NOT NULL DEFAULT 0,
					archived_at TEXT
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

			// Add archived column if it doesn't exist (for migration)
			var checkArchivedSql = "SELECT COUNT(*) FROM pragma_table_info('tasks') WHERE name='archived'";
			using var checkArchivedCommand = new SqliteCommand(checkArchivedSql, connection);
			var archivedCount = Convert.ToInt64(await checkArchivedCommand.ExecuteScalarAsync(cancellationToken));
			if (archivedCount == 0)
			{
				var alterArchivedSql = "ALTER TABLE tasks ADD COLUMN archived INTEGER NOT NULL DEFAULT 0";
				using var alterArchivedCommand = new SqliteCommand(alterArchivedSql, connection);
				await alterArchivedCommand.ExecuteNonQueryAsync(cancellationToken);
			}

			// Add archived_at column if it doesn't exist (for migration)
			var checkArchivedAtSql = "SELECT COUNT(*) FROM pragma_table_info('tasks') WHERE name='archived_at'";
			using var checkArchivedAtCommand = new SqliteCommand(checkArchivedAtSql, connection);
			var archivedAtCount = Convert.ToInt64(await checkArchivedAtCommand.ExecuteScalarAsync(cancellationToken));
			if (archivedAtCount == 0)
			{
				var alterArchivedAtSql = "ALTER TABLE tasks ADD COLUMN archived_at TEXT";
				using var alterArchivedAtCommand = new SqliteCommand(alterArchivedAtSql, connection);
				await alterArchivedAtCommand.ExecuteNonQueryAsync(cancellationToken);
			}
		}

		// === Restored methods (excluding archive logic) ===

		private string GenerateUid()
		{
			// Generate a unique 6-character ID
			return Guid.NewGuid().ToString().Substring(0, 6);
		}

		public async System.Threading.Tasks.Task<TaskItem> AddTaskAsync(
			string title, string? description, string priority, DateTime? dueDate, List<string> tags, string? project = null, string? assignee = null, string status = "todo", CancellationToken cancellationToken = default)
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
                    INSERT INTO tasks (uid, title, description, priority, due_date, tags, project, assignee, status, created_at, updated_at, archived, archived_at) VALUES
                    (@uid, @title, @description, @priority, @dueDate, @tags, @project, @assignee, @status, @createdAt, @updatedAt, 0, null)
                ";
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
					UpdatedAt = updatedAt,
					Archived = false,
					ArchivedAt = null
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
			var sql = "SELECT id, uid, title, description, priority, due_date, tags, project, assignee, status, created_at, updated_at, archived, archived_at FROM tasks WHERE uid = @uid";
			using var command = new SqliteCommand(sql, connection);
			command.Parameters.AddWithValue("@uid", uid);
			using var reader = await command.ExecuteReaderAsync(cancellationToken);
if (await reader.ReadAsync(cancellationToken))
				{
					// Hide archived tasks from fetch by UID
					if (!reader.IsDBNull(12) && reader.GetInt32(12) != 0)
						return null;
					return new TaskItem
				{
					Id = reader.GetInt32(0),
					Uid = reader.GetString(1),
					Title = reader.GetString(2),
					Description = reader.IsDBNull(3) ? null : reader.GetString(3),
					Priority = reader.GetString(4),
					DueDate = reader.IsDBNull(5) || string.IsNullOrEmpty(reader.GetString(5)) ? null : DateTime.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
					Tags = reader.IsDBNull(6) ? new List<string>() : reader.GetString(6).Split(",").Where(t => !string.IsNullOrEmpty(t)).ToList(),
					Project = reader.IsDBNull(7) ? null : reader.GetString(7),
					Assignee = reader.IsDBNull(8) ? null : reader.GetString(8),
					Status = reader.GetString(9),
					CreatedAt = DateTime.Parse(reader.GetString(10), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
					UpdatedAt = DateTime.Parse(reader.GetString(11), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
					Archived = !reader.IsDBNull(12) && reader.GetInt32(12) != 0,
					ArchivedAt = reader.IsDBNull(13) || string.IsNullOrEmpty(reader.GetString(13)) ? (DateTime?)null : DateTime.Parse(reader.GetString(13), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
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
                due_date = @dueDate, tags = @tags, project = @project, assignee = @assignee, status = @status, updated_at = @updatedAt, archived = @archived, archived_at = @archivedAt
                WHERE id = @id
                ";
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
				command.Parameters.AddWithValue("@archived", task.Archived ? 1 : 0);
				command.Parameters.AddWithValue("@archivedAt", task.ArchivedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);
				await command.ExecuteNonQueryAsync(cancellationToken);
				await transaction.CommitAsync(cancellationToken);
			}
			catch
			{
				await transaction.RollbackAsync(cancellationToken);
				throw;
			}
		}

		public async System.Threading.Tasks.Task DeleteTaskAsync(string uid, CancellationToken cancellationToken = default)
		{
			// Archive instead of delete
			var task = await GetTaskByUidAsync(uid, cancellationToken);
			if (task == null) return;
			if (!task.Archived)
			{
				task.Archived = true;
				task.ArchivedAt = DateTime.UtcNow;
				await UpdateTaskAsync(task, cancellationToken);
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

		public async System.Threading.Tasks.Task<List<TaskItem>> GetAllTasksAsync(CancellationToken cancellationToken = default)
		{
			var tasks = new List<TaskItem>();
			using var connection = new SqliteConnection($"Data Source={_dbPath}");
			await connection.OpenAsync(cancellationToken);
			var sql = "SELECT id, uid, title, description, priority, due_date, tags, project, assignee, status, created_at, updated_at, archived, archived_at FROM tasks WHERE archived = 0";
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
					Tags = reader.IsDBNull(6) ? new List<string>() : reader.GetString(6).Split(",").Where(t => !string.IsNullOrEmpty(t)).ToList(),
					Project = reader.IsDBNull(7) ? null : reader.GetString(7),
					Assignee = reader.IsDBNull(8) ? null : reader.GetString(8),
					Status = reader.GetString(9),
					CreatedAt = DateTime.Parse(reader.GetString(10), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
					UpdatedAt = DateTime.Parse(reader.GetString(11), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
					Archived = !reader.IsDBNull(12) && reader.GetInt32(12) != 0,
					ArchivedAt = reader.IsDBNull(13) || string.IsNullOrEmpty(reader.GetString(13)) ? (DateTime?)null : DateTime.Parse(reader.GetString(13), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
				};
				tasks.Add(task);
			}
			return tasks;
		}

		public async System.Threading.Tasks.Task<List<string>> GetAllUniqueTagsAsync(CancellationToken cancellationToken = default)
		{
			var tags = new HashSet<string>();
			using var connection = new SqliteConnection($"Data Source={_dbPath}");
			await connection.OpenAsync(cancellationToken);
			var sql = "SELECT tags FROM tasks WHERE tags IS NOT NULL AND tags != '' AND archived = 0";
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

		public async System.Threading.Tasks.Task<List<TaskItem>> SearchTasksFTSAsync(string query, CancellationToken cancellationToken = default)
		{
			var tasks = new List<TaskItem>();
			using var connection = new SqliteConnection($"Data Source={_dbPath}");
			await connection.OpenAsync(cancellationToken);
			var sql = @"
                SELECT t.id, t.uid, t.title, t.description, t.priority, t.due_date, t.tags, t.project, t.assignee, t.status, t.created_at, t.updated_at, t.archived, t.archived_at
                FROM tasks_fts fts
                JOIN tasks t ON t.id = fts.rowid
                WHERE tasks_fts MATCH @query AND t.archived = 0
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
					Tags = reader.IsDBNull(6) ? new List<string>() : reader.GetString(6).Split(",").Where(t => !string.IsNullOrEmpty(t)).ToList(),
					Project = reader.IsDBNull(7) ? null : reader.GetString(7),
					Assignee = reader.IsDBNull(8) ? null : reader.GetString(8),
					Status = reader.GetString(9),
					CreatedAt = DateTime.Parse(reader.GetString(10), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
					UpdatedAt = DateTime.Parse(reader.GetString(11), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
					Archived = !reader.IsDBNull(12) && reader.GetInt32(12) != 0,
					ArchivedAt = reader.IsDBNull(13) || string.IsNullOrEmpty(reader.GetString(13)) ? (DateTime?)null : DateTime.Parse(reader.GetString(13), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
				};
				tasks.Add(task);
			}
			return tasks;
		}

		public async System.Threading.Tasks.Task<List<TaskItem>> SearchTasksSemanticAsync(string query, CancellationToken cancellationToken = default)
		{
			// Placeholder: returns empty; implementation would require vector extensions.
			return new List<TaskItem>();
		}

		public async System.Threading.Tasks.Task<List<TaskItem>> SearchTasksHybridAsync(string query, CancellationToken cancellationToken = default)
		{
			var ftsTasks = await SearchTasksFTSAsync(query, cancellationToken);
			var semanticTasks = await SearchTasksSemanticAsync(query, cancellationToken);
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

		public async System.Threading.Tasks.Task ClearAllTasksAsync(CancellationToken cancellationToken = default)
		{
			// Archive all tasks instead of deleting them
			using var connection = new SqliteConnection($"Data Source={_dbPath}");
			await connection.OpenAsync(cancellationToken);
			var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
			var sql = "UPDATE tasks SET archived = 1, archived_at = @archivedAt WHERE archived = 0";
			using var command = new SqliteCommand(sql, connection);
			command.Parameters.AddWithValue("@archivedAt", now);
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
	}
}

// === End restored methods ===

