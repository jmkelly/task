using System;

namespace Task.Core
{
	public static class DatabaseProviders
	{
		public const string Sqlite = "sqlite";
		public const string Postgres = "pg";

		public static string Normalize(string? provider)
		{
			if (string.IsNullOrWhiteSpace(provider))
			{
				return Sqlite;
			}

			return provider.Trim().ToLowerInvariant() switch
			{
				Sqlite => Sqlite,
				Postgres => Postgres,
				"postgres" => Postgres,
				"postgresql" => Postgres,
				_ => throw new ArgumentException("database.provider must be 'sqlite' or 'pg'")
			};
		}

		public static bool IsSupported(string? provider)
		{
			try
			{
				Normalize(provider);
				return true;
			}
			catch (ArgumentException)
			{
				return false;
			}
		}
	}
}
