using Microsoft.Data.Sqlite;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace Task.Core
{
	public class Database
	{
		private readonly DatabaseConnectionSettings _settings;

		public Database(string dbPath)
			: this(DatabaseConnectionSettings.ForSqlite(dbPath))
		{
		}

		public Database(DatabaseConnectionSettings settings)
		{
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));
		}

		public string Provider => _settings.Provider;

		public void Initialize()
		{
			if (UsesPostgres)
			{
				InitializePostgres();
				return;
			}

			InitializeSqlite();
		}

		public async global::System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken = default)
		{
			if (UsesPostgres)
			{
				await InitializePostgresAsync(cancellationToken);
				return;
			}

			await InitializeSqliteAsync(cancellationToken);
		}

		public async global::System.Threading.Tasks.Task<TaskItem> AddTaskAsync(
			string uid,
			string title,
			string? description,
			string priority,
			DateTime? dueDate,
			List<string> tags,
			string? project = null,
			string? assignee = null,
			string status = "todo",
			string? blockReason = null,
			CancellationToken cancellationToken = default)
		{
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			try
			{
				var createdAt = DateTime.UtcNow;
				var updatedAt = createdAt;
				var insertSql = UsesPostgres
					? @"
						INSERT INTO tasks (uid, title, description, priority, due_date, tags, project, assignee, status, block_reason, created_at, updated_at, archived, archived_at)
						VALUES (@uid, @title, @description, @duePriority, @dueDate, @tags, @project, @assignee, @status, @blockReason, @createdAt, @updatedAt, FALSE, NULL)
						RETURNING id"
					: @"
						INSERT INTO tasks (uid, title, description, priority, due_date, tags, project, assignee, status, block_reason, created_at, updated_at, archived, archived_at)
						VALUES (@uid, @title, @description, @duePriority, @dueDate, @tags, @project, @assignee, @status, @blockReason, @createdAt, @updatedAt, 0, NULL)";

				await using var insertCommand = connection.CreateCommand();
				insertCommand.Transaction = transaction;
				insertCommand.CommandText = insertSql;
				AddTaskMutationParameters(insertCommand, uid, title, description, priority, dueDate, tags, project, assignee, status, blockReason, createdAt, updatedAt);

				var id = UsesPostgres
					? Convert.ToInt32(await insertCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture)
					: await ExecuteSqliteInsertAsync(connection, transaction, insertCommand, cancellationToken);

				await transaction.CommitAsync(cancellationToken);

				return new TaskItem
				{
					Id = id,
					Uid = uid,
					Title = title,
					Description = description,
					Priority = priority,
					DueDate = dueDate,
					Tags = tags,
					Project = project,
					Assignee = assignee,
					Status = status,
					BlockReason = blockReason,
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

		public async global::System.Threading.Tasks.Task<TaskItem?> GetTaskByUidAsync(string uid, CancellationToken cancellationToken = default)
		{
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT id, uid, title, description, priority, due_date, tags, project, assignee, status, block_reason, created_at, updated_at, archived, archived_at FROM tasks WHERE uid = @uid";
			AddParameter(command, "@uid", uid);

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			if (!await reader.ReadAsync(cancellationToken))
			{
				return null;
			}

			if (ReadArchivedFlag(reader, 13))
			{
				return null;
			}

			return ReadTask(reader);
		}

		public async global::System.Threading.Tasks.Task UpdateTaskAsync(TaskItem task, CancellationToken cancellationToken = default)
		{
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			try
			{
				await using var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText = @"
					UPDATE tasks
					SET title = @title,
						description = @description,
						priority = @priority,
						due_date = @dueDate,
						tags = @tags,
						project = @project,
						assignee = @assignee,
						status = @status,
						block_reason = @blockReason,
						updated_at = @updatedAt,
						archived = @archived,
						archived_at = @archivedAt
					WHERE id = @id";

				AddParameter(command, "@id", task.Id);
				AddParameter(command, "@title", task.Title);
				AddParameter(command, "@description", task.Description ?? string.Empty);
				AddParameter(command, "@priority", task.Priority);
				AddParameter(command, "@dueDate", task.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty);
				AddParameter(command, "@tags", task.TagsString);
				AddParameter(command, "@project", task.Project ?? string.Empty);
				AddParameter(command, "@assignee", task.Assignee ?? string.Empty);
				AddParameter(command, "@status", task.Status);
				AddParameter(command, "@blockReason", string.IsNullOrWhiteSpace(task.BlockReason) ? DBNull.Value : task.BlockReason);
				AddParameter(command, "@updatedAt", FormatStoredDateTime(DateTime.UtcNow));
				AddParameter(command, "@archived", UsesPostgres ? task.Archived : task.Archived ? 1 : 0);
				AddParameter(command, "@archivedAt", task.ArchivedAt.HasValue ? FormatStoredDateTime(task.ArchivedAt.Value) : DBNull.Value);

				await command.ExecuteNonQueryAsync(cancellationToken);
				await transaction.CommitAsync(cancellationToken);
			}
			catch
			{
				await transaction.RollbackAsync(cancellationToken);
				throw;
			}
		}

		public async global::System.Threading.Tasks.Task DeleteTaskAsync(string uid, CancellationToken cancellationToken = default)
		{
			var task = await GetTaskByUidAsync(uid, cancellationToken);
			if (task == null)
			{
				return;
			}

			if (!task.Archived)
			{
				task.Archived = true;
				task.ArchivedAt = DateTime.UtcNow;
				await UpdateTaskAsync(task, cancellationToken);
			}
		}

		public async global::System.Threading.Tasks.Task CompleteTaskAsync(string uid, CancellationToken cancellationToken = default)
		{
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "UPDATE tasks SET status = 'done', updated_at = @updatedAt WHERE uid = @uid";
			AddParameter(command, "@uid", uid);
			AddParameter(command, "@updatedAt", FormatStoredDateTime(DateTime.UtcNow));
			await command.ExecuteNonQueryAsync(cancellationToken);
		}

		public async global::System.Threading.Tasks.Task<List<TaskItem>> GetAllTasksAsync(CancellationToken cancellationToken = default)
		{
			var tasks = new List<TaskItem>();
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = $"SELECT id, uid, title, description, priority, due_date, tags, project, assignee, status, block_reason, created_at, updated_at, archived, archived_at FROM tasks WHERE {ActiveTasksPredicate}";

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				tasks.Add(ReadTask(reader));
			}

			return tasks;
		}

		public async global::System.Threading.Tasks.Task<List<string>> GetAllUniqueTagsAsync(CancellationToken cancellationToken = default)
		{
			var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = $"SELECT tags FROM tasks WHERE tags IS NOT NULL AND tags != '' AND {ActiveTasksPredicate}";

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				var tagString = reader.IsDBNull(0) ? null : reader.GetString(0);
				if (string.IsNullOrWhiteSpace(tagString))
				{
					continue;
				}

				foreach (var tag in tagString.Split(',').Select(static t => t.Trim()).Where(static t => !string.IsNullOrWhiteSpace(t)))
				{
					tags.Add(tag);
				}
			}

			return tags.OrderBy(static tag => tag, StringComparer.OrdinalIgnoreCase).ToList();
		}

		public async global::System.Threading.Tasks.Task<List<TaskItem>> SearchTasksFTSAsync(string query, CancellationToken cancellationToken = default)
		{
			return UsesPostgres
				? await SearchTasksPostgresAsync(query, cancellationToken)
				: await SearchTasksSqliteAsync(query, cancellationToken);
		}

		public async global::System.Threading.Tasks.Task<List<TaskItem>> SearchTasksSemanticAsync(string query, CancellationToken cancellationToken = default)
		{
			return await global::System.Threading.Tasks.Task.FromResult(new List<TaskItem>());
		}

		public async global::System.Threading.Tasks.Task<List<TaskItem>> SearchTasksHybridAsync(string query, CancellationToken cancellationToken = default)
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

		public async global::System.Threading.Tasks.Task<List<TaskItem>> SearchTasksAsync(string query, CancellationToken cancellationToken = default)
		{
			return await SearchTasksHybridAsync(query, cancellationToken);
		}

		public async global::System.Threading.Tasks.Task ClearAllTasksAsync(CancellationToken cancellationToken = default)
		{
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = $"UPDATE tasks SET archived = @archived, archived_at = @archivedAt WHERE {ActiveTasksPredicate}";
			AddParameter(command, "@archived", UsesPostgres ? true : 1);
			AddParameter(command, "@archivedAt", FormatStoredDateTime(DateTime.UtcNow));
			await command.ExecuteNonQueryAsync(cancellationToken);
		}

		private bool UsesPostgres => _settings.Provider == DatabaseProviders.Postgres;

		private string ActiveTasksPredicate => UsesPostgres ? "archived = FALSE" : "archived = 0";

		private void InitializeSqlite()
		{
			EnsureSqliteParentDirectoryExists();
			using var connection = new SqliteConnection($"Data Source={_settings.SqliteDatabasePath}");
			connection.Open();
			using var command = new SqliteCommand(SqliteInitializationSql, connection);
			command.ExecuteNonQuery();
			RunSqliteMigrations(connection);
		}

		private async global::System.Threading.Tasks.Task InitializeSqliteAsync(CancellationToken cancellationToken)
		{
			EnsureSqliteParentDirectoryExists();
			await using var connection = new SqliteConnection($"Data Source={_settings.SqliteDatabasePath}");
			await connection.OpenAsync(cancellationToken);
			await using var command = new SqliteCommand(SqliteInitializationSql, connection);
			await command.ExecuteNonQueryAsync(cancellationToken);
			await RunSqliteMigrationsAsync(connection, cancellationToken);
		}

		private void InitializePostgres()
		{
			using var connection = new NpgsqlConnection(_settings.PostgresConnectionString);
			connection.Open();
			using var command = new NpgsqlCommand(PostgresInitializationSql, connection);
			command.ExecuteNonQuery();
		}

		private async global::System.Threading.Tasks.Task InitializePostgresAsync(CancellationToken cancellationToken)
		{
			await using var connection = new NpgsqlConnection(_settings.PostgresConnectionString);
			await connection.OpenAsync(cancellationToken);
			await using var command = new NpgsqlCommand(PostgresInitializationSql, connection);
			await command.ExecuteNonQueryAsync(cancellationToken);
		}

		private async global::System.Threading.Tasks.Task<int> ExecuteSqliteInsertAsync(
			DbConnection connection,
			DbTransaction transaction,
			DbCommand insertCommand,
			CancellationToken cancellationToken)
		{
			await insertCommand.ExecuteNonQueryAsync(cancellationToken);

			await using var idCommand = connection.CreateCommand();
			idCommand.Transaction = transaction;
			idCommand.CommandText = "SELECT last_insert_rowid()";
			var scalar = await idCommand.ExecuteScalarAsync(cancellationToken);
			return Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
		}

		private async global::System.Threading.Tasks.Task<List<TaskItem>> SearchTasksSqliteAsync(string query, CancellationToken cancellationToken)
		{
			var tasks = new List<TaskItem>();
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = @"
				SELECT t.id, t.uid, t.title, t.description, t.priority, t.due_date, t.tags, t.project, t.assignee, t.status, t.block_reason, t.created_at, t.updated_at, t.archived, t.archived_at
				FROM tasks_fts fts
				JOIN tasks t ON t.id = fts.rowid
				WHERE tasks_fts MATCH @query AND t.archived = 0
				ORDER BY rank";
			AddParameter(command, "@query", query);

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				tasks.Add(ReadTask(reader));
			}

			return tasks;
		}

		private async global::System.Threading.Tasks.Task<List<TaskItem>> SearchTasksPostgresAsync(string query, CancellationToken cancellationToken)
		{
			var tasks = new List<TaskItem>();
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = @"
				SELECT id, uid, title, description, priority, due_date, tags, project, assignee, status, block_reason, created_at, updated_at, archived, archived_at
				FROM tasks
				WHERE archived = FALSE
				  AND (
						title ILIKE @query
						OR COALESCE(description, '') ILIKE @query
						OR COALESCE(tags, '') ILIKE @query)
				ORDER BY created_at DESC";
			AddParameter(command, "@query", $"%{query}%");

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				tasks.Add(ReadTask(reader));
			}

			return tasks;
		}

		private static void AddTaskMutationParameters(
			DbCommand command,
			string uid,
			string title,
			string? description,
			string priority,
			DateTime? dueDate,
			List<string> tags,
			string? project,
			string? assignee,
			string status,
			string? blockReason,
			DateTime createdAt,
			DateTime updatedAt)
		{
			AddParameter(command, "@uid", uid);
			AddParameter(command, "@title", title);
			AddParameter(command, "@description", description ?? string.Empty);
			AddParameter(command, "@duePriority", priority);
			AddParameter(command, "@dueDate", dueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty);
			AddParameter(command, "@tags", string.Join(',', tags));
			AddParameter(command, "@project", project ?? string.Empty);
			AddParameter(command, "@assignee", assignee ?? string.Empty);
			AddParameter(command, "@status", status);
			AddParameter(command, "@blockReason", string.IsNullOrWhiteSpace(blockReason) ? DBNull.Value : blockReason);
			AddParameter(command, "@createdAt", FormatStoredDateTime(createdAt));
			AddParameter(command, "@updatedAt", FormatStoredDateTime(updatedAt));
		}

		private static void AddParameter(DbCommand command, string name, object? value)
		{
			var parameter = command.CreateParameter();
			parameter.ParameterName = name;
			parameter.Value = value ?? DBNull.Value;
			command.Parameters.Add(parameter);
		}

		private async global::System.Threading.Tasks.Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
		{
			if (UsesPostgres)
			{
				var postgresConnection = new NpgsqlConnection(_settings.PostgresConnectionString);
				await postgresConnection.OpenAsync(cancellationToken);
				return postgresConnection;
			}

			EnsureSqliteParentDirectoryExists();
			var sqliteConnection = new SqliteConnection($"Data Source={_settings.SqliteDatabasePath}");
			await sqliteConnection.OpenAsync(cancellationToken);
			return sqliteConnection;
		}

		private void EnsureSqliteParentDirectoryExists()
		{
			var directoryPath = Path.GetDirectoryName(_settings.SqliteDatabasePath);
			if (string.IsNullOrWhiteSpace(directoryPath))
			{
				return;
			}

			Directory.CreateDirectory(directoryPath);
		}

		private static TaskItem ReadTask(DbDataReader reader)
		{
			return new TaskItem
			{
				Id = reader.GetInt32(0),
				Uid = reader.GetString(1),
				Title = reader.GetString(2),
				Description = ReadNullableString(reader, 3),
				Priority = reader.GetString(4),
				DueDate = ParseOptionalStoredDateTime(reader, 5),
				Tags = ParseTags(reader, 6),
				Project = ReadNullableString(reader, 7),
				Assignee = ReadNullableString(reader, 8),
				Status = reader.GetString(9),
				BlockReason = ReadNullableString(reader, 10),
				CreatedAt = ParseRequiredStoredDateTime(reader, 11),
				UpdatedAt = ParseRequiredStoredDateTime(reader, 12),
				Archived = ReadArchivedFlag(reader, 13),
				ArchivedAt = ParseOptionalStoredDateTime(reader, 14)
			};
		}

		private static List<string> ParseTags(DbDataReader reader, int ordinal)
		{
			var tagValue = ReadNullableString(reader, ordinal);
			if (string.IsNullOrWhiteSpace(tagValue))
			{
				return new List<string>();
			}

			return tagValue.Split(',').Where(static tag => !string.IsNullOrWhiteSpace(tag)).ToList();
		}

		private static string? ReadNullableString(DbDataReader reader, int ordinal)
		{
			if (reader.IsDBNull(ordinal))
			{
				return null;
			}

			var value = reader.GetValue(ordinal)?.ToString();
			return string.IsNullOrWhiteSpace(value) ? null : value;
		}

		private static bool ReadArchivedFlag(DbDataReader reader, int ordinal)
		{
			if (reader.IsDBNull(ordinal))
			{
				return false;
			}

			var value = reader.GetValue(ordinal);
			return value switch
			{
				bool booleanValue => booleanValue,
				byte byteValue => byteValue != 0,
				short shortValue => shortValue != 0,
				int intValue => intValue != 0,
				long longValue => longValue != 0,
				string stringValue when bool.TryParse(stringValue, out var parsedBool) => parsedBool,
				string stringValue when int.TryParse(stringValue, out var parsedInt) => parsedInt != 0,
				_ => false
			};
		}

		private static DateTime ParseRequiredStoredDateTime(DbDataReader reader, int ordinal)
		{
			return ParseOptionalStoredDateTime(reader, ordinal) ?? DateTime.MinValue;
		}

		private static DateTime? ParseOptionalStoredDateTime(DbDataReader reader, int ordinal)
		{
			if (reader.IsDBNull(ordinal))
			{
				return null;
			}

			var value = reader.GetValue(ordinal);
			if (value is DateTime dateTime)
			{
				return dateTime.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc) : dateTime.ToUniversalTime();
			}

			var text = value?.ToString();
			if (string.IsNullOrWhiteSpace(text))
			{
				return null;
			}

			return DateTime.TryParse(
				text,
				CultureInfo.InvariantCulture,
				DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
				out var parsed)
				? parsed
				: null;
		}

		private static string FormatStoredDateTime(DateTime value)
		{
			return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
		}

		private static void RunSqliteMigrations(SqliteConnection connection)
		{
			RunSqliteMigration(connection, "project", "ALTER TABLE tasks ADD COLUMN project TEXT");
			RunSqliteMigration(connection, "assignee", "ALTER TABLE tasks ADD COLUMN assignee TEXT");
			RunSqliteMigration(connection, "archived", "ALTER TABLE tasks ADD COLUMN archived INTEGER NOT NULL DEFAULT 0");
			RunSqliteMigration(connection, "archived_at", "ALTER TABLE tasks ADD COLUMN archived_at TEXT");
			RunSqliteMigration(connection, "block_reason", "ALTER TABLE tasks ADD COLUMN block_reason TEXT");
		}

		private static async global::System.Threading.Tasks.Task RunSqliteMigrationsAsync(SqliteConnection connection, CancellationToken cancellationToken)
		{
			await RunSqliteMigrationAsync(connection, "project", "ALTER TABLE tasks ADD COLUMN project TEXT", cancellationToken);
			await RunSqliteMigrationAsync(connection, "assignee", "ALTER TABLE tasks ADD COLUMN assignee TEXT", cancellationToken);
			await RunSqliteMigrationAsync(connection, "archived", "ALTER TABLE tasks ADD COLUMN archived INTEGER NOT NULL DEFAULT 0", cancellationToken);
			await RunSqliteMigrationAsync(connection, "archived_at", "ALTER TABLE tasks ADD COLUMN archived_at TEXT", cancellationToken);
			await RunSqliteMigrationAsync(connection, "block_reason", "ALTER TABLE tasks ADD COLUMN block_reason TEXT", cancellationToken);
		}

		private static void RunSqliteMigration(SqliteConnection connection, string columnName, string sql)
		{
			using var checkCommand = new SqliteCommand($"SELECT COUNT(*) FROM pragma_table_info('tasks') WHERE name='{columnName}'", connection);
			var count = Convert.ToInt64(checkCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
			if (count != 0)
			{
				return;
			}

			using var alterCommand = new SqliteCommand(sql, connection);
			alterCommand.ExecuteNonQuery();
		}

		private static async global::System.Threading.Tasks.Task RunSqliteMigrationAsync(SqliteConnection connection, string columnName, string sql, CancellationToken cancellationToken)
		{
			await using var checkCommand = new SqliteCommand($"SELECT COUNT(*) FROM pragma_table_info('tasks') WHERE name='{columnName}'", connection);
			var count = Convert.ToInt64(await checkCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
			if (count != 0)
			{
				return;
			}

			await using var alterCommand = new SqliteCommand(sql, connection);
			await alterCommand.ExecuteNonQueryAsync(cancellationToken);
		}

		private const string SqliteInitializationSql = @"
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
				block_reason TEXT,
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
			CREATE INDEX IF NOT EXISTS idx_tasks_assignee ON tasks(assignee);";

		private const string PostgresInitializationSql = @"
			CREATE TABLE IF NOT EXISTS tasks (
				id INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
				uid TEXT UNIQUE NOT NULL,
				title TEXT NOT NULL,
				description TEXT,
				priority TEXT NOT NULL,
				due_date TEXT,
				tags TEXT,
				project TEXT,
				assignee TEXT,
				status TEXT NOT NULL,
				block_reason TEXT,
				created_at TEXT NOT NULL,
				updated_at TEXT NOT NULL,
				archived BOOLEAN NOT NULL DEFAULT FALSE,
				archived_at TEXT
			);
			ALTER TABLE tasks ADD COLUMN IF NOT EXISTS project TEXT;
			ALTER TABLE tasks ADD COLUMN IF NOT EXISTS assignee TEXT;
			ALTER TABLE tasks ADD COLUMN IF NOT EXISTS archived BOOLEAN NOT NULL DEFAULT FALSE;
			ALTER TABLE tasks ADD COLUMN IF NOT EXISTS archived_at TEXT;
			ALTER TABLE tasks ADD COLUMN IF NOT EXISTS block_reason TEXT;
			CREATE INDEX IF NOT EXISTS idx_tasks_uid ON tasks(uid);
			CREATE INDEX IF NOT EXISTS idx_tasks_status ON tasks(status);
			CREATE INDEX IF NOT EXISTS idx_tasks_priority ON tasks(priority);
			CREATE INDEX IF NOT EXISTS idx_tasks_due_date ON tasks(due_date);
			CREATE INDEX IF NOT EXISTS idx_tasks_created_at ON tasks(created_at);
			CREATE INDEX IF NOT EXISTS idx_tasks_project ON tasks(project);
			CREATE INDEX IF NOT EXISTS idx_tasks_assignee ON tasks(assignee);";
	}
}
