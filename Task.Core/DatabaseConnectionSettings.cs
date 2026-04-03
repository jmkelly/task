using System;

namespace Task.Core
{
	public sealed class DatabaseConnectionSettings
	{
		private DatabaseConnectionSettings(string provider, string sqliteDatabasePath, string? postgresConnectionString)
		{
			Provider = provider;
			SqliteDatabasePath = sqliteDatabasePath;
			PostgresConnectionString = postgresConnectionString;
		}

		public string Provider { get; }
		public string SqliteDatabasePath { get; }
		public string? PostgresConnectionString { get; }

		public static DatabaseConnectionSettings Create(
			string? provider,
			string? sqliteDatabasePath,
			string? postgresConnectionString,
			string? configHomeOverride = null,
			string? userHomeOverride = null)
		{
			var normalizedProvider = DatabaseProviders.Normalize(provider);
			var resolvedSqliteDatabasePath = TaskPaths.ResolveDatabasePath(
				sqliteDatabasePath,
				configHomeOverride,
				userHomeOverride);

			if (normalizedProvider == DatabaseProviders.Postgres && string.IsNullOrWhiteSpace(postgresConnectionString))
			{
				throw new ArgumentException("postgres.connectionString must be set when database.provider is 'pg'");
			}

			return new DatabaseConnectionSettings(
				normalizedProvider,
				resolvedSqliteDatabasePath,
				string.IsNullOrWhiteSpace(postgresConnectionString) ? null : postgresConnectionString);
		}

		public static DatabaseConnectionSettings ForSqlite(string? sqliteDatabasePath)
		{
			return Create(DatabaseProviders.Sqlite, sqliteDatabasePath, null);
		}
	}
}
