using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using ST = global::System.Threading.Tasks;
using Npgsql;

namespace Task.Core.Providers.Postgres
{
    public sealed class PostgresDatabase : IAsyncDisposable
    {
        private readonly string _connectionString;
        private readonly NpgsqlDataSource _dataSource;

        public PostgresDatabase(string? connStr = null)
        {
            _connectionString = connStr ?? GetConnectionStringFromEnv();
            _dataSource = NpgsqlDataSource.Create(_connectionString);
        }

        public async ValueTask DisposeAsync()
        {
            await _dataSource.DisposeAsync();
        }

        public async ST.Task InitSchemaAsync(CancellationToken cancellationToken = default)
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS tasks (
                    id SERIAL PRIMARY KEY,
                    uid TEXT NOT NULL UNIQUE,
                    title TEXT NOT NULL,
                    description TEXT,
                    priority TEXT NOT NULL,
                    due_date DATE,
                    tags TEXT,
                    project TEXT,
                    assignee TEXT,
                    depends_on TEXT,
                    status TEXT NOT NULL,
                    block_reason TEXT,
                    created_at TIMESTAMPTZ NOT NULL,
                    updated_at TIMESTAMPTZ NOT NULL,
                    archived BOOLEAN NOT NULL DEFAULT FALSE,
                    archived_at TIMESTAMPTZ
                );

                ALTER TABLE IF EXISTS tasks ALTER COLUMN uid TYPE TEXT USING uid::text;
                ALTER TABLE IF EXISTS tasks ADD COLUMN IF NOT EXISTS project TEXT;
                ALTER TABLE IF EXISTS tasks ADD COLUMN IF NOT EXISTS assignee TEXT;
                ALTER TABLE IF EXISTS tasks ADD COLUMN IF NOT EXISTS depends_on TEXT;
                ALTER TABLE IF EXISTS tasks ADD COLUMN IF NOT EXISTS status TEXT;
                ALTER TABLE IF EXISTS tasks ADD COLUMN IF NOT EXISTS block_reason TEXT;
                ALTER TABLE IF EXISTS tasks ADD COLUMN IF NOT EXISTS archived BOOLEAN NOT NULL DEFAULT FALSE;
                ALTER TABLE IF EXISTS tasks ADD COLUMN IF NOT EXISTS archived_at TIMESTAMPTZ;

                UPDATE tasks
                SET status = COALESCE(NULLIF(status, ''), 'todo')
                WHERE status IS NULL OR status = '';

                CREATE INDEX IF NOT EXISTS idx_tasks_uid ON tasks(uid);
                CREATE INDEX IF NOT EXISTS idx_tasks_status ON tasks(status);
                CREATE INDEX IF NOT EXISTS idx_tasks_priority ON tasks(priority);
                CREATE INDEX IF NOT EXISTS idx_tasks_due_date ON tasks(due_date);
                CREATE INDEX IF NOT EXISTS idx_tasks_created_at ON tasks(created_at);
                CREATE INDEX IF NOT EXISTS idx_tasks_updated_at ON tasks(updated_at);
                CREATE INDEX IF NOT EXISTS idx_tasks_project ON tasks(project);
                CREATE INDEX IF NOT EXISTS idx_tasks_assignee ON tasks(assignee);
                CREATE INDEX IF NOT EXISTS idx_tasks_archived ON tasks(archived);
            ";

            await using var cmd = _dataSource.CreateCommand(sql);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async ST.Task<NpgsqlDataReader> ExecuteReaderAsync(
            string sql,
            IList<NpgsqlParameter>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            var command = new NpgsqlCommand(sql, connection);
            AddParameters(command, parameters);

            try
            {
                return await command.ExecuteReaderAsync(CommandBehavior.CloseConnection, cancellationToken);
            }
            catch
            {
                await command.DisposeAsync();
                await connection.DisposeAsync();
                throw;
            }
        }

        public async ST.Task<int> ExecuteNonQueryAsync(
            string sql,
            IList<NpgsqlParameter>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            await using var cmd = _dataSource.CreateCommand(sql);
            AddParameters(cmd, parameters);
            return await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async ST.Task<object?> ExecuteScalarAsync(
            string sql,
            IList<NpgsqlParameter>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            await using var cmd = _dataSource.CreateCommand(sql);
            AddParameters(cmd, parameters);
            return await cmd.ExecuteScalarAsync(cancellationToken);
        }

        private static string GetConnectionStringFromEnv()
        {
            return Environment.GetEnvironmentVariable("TASK_PG_CONNECTION_STRING")
                ?? Environment.GetEnvironmentVariable("DATABASE_URL")
                ?? "Host=localhost;Username=postgres;Password=postgres;Database=tasks";
        }

        private static void AddParameters(NpgsqlCommand command, IList<NpgsqlParameter>? parameters)
        {
            if (parameters == null)
            {
                return;
            }

            foreach (var parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }
        }
    }
}
