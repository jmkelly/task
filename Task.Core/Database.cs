using Microsoft.Data.Sqlite;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Task.Core.Auth;

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
			string? userId = null,
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
						INSERT INTO tasks (uid, title, description, priority, due_date, tags, project, assignee, status, block_reason, created_at, updated_at, archived, archived_at, user_id, created_by)
						VALUES (@uid, @title, @description, @duePriority, @dueDate, @tags, @project, @assignee, @status, @blockReason, @createdAt, @updatedAt, FALSE, NULL, @userId, @userId)
						RETURNING id"
					: @"
						INSERT INTO tasks (uid, title, description, priority, due_date, tags, project, assignee, status, block_reason, created_at, updated_at, archived, archived_at, user_id, created_by)
						VALUES (@uid, @title, @description, @duePriority, @dueDate, @tags, @project, @assignee, @status, @blockReason, @createdAt, @updatedAt, 0, NULL, @userId, @userId)";

				await using var insertCommand = connection.CreateCommand();
				insertCommand.Transaction = transaction;
				insertCommand.CommandText = insertSql;
				AddTaskMutationParameters(insertCommand, uid, title, description, priority, dueDate, tags, project, assignee, status, blockReason, createdAt, updatedAt, userId);

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

		public async global::System.Threading.Tasks.Task<TaskItem?> GetTaskByUidAsync(string uid, string? userId = null, CancellationToken cancellationToken = default)
		{
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT id, uid, title, description, priority, due_date, tags, project, assignee, status, block_reason, created_at, updated_at, archived, archived_at FROM tasks WHERE uid = @uid";
			AddParameter(command, "@uid", uid);
			AppendUserScope(command, "user_id", userId);

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

		public async global::System.Threading.Tasks.Task UpdateTaskAsync(TaskItem task, string? userId = null, CancellationToken cancellationToken = default)
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
						archived_at = @archivedAt,
						updated_by = @userId
					WHERE id = @id";
				if (userId != null)
				{
					// Scope the update to the owning user; the @userId parameter is already bound.
					command.CommandText += " AND user_id = @userId";
				}

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
				AddParameter(command, "@userId", userId);
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

		public async global::System.Threading.Tasks.Task DeleteTaskAsync(string uid, string? userId = null, CancellationToken cancellationToken = default)
		{
			var task = await GetTaskByUidAsync(uid, userId, cancellationToken);
			if (task == null)
			{
				return;
			}

			if (!task.Archived)
			{
				task.Archived = true;
				task.ArchivedAt = DateTime.UtcNow;
				await UpdateTaskAsync(task, userId, cancellationToken);
			}
		}

		public async global::System.Threading.Tasks.Task CompleteTaskAsync(string uid, string? userId = null, CancellationToken cancellationToken = default)
		{
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "UPDATE tasks SET status = 'done', updated_at = @updatedAt, updated_by = @userId WHERE uid = @uid";
			AddParameter(command, "@uid", uid);
			AddParameter(command, "@updatedAt", FormatStoredDateTime(DateTime.UtcNow));
			AddParameter(command, "@userId", userId);
			if (userId != null)
			{
				command.CommandText += " AND user_id = @userId";
			}
			await command.ExecuteNonQueryAsync(cancellationToken);
		}

		public async global::System.Threading.Tasks.Task<List<TaskItem>> GetAllTasksAsync(string? userId = null, CancellationToken cancellationToken = default)
		{
			var tasks = new List<TaskItem>();
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = $"SELECT id, uid, title, description, priority, due_date, tags, project, assignee, status, block_reason, created_at, updated_at, archived, archived_at FROM tasks WHERE {ActiveTasksPredicate}";
			AppendUserScope(command, "user_id", userId);

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				tasks.Add(ReadTask(reader));
			}

			return tasks;
		}

		/// <summary>Admin view: every task (any owner) including unowned legacy rows.</summary>
		public async global::System.Threading.Tasks.Task<List<AdminTaskView>> GetAllTasksAdminAsync(CancellationToken cancellationToken = default)
		{
			var tasks = new List<AdminTaskView>();
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = $"SELECT uid, title, status, assignee, user_id, created_at, updated_at FROM tasks WHERE {ActiveTasksPredicate} ORDER BY created_at DESC";

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				tasks.Add(new AdminTaskView
				{
					Uid = reader.GetString(0),
					Title = reader.GetString(1),
					Status = reader.GetString(2),
					Assignee = ReadNullableString(reader, 3),
					UserId = ReadNullableString(reader, 4),
					CreatedAt = ParseRequiredStoredDateTime(reader, 5),
					UpdatedAt = ParseRequiredStoredDateTime(reader, 6)
				});
			}

			return tasks;
		}

		public async global::System.Threading.Tasks.Task<List<string>> GetAllUniqueTagsAsync(string? userId = null, CancellationToken cancellationToken = default)
		{
			var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = $"SELECT tags FROM tasks WHERE tags IS NOT NULL AND tags != '' AND {ActiveTasksPredicate}";
			AppendUserScope(command, "user_id", userId);

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

		public async global::System.Threading.Tasks.Task<List<TaskItem>> SearchTasksFTSAsync(string query, string? userId = null, CancellationToken cancellationToken = default)
		{
			return UsesPostgres
				? await SearchTasksPostgresAsync(query, userId, cancellationToken)
				: await SearchTasksSqliteAsync(query, userId, cancellationToken);
		}

		public async global::System.Threading.Tasks.Task<List<TaskItem>> SearchTasksSemanticAsync(string query, string? userId = null, CancellationToken cancellationToken = default)
		{
			return await global::System.Threading.Tasks.Task.FromResult(new List<TaskItem>());
		}

		public async global::System.Threading.Tasks.Task<List<TaskItem>> SearchTasksHybridAsync(string query, string? userId = null, CancellationToken cancellationToken = default)
		{
			var ftsTasks = await SearchTasksFTSAsync(query, userId, cancellationToken);
			var semanticTasks = await SearchTasksSemanticAsync(query, userId, cancellationToken);

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

		public async global::System.Threading.Tasks.Task<List<TaskItem>> SearchTasksAsync(string query, string? userId = null, CancellationToken cancellationToken = default)
		{
			return await SearchTasksHybridAsync(query, userId, cancellationToken);
		}

		public async global::System.Threading.Tasks.Task ClearAllTasksAsync(string? userId = null, CancellationToken cancellationToken = default)
		{
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = $"UPDATE tasks SET archived = @archived, archived_at = @archivedAt WHERE {ActiveTasksPredicate}";
			AppendUserScope(command, "user_id", userId);
			AddParameter(command, "@archived", UsesPostgres ? true : 1);
			AddParameter(command, "@archivedAt", FormatStoredDateTime(DateTime.UtcNow));
			await command.ExecuteNonQueryAsync(cancellationToken);
		}

		private bool UsesPostgres => _settings.Provider == DatabaseProviders.Postgres;

		private string ActiveTasksPredicate => UsesPostgres ? "archived = FALSE" : "archived = 0";

		// ------------------------------------------------------------------------
		// Auth persistence (users + api_keys). Shared by the API, the CLI's
		// `task users create`, and tests. Same dual-provider pattern as tasks.
		// ------------------------------------------------------------------------

		public async global::System.Threading.Tasks.Task<UserRecord?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken = default)
		{
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT id, username, password_hash, is_admin, created_at, disabled_at FROM users WHERE LOWER(username) = LOWER(@username) LIMIT 1";
			AddParameter(command, "@username", username);

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			return await reader.ReadAsync(cancellationToken) ? ReadUser(reader) : null;
		}

		public async global::System.Threading.Tasks.Task<UserRecord?> FindUserByIdAsync(string id, CancellationToken cancellationToken = default)
		{
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT id, username, password_hash, is_admin, created_at, disabled_at FROM users WHERE id = @id LIMIT 1";
			AddParameter(command, "@id", id);

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			return await reader.ReadAsync(cancellationToken) ? ReadUser(reader) : null;
		}

		public async global::System.Threading.Tasks.Task<List<UserRecord>> GetAllUsersAsync(CancellationToken cancellationToken = default)
		{
			var users = new List<UserRecord>();
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT id, username, password_hash, is_admin, created_at, disabled_at FROM users ORDER BY created_at ASC, username ASC";

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				users.Add(ReadUser(reader));
			}

			return users;
		}

		public async global::System.Threading.Tasks.Task<int> CountUsersAsync(CancellationToken cancellationToken = default)
		{
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT COUNT(*) FROM users";
			var scalar = await command.ExecuteScalarAsync(cancellationToken);
			return Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Creates a user inside a transaction. The COUNT check and the insert run under
		/// a write lock so two concurrent signups cannot both become the first (admin) user.
		/// Tasks whose assignee matches the username (case-insensitive, exact) are claimed
		/// in the same transaction. Returns null when the username is already taken.
		/// </summary>
		public async global::System.Threading.Tasks.Task<(UserRecord User, bool IsFirstUser)?> CreateUserAndClaimTasksAsync(
			string id,
			string username,
			string passwordHash,
			bool forceIsAdmin,
			CancellationToken cancellationToken = default)
		{
			await using var connection = await OpenConnectionAsync(cancellationToken);

			// SQLite: take the write lock up-front so the count+insert is atomic.
			// Postgres: lock the users table in the transaction for the same reason.
			await using var transaction = connection is SqliteConnection sqlite
				? sqlite.BeginTransaction(deferred: false)
				: await connection.BeginTransactionAsync(cancellationToken);

			try
			{
				if (UsesPostgres)
				{
					await using var lockCommand = connection.CreateCommand();
					lockCommand.Transaction = transaction;
					lockCommand.CommandText = "LOCK TABLE users IN SHARE ROW EXCLUSIVE MODE";
					await lockCommand.ExecuteNonQueryAsync(cancellationToken);
				}

				await using var duplicateCommand = connection.CreateCommand();
				duplicateCommand.Transaction = transaction;
				duplicateCommand.CommandText = "SELECT COUNT(*) FROM users WHERE LOWER(username) = LOWER(@username)";
				AddParameter(duplicateCommand, "@username", username);
				var duplicateCount = Convert.ToInt32(await duplicateCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
				if (duplicateCount > 0)
				{
					await transaction.RollbackAsync(cancellationToken);
					return null;
				}

				await using var countCommand = connection.CreateCommand();
				countCommand.Transaction = transaction;
				countCommand.CommandText = "SELECT COUNT(*) FROM users";
				var userCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
				var isFirstUser = userCount == 0;
				var isAdmin = forceIsAdmin || isFirstUser;
				var createdAt = DateTime.UtcNow;

				await using var insertCommand = connection.CreateCommand();
				insertCommand.Transaction = transaction;
				insertCommand.CommandText = "INSERT INTO users (id, username, password_hash, is_admin, created_at, disabled_at) VALUES (@id, @username, @passwordHash, @isAdmin, @createdAt, NULL)";
				AddParameter(insertCommand, "@id", id);
				AddParameter(insertCommand, "@username", username);
				AddParameter(insertCommand, "@passwordHash", passwordHash);
				AddParameter(insertCommand, "@isAdmin", UsesPostgres ? isAdmin : (isAdmin ? 1 : 0));
				AddParameter(insertCommand, "@createdAt", FormatStoredDateTime(createdAt));
				await insertCommand.ExecuteNonQueryAsync(cancellationToken);

				var claimed = await ClaimTasksByAssigneeAsync(connection, transaction, username, id, cancellationToken);

				await transaction.CommitAsync(cancellationToken);

				return (new UserRecord
				{
					Id = id,
					Username = username,
					PasswordHash = passwordHash,
					IsAdmin = isAdmin,
					CreatedAt = createdAt,
					DisabledAt = null
				}, isFirstUser);
			}
			catch
			{
				await transaction.RollbackAsync(cancellationToken);
				throw;
			}
		}

		private static async global::System.Threading.Tasks.Task<int> ClaimTasksByAssigneeAsync(
			DbConnection connection,
			DbTransaction transaction,
			string username,
			string userId,
			CancellationToken cancellationToken)
		{
			await using var command = connection.CreateCommand();
			command.Transaction = transaction;
			command.CommandText = @"
				UPDATE tasks
				SET user_id = @userId, updated_by = @userId
				WHERE user_id IS NULL
				  AND LOWER(COALESCE(assignee, '')) = LOWER(@username)";
			AddParameter(command, "@userId", userId);
			AddParameter(command, "@username", username);
			return await command.ExecuteNonQueryAsync(cancellationToken);
		}

		public async global::System.Threading.Tasks.Task<ApiKeyRecord?> FindApiKeyByHashAsync(string keyHash, CancellationToken cancellationToken = default)
		{
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT id, user_id, name, key_hash, created_at, last_used_at, revoked_at FROM api_keys WHERE key_hash = @keyHash LIMIT 1";
			AddParameter(command, "@keyHash", keyHash);

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			return await reader.ReadAsync(cancellationToken) ? ReadApiKey(reader) : null;
		}

		public async global::System.Threading.Tasks.Task<ApiKeyRecord> InsertApiKeyAsync(string id, string userId, string name, string keyHash, CancellationToken cancellationToken = default)
		{
			var createdAt = DateTime.UtcNow;
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "INSERT INTO api_keys (id, user_id, name, key_hash, created_at, last_used_at, revoked_at) VALUES (@id, @userId, @name, @keyHash, @createdAt, NULL, NULL)";
			AddParameter(command, "@id", id);
			AddParameter(command, "@userId", userId);
			AddParameter(command, "@name", name);
			AddParameter(command, "@keyHash", keyHash);
			AddParameter(command, "@createdAt", FormatStoredDateTime(createdAt));
			await command.ExecuteNonQueryAsync(cancellationToken);

			return new ApiKeyRecord
			{
				Id = id,
				UserId = userId,
				Name = name,
				KeyHash = keyHash,
				CreatedAt = createdAt,
				LastUsedAt = null,
				RevokedAt = null
			};
		}

		public async global::System.Threading.Tasks.Task<List<ApiKeyRecord>> GetApiKeysForUserAsync(string userId, CancellationToken cancellationToken = default)
		{
			var keys = new List<ApiKeyRecord>();
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT id, user_id, name, key_hash, created_at, last_used_at, revoked_at FROM api_keys WHERE user_id = @userId ORDER BY created_at ASC";
			AddParameter(command, "@userId", userId);

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				keys.Add(ReadApiKey(reader));
			}

			return keys;
		}

		public async global::System.Threading.Tasks.Task<List<ApiKeyRecord>> GetAllApiKeysAsync(CancellationToken cancellationToken = default)
		{
			var keys = new List<ApiKeyRecord>();
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT id, user_id, name, key_hash, created_at, last_used_at, revoked_at FROM api_keys ORDER BY created_at ASC";

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				keys.Add(ReadApiKey(reader));
			}

			return keys;
		}

		/// <summary>Revokes a key. When userId is set, only that user's key can be revoked.</summary>
		public async global::System.Threading.Tasks.Task<bool> RevokeApiKeyAsync(string id, string? userId, CancellationToken cancellationToken = default)
		{
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "UPDATE api_keys SET revoked_at = @revokedAt WHERE id = @id AND revoked_at IS NULL";
			AddParameter(command, "@id", id);
			AddParameter(command, "@revokedAt", FormatStoredDateTime(DateTime.UtcNow));
			AppendUserScope(command, "user_id", userId);
			var affected = await command.ExecuteNonQueryAsync(cancellationToken);
			return affected > 0;
		}

		public async global::System.Threading.Tasks.Task UpdateApiKeyLastUsedAsync(string id, CancellationToken cancellationToken = default)
		{
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "UPDATE api_keys SET last_used_at = @lastUsedAt WHERE id = @id";
			AddParameter(command, "@id", id);
			AddParameter(command, "@lastUsedAt", FormatStoredDateTime(DateTime.UtcNow));
			await command.ExecuteNonQueryAsync(cancellationToken);
		}

		public async global::System.Threading.Tasks.Task SetUserDisabledAsync(string userId, DateTime? disabledAt, CancellationToken cancellationToken = default)
		{
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "UPDATE users SET disabled_at = @disabledAt WHERE id = @id";
			AddParameter(command, "@id", userId);
			AddParameter(command, "@disabledAt", disabledAt.HasValue ? FormatStoredDateTime(disabledAt.Value) : DBNull.Value);
			await command.ExecuteNonQueryAsync(cancellationToken);
		}

		public async global::System.Threading.Tasks.Task ClearAuthTablesAsync(CancellationToken cancellationToken = default)
		{
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "DELETE FROM api_keys; DELETE FROM users;";
			await command.ExecuteNonQueryAsync(cancellationToken);
		}

		private static UserRecord ReadUser(DbDataReader reader)
		{
			return new UserRecord
			{
				Id = reader.GetString(0),
				Username = reader.GetString(1),
				PasswordHash = reader.GetString(2),
				IsAdmin = ReadArchivedFlag(reader, 3),
				CreatedAt = ParseRequiredStoredDateTime(reader, 4),
				DisabledAt = ParseOptionalStoredDateTime(reader, 5)
			};
		}

		private static ApiKeyRecord ReadApiKey(DbDataReader reader)
		{
			return new ApiKeyRecord
			{
				Id = reader.GetString(0),
				UserId = reader.GetString(1),
				Name = reader.GetString(2),
				KeyHash = reader.GetString(3),
				CreatedAt = ParseRequiredStoredDateTime(reader, 4),
				LastUsedAt = ParseOptionalStoredDateTime(reader, 5),
				RevokedAt = ParseOptionalStoredDateTime(reader, 6)
			};
		}

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

		private async global::System.Threading.Tasks.Task<List<TaskItem>> SearchTasksSqliteAsync(string query, string? userId, CancellationToken cancellationToken)
		{
			var tasks = new List<TaskItem>();
			await using var connection = await OpenConnectionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = @"
				SELECT t.id, t.uid, t.title, t.description, t.priority, t.due_date, t.tags, t.project, t.assignee, t.status, t.block_reason, t.created_at, t.updated_at, t.archived, t.archived_at
				FROM tasks_fts fts
				JOIN tasks t ON t.id = fts.rowid
				WHERE tasks_fts MATCH @query AND t.archived = 0";
			if (userId != null)
			{
				command.CommandText += " AND t.user_id = @userId";
			}
			command.CommandText += "\n\t\t\t\tORDER BY rank";
			AddParameter(command, "@query", query);
			AddParameter(command, "@userId", userId);

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				tasks.Add(ReadTask(reader));
			}

			return tasks;
		}

		private async global::System.Threading.Tasks.Task<List<TaskItem>> SearchTasksPostgresAsync(string query, string? userId, CancellationToken cancellationToken)
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
						OR COALESCE(tags, '') ILIKE @query)";
			if (userId != null)
			{
				command.CommandText += "\n\t\t\t\t  AND user_id = @userId";
			}
			command.CommandText += "\n\t\t\t\tORDER BY created_at DESC";
			AddParameter(command, "@query", $"%{query}%");
			AddParameter(command, "@userId", userId);

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
			DateTime updatedAt,
			string? userId)
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
			AddParameter(command, "@userId", userId);
		}

		private static void AppendUserScope(DbCommand command, string column, string? userId)
		{
			if (userId == null)
			{
				return;
			}

			command.CommandText += $" AND {column} = @userId";
			AddParameter(command, "@userId", userId);
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
			RunSqliteMigration(connection, "user_id", "ALTER TABLE tasks ADD COLUMN user_id TEXT");
			RunSqliteMigration(connection, "created_by", "ALTER TABLE tasks ADD COLUMN created_by TEXT");
			RunSqliteMigration(connection, "updated_by", "ALTER TABLE tasks ADD COLUMN updated_by TEXT");
		}

		private static async global::System.Threading.Tasks.Task RunSqliteMigrationsAsync(SqliteConnection connection, CancellationToken cancellationToken)
		{
			await RunSqliteMigrationAsync(connection, "project", "ALTER TABLE tasks ADD COLUMN project TEXT", cancellationToken);
			await RunSqliteMigrationAsync(connection, "assignee", "ALTER TABLE tasks ADD COLUMN assignee TEXT", cancellationToken);
			await RunSqliteMigrationAsync(connection, "archived", "ALTER TABLE tasks ADD COLUMN archived INTEGER NOT NULL DEFAULT 0", cancellationToken);
			await RunSqliteMigrationAsync(connection, "archived_at", "ALTER TABLE tasks ADD COLUMN archived_at TEXT", cancellationToken);
			await RunSqliteMigrationAsync(connection, "block_reason", "ALTER TABLE tasks ADD COLUMN block_reason TEXT", cancellationToken);
			await RunSqliteMigrationAsync(connection, "user_id", "ALTER TABLE tasks ADD COLUMN user_id TEXT", cancellationToken);
			await RunSqliteMigrationAsync(connection, "created_by", "ALTER TABLE tasks ADD COLUMN created_by TEXT", cancellationToken);
			await RunSqliteMigrationAsync(connection, "updated_by", "ALTER TABLE tasks ADD COLUMN updated_by TEXT", cancellationToken);
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
				archived_at TEXT,
				user_id TEXT,
				created_by TEXT,
				updated_by TEXT
			);
			CREATE TABLE IF NOT EXISTS users (
				id TEXT PRIMARY KEY,
				username TEXT NOT NULL COLLATE NOCASE UNIQUE,
				password_hash TEXT NOT NULL,
				is_admin INTEGER NOT NULL DEFAULT 0,
				created_at TEXT NOT NULL,
				disabled_at TEXT
			);
			CREATE INDEX IF NOT EXISTS idx_users_username ON users(username);
			CREATE TABLE IF NOT EXISTS api_keys (
				id TEXT PRIMARY KEY,
				user_id TEXT NOT NULL,
				name TEXT NOT NULL,
				key_hash TEXT NOT NULL UNIQUE,
				created_at TEXT NOT NULL,
				last_used_at TEXT,
				revoked_at TEXT
			);
			CREATE INDEX IF NOT EXISTS idx_api_keys_user_id ON api_keys(user_id);
			CREATE INDEX IF NOT EXISTS idx_api_keys_key_hash ON api_keys(key_hash);
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
			ALTER TABLE tasks ADD COLUMN IF NOT EXISTS user_id TEXT;
			ALTER TABLE tasks ADD COLUMN IF NOT EXISTS created_by TEXT;
			ALTER TABLE tasks ADD COLUMN IF NOT EXISTS updated_by TEXT;
			CREATE TABLE IF NOT EXISTS users (
				id TEXT PRIMARY KEY,
				username TEXT NOT NULL UNIQUE,
				password_hash TEXT NOT NULL,
				is_admin BOOLEAN NOT NULL DEFAULT FALSE,
				created_at TEXT NOT NULL,
				disabled_at TEXT
			);
			CREATE UNIQUE INDEX IF NOT EXISTS idx_users_username_lower ON users (LOWER(username));
			CREATE TABLE IF NOT EXISTS api_keys (
				id TEXT PRIMARY KEY,
				user_id TEXT NOT NULL,
				name TEXT NOT NULL,
				key_hash TEXT NOT NULL UNIQUE,
				created_at TEXT NOT NULL,
				last_used_at TEXT,
				revoked_at TEXT
			);
			CREATE INDEX IF NOT EXISTS idx_api_keys_user_id ON api_keys(user_id);
			CREATE INDEX IF NOT EXISTS idx_api_keys_key_hash ON api_keys(key_hash);
			CREATE INDEX IF NOT EXISTS idx_tasks_uid ON tasks(uid);
			CREATE INDEX IF NOT EXISTS idx_tasks_status ON tasks(status);
			CREATE INDEX IF NOT EXISTS idx_tasks_priority ON tasks(priority);
			CREATE INDEX IF NOT EXISTS idx_tasks_due_date ON tasks(due_date);
			CREATE INDEX IF NOT EXISTS idx_tasks_created_at ON tasks(created_at);
			CREATE INDEX IF NOT EXISTS idx_tasks_project ON tasks(project);
			CREATE INDEX IF NOT EXISTS idx_tasks_assignee ON tasks(assignee);";
	}
}
