using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ST = global::System.Threading.Tasks;
using Npgsql;

namespace Task.Core.Providers.Postgres
{
    public sealed class PostgresTaskService : ITaskService
    {
        private static readonly HashSet<string> AllowedSortColumns = new(StringComparer.OrdinalIgnoreCase)
        {
            "title",
            "priority",
            "due_date",
            "created_at",
            "updated_at"
        };

        private readonly PostgresDatabase _db;

        public PostgresTaskService(string? connStr = null)
        {
            _db = new PostgresDatabase(connStr);
        }

        public async ST.Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await _db.InitSchemaAsync(cancellationToken);
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
            var sql = new StringBuilder($@"
                SELECT {SelectColumns}
                FROM tasks
                WHERE archived = FALSE");
            var parameters = new List<NpgsqlParameter>();

            AddCaseInsensitiveEqualsFilter(sql, parameters, "status", status, "status");
            AddCaseInsensitiveEqualsFilter(sql, parameters, "priority", priority, "priority");
            AddCaseInsensitiveEqualsFilter(sql, parameters, "project", project, "project");
            AddCaseInsensitiveEqualsFilter(sql, parameters, "assignee", assignee, "assignee");

            if (!string.IsNullOrWhiteSpace(tags))
            {
                var tagValues = ParseDelimitedValues(tags);
                if (tagValues.Count > 0)
                {
                    sql.Append(" AND (");
                    for (var index = 0; index < tagValues.Count; index++)
                    {
                        if (index > 0)
                        {
                            sql.Append(" OR ");
                        }

                        var parameterName = $"tag_{index}";
                        sql.Append($@"EXISTS (
                            SELECT 1
                            FROM unnest(string_to_array(COALESCE(tags, ''), ',')) AS tag_value
                            WHERE LOWER(BTRIM(tag_value)) = LOWER(@{parameterName})
                        )");
                        parameters.Add(new NpgsqlParameter(parameterName, tagValues[index]));
                    }
                    sql.Append(')');
                }
            }

            if (dueBefore.HasValue)
            {
                sql.Append(" AND due_date IS NOT NULL AND due_date <= @due_before");
                parameters.Add(new NpgsqlParameter("due_before", dueBefore.Value.Date));
            }

            if (dueAfter.HasValue)
            {
                sql.Append(" AND due_date IS NOT NULL AND due_date >= @due_after");
                parameters.Add(new NpgsqlParameter("due_after", dueAfter.Value.Date));
            }

            var orderByColumn = NormalizeSortColumn(sortBy);
            var orderByDirection = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
            sql.Append($" ORDER BY {orderByColumn} {orderByDirection}, id ASC");

            if (limit.HasValue)
            {
                sql.Append(" LIMIT @limit");
                parameters.Add(new NpgsqlParameter("limit", limit.Value));
            }

            if (offset.HasValue)
            {
                sql.Append(" OFFSET @offset");
                parameters.Add(new NpgsqlParameter("offset", offset.Value));
            }

            return await ReadTasksAsync(sql.ToString(), parameters, cancellationToken);
        }

        public async ST.Task<TaskItem?> GetTaskByUidAsync(string uid, CancellationToken cancellationToken = default)
        {
            var sql = $@"
                SELECT {SelectColumns}
                FROM tasks
                WHERE uid = @uid AND archived = FALSE
                LIMIT 1";

            await using var reader = await _db.ExecuteReaderAsync(
                sql,
                new List<NpgsqlParameter> { new("uid", uid) },
                cancellationToken);

            return await reader.ReadAsync(cancellationToken)
                ? MapTaskItem(reader)
                : null;
        }

        public async ST.Task<TaskItem> AddTaskAsync(
            string uid,
            string title,
            string? description,
            string priority,
            DateTime? dueDate,
            List<string> tags,
            string? project = null,
            List<string>? dependsOn = null,
            string? assignee = null,
            string status = "todo",
            string? blockReason = null,
            CancellationToken cancellationToken = default)
        {
            ValidateBlockReason(status, blockReason);

            var normalizedTags = ParseDelimitedValues(tags);
            var normalizedDependencies = ParseDelimitedValues(dependsOn);
            var now = DateTime.UtcNow;

            const string sql = @"
                INSERT INTO tasks (
                    uid,
                    title,
                    description,
                    priority,
                    due_date,
                    tags,
                    project,
                    assignee,
                    depends_on,
                    status,
                    block_reason,
                    created_at,
                    updated_at,
                    archived,
                    archived_at)
                VALUES (
                    @uid,
                    @title,
                    @description,
                    @priority,
                    @due_date,
                    @tags,
                    @project,
                    @assignee,
                    @depends_on,
                    @status,
                    @block_reason,
                    @created_at,
                    @updated_at,
                    FALSE,
                    NULL)
                RETURNING id;";

            var parameters = new List<NpgsqlParameter>
            {
                new("uid", uid),
                new("title", title),
                new("description", ToDbValue(description)),
                new("priority", priority),
                new("due_date", ToDbValue(dueDate?.Date)),
                new("tags", ToDbValue(ToDelimitedString(normalizedTags))),
                new("project", ToDbValue(project)),
                new("assignee", ToDbValue(assignee)),
                new("depends_on", ToDbValue(ToDelimitedString(normalizedDependencies))),
                new("status", status),
                new("block_reason", ToDbValue(blockReason)),
                new("created_at", now),
                new("updated_at", now)
            };

            var insertedId = await _db.ExecuteScalarAsync(sql, parameters, cancellationToken);

            return new TaskItem
            {
                Id = insertedId is null ? 0 : Convert.ToInt32(insertedId),
                Uid = uid,
                Title = title,
                Description = description,
                Priority = priority,
                DueDate = dueDate?.Date,
                Tags = normalizedTags,
                Project = project,
                Assignee = assignee,
                DependsOn = normalizedDependencies,
                Status = status,
                BlockReason = blockReason,
                CreatedAt = now,
                UpdatedAt = now,
                Archived = false,
                ArchivedAt = null
            };
        }

        public async ST.Task UpdateTaskAsync(TaskItem task, CancellationToken cancellationToken = default)
        {
            ValidateBlockReason(task.Status, task.BlockReason);

            var now = DateTime.UtcNow;
            task.UpdatedAt = now;

            const string sql = @"
                UPDATE tasks
                SET title = @title,
                    description = @description,
                    priority = @priority,
                    due_date = @due_date,
                    tags = @tags,
                    project = @project,
                    assignee = @assignee,
                    depends_on = @depends_on,
                    status = @status,
                    block_reason = @block_reason,
                    updated_at = @updated_at,
                    archived = @archived,
                    archived_at = @archived_at
                WHERE uid = @uid";

            var parameters = new List<NpgsqlParameter>
            {
                new("uid", task.Uid),
                new("title", task.Title),
                new("description", ToDbValue(task.Description)),
                new("priority", task.Priority),
                new("due_date", ToDbValue(task.DueDate?.Date)),
                new("tags", ToDbValue(ToDelimitedString(ParseDelimitedValues(task.Tags)))),
                new("project", ToDbValue(task.Project)),
                new("assignee", ToDbValue(task.Assignee)),
                new("depends_on", ToDbValue(ToDelimitedString(ParseDelimitedValues(task.DependsOn)))),
                new("status", task.Status),
                new("block_reason", ToDbValue(task.BlockReason)),
                new("updated_at", now),
                new("archived", task.Archived),
                new("archived_at", ToDbValue(task.ArchivedAt)),
            };

            await _db.ExecuteNonQueryAsync(sql, parameters, cancellationToken);
        }

        public async ST.Task DeleteTaskAsync(string uid, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                UPDATE tasks
                SET archived = TRUE,
                    archived_at = @archived_at,
                    updated_at = @updated_at
                WHERE uid = @uid AND archived = FALSE";

            var now = DateTime.UtcNow;
            await _db.ExecuteNonQueryAsync(
                sql,
                new List<NpgsqlParameter>
                {
                    new("uid", uid),
                    new("archived_at", now),
                    new("updated_at", now)
                },
                cancellationToken);
        }

        public async ST.Task CompleteTaskAsync(string uid, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                UPDATE tasks
                SET status = 'done',
                    block_reason = NULL,
                    updated_at = @updated_at
                WHERE uid = @uid AND archived = FALSE";

            await _db.ExecuteNonQueryAsync(
                sql,
                new List<NpgsqlParameter>
                {
                    new("uid", uid),
                    new("updated_at", DateTime.UtcNow)
                },
                cancellationToken);
        }

        public async ST.Task<List<TaskItem>> SearchTasksAsync(string query, string type = "fts", CancellationToken cancellationToken = default)
        {
            var terms = ParseSearchTerms(query);
            if (terms.Count == 0)
            {
                return new List<TaskItem>();
            }

            var sql = new StringBuilder($@"
                SELECT {SelectColumns}
                FROM tasks
                WHERE archived = FALSE");
            var parameters = new List<NpgsqlParameter>();

            for (var index = 0; index < terms.Count; index++)
            {
                var parameterName = $"search_{index}";
                sql.Append($@"
                    AND (
                        COALESCE(title, '') ILIKE @{parameterName} ESCAPE '\\'
                        OR COALESCE(description, '') ILIKE @{parameterName} ESCAPE '\\'
                        OR COALESCE(tags, '') ILIKE @{parameterName} ESCAPE '\\'
                    )");
                parameters.Add(new NpgsqlParameter(parameterName, $"%{EscapeLikePattern(terms[index])}%"));
            }

            sql.Append(" ORDER BY updated_at DESC, created_at DESC, id ASC");
            return await ReadTasksAsync(sql.ToString(), parameters, cancellationToken);
        }

        public async ST.Task<List<string>> GetAllUniqueTagsAsync(CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT tags
                FROM tasks
                WHERE archived = FALSE
                  AND tags IS NOT NULL
                  AND tags <> ''";

            var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await using var reader = await _db.ExecuteReaderAsync(sql, cancellationToken: cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                foreach (var tag in ParseDelimitedValues(ReadNullableString(reader, "tags")))
                {
                    values.Add(tag);
                }
            }

            return values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public async ST.Task<List<string>> GetAllUniqueProjectsAsync(CancellationToken cancellationToken = default)
        {
            return await ReadUniqueScalarValuesAsync("project", cancellationToken);
        }

        public async ST.Task<List<string>> GetAllUniqueAssigneesAsync(CancellationToken cancellationToken = default)
        {
            return await ReadUniqueScalarValuesAsync("assignee", cancellationToken);
        }

        public async ST.Task<List<TaskItem>> GetTasksDependingOnAsync(string uid, CancellationToken cancellationToken = default)
        {
            var sql = $@"
                SELECT {SelectColumns}
                FROM tasks
                WHERE archived = FALSE
                  AND depends_on IS NOT NULL
                  AND depends_on <> ''
                ORDER BY created_at ASC, id ASC";

            var candidates = await ReadTasksAsync(sql, null, cancellationToken);
            return candidates
                .Where(task => task.DependsOn.Contains(uid, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        public async ST.Task<bool> ValidateDependenciesAsync(string uid, List<string> dependsOn, CancellationToken cancellationToken = default)
        {
            var dependencyUids = ParseDelimitedValues(dependsOn);
            if (dependencyUids.Count == 0)
            {
                return true;
            }

            for (var index = 0; index < dependencyUids.Count; index++)
            {
                const string sqlPrefix = @"
                    SELECT 1
                    FROM tasks
                    WHERE uid = @uid
                      AND archived = FALSE
                    LIMIT 1";

                var exists = await _db.ExecuteScalarAsync(
                    sqlPrefix,
                    new List<NpgsqlParameter> { new("uid", dependencyUids[index]) },
                    cancellationToken);

                if (exists == null)
                {
                    return false;
                }
            }

            return true;
        }

        public async ST.Task ArchiveAllTasksAsync(CancellationToken cancellationToken = default)
        {
            const string sql = @"
                UPDATE tasks
                SET archived = TRUE,
                    archived_at = @archived_at,
                    updated_at = @updated_at
                WHERE archived = FALSE";

            var now = DateTime.UtcNow;
            await _db.ExecuteNonQueryAsync(
                sql,
                new List<NpgsqlParameter>
                {
                    new("archived_at", now),
                    new("updated_at", now)
                },
                cancellationToken);
        }

        private async ST.Task<List<TaskItem>> ReadTasksAsync(string sql, IList<NpgsqlParameter>? parameters, CancellationToken cancellationToken)
        {
            var tasks = new List<TaskItem>();

            await using var reader = await _db.ExecuteReaderAsync(sql, parameters, cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                tasks.Add(MapTaskItem(reader));
            }

            return tasks;
        }

        private async ST.Task<List<string>> ReadUniqueScalarValuesAsync(string columnName, CancellationToken cancellationToken)
        {
            columnName = NormalizeUniqueValueColumn(columnName);

            var sql = $@"
                SELECT DISTINCT {columnName}
                FROM tasks
                WHERE archived = FALSE
                  AND {columnName} IS NOT NULL
                  AND {columnName} <> ''
                ORDER BY {columnName} ASC";

            var values = new List<string>();
            await using var reader = await _db.ExecuteReaderAsync(sql, cancellationToken: cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                values.Add(reader.GetString(0));
            }

            return values;
        }

        private static TaskItem MapTaskItem(NpgsqlDataReader reader)
        {
            return new TaskItem
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                Uid = reader.GetString(reader.GetOrdinal("uid")),
                Title = reader.GetString(reader.GetOrdinal("title")),
                Description = ReadNullableString(reader, "description"),
                Priority = reader.GetString(reader.GetOrdinal("priority")),
                DueDate = ReadNullableDate(reader, "due_date"),
                Tags = ParseDelimitedValues(ReadNullableString(reader, "tags")),
                Project = ReadNullableString(reader, "project"),
                Assignee = ReadNullableString(reader, "assignee"),
                DependsOn = ParseDelimitedValues(ReadNullableString(reader, "depends_on")),
                Status = reader.GetString(reader.GetOrdinal("status")),
                BlockReason = ReadNullableString(reader, "block_reason"),
                CreatedAt = ReadRequiredDateTime(reader, "created_at"),
                UpdatedAt = ReadRequiredDateTime(reader, "updated_at"),
                Archived = reader.GetBoolean(reader.GetOrdinal("archived")),
                ArchivedAt = ReadNullableDateTime(reader, "archived_at")
            };
        }

        private static string NormalizeSortColumn(string? sortBy)
        {
            var normalized = sortBy?.Trim().ToLowerInvariant() switch
            {
                "duedate" or "due_date" => "due_date",
                "createdat" or "created_at" => "created_at",
                "updatedat" or "updated_at" => "updated_at",
                "title" => "title",
                "priority" => "priority",
                _ => "created_at"
            };

            return AllowedSortColumns.Contains(normalized) ? normalized : "created_at";
        }

        private static string NormalizeUniqueValueColumn(string columnName)
        {
            return columnName switch
            {
                "project" => "project",
                "assignee" => "assignee",
                _ => throw new ArgumentOutOfRangeException(nameof(columnName), columnName, "Unsupported unique-value column.")
            };
        }

        private static void AddCaseInsensitiveEqualsFilter(
            StringBuilder sql,
            ICollection<NpgsqlParameter> parameters,
            string columnName,
            string? value,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            sql.Append($" AND LOWER(COALESCE({columnName}, '')) = LOWER(@{parameterName})");
            parameters.Add(new NpgsqlParameter(parameterName, value));
        }

        private static List<string> ParseSearchTerms(string query)
        {
            return query
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> ParseDelimitedValues(IEnumerable<string>? values)
        {
            if (values == null)
            {
                return new List<string>();
            }

            return values
                .SelectMany(value => (value ?? string.Empty).Split(',', StringSplitOptions.TrimEntries))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> ParseDelimitedValues(string? values)
        {
            if (string.IsNullOrWhiteSpace(values))
            {
                return new List<string>();
            }

            return ParseDelimitedValues(new[] { values });
        }

        private static string? ToDelimitedString(List<string> values)
        {
            return values.Count == 0 ? null : string.Join(',', values);
        }

        private static object ToDbValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
        }

        private static object ToDbValue(DateTime? value)
        {
            return value.HasValue ? value.Value : DBNull.Value;
        }

        private static string? ReadNullableString(NpgsqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        private static DateTime? ReadNullableDate(NpgsqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
        }

        private static DateTime ReadRequiredDateTime(NpgsqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.GetDateTime(ordinal);
        }

        private static DateTime? ReadNullableDateTime(NpgsqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
        }

        private static string EscapeLikePattern(string value)
        {
            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
        }

        private static void ValidateBlockReason(string status, string? blockReason)
        {
            if (!string.IsNullOrWhiteSpace(blockReason)
                && !string.Equals(status, "blocked", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Block reason is only allowed when status is blocked.");
            }
        }

        private const string SelectColumns = @"
            id,
            uid,
            title,
            description,
            priority,
            due_date,
            tags,
            project,
            assignee,
            depends_on,
            status,
            block_reason,
            created_at,
            updated_at,
            archived,
            archived_at";
    }
}
