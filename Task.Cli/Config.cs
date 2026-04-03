using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Task.Core;

namespace Task.Cli
{
	public class Config
	{
		private static readonly string ConfigDir = GetConfigDirectory();
		private static readonly string ConfigFile = Path.Combine(ConfigDir, "config.json");

		public string? ApiUrl { get; set; }
		public string? DefaultOutput { get; set; }
		public DatabaseConfig? Database { get; set; }
		public TelegramConfig? Telegram { get; set; }

		public sealed class DatabaseConfig
		{
			public string? Provider { get; set; }
			public SqliteConfig? Sqlite { get; set; }
			public PostgresConfig? Postgres { get; set; }
		}

		public sealed class SqliteConfig
		{
			public string? Path { get; set; }
		}

		public sealed class PostgresConfig
		{
			public string? ConnectionString { get; set; }
		}

		public sealed class TelegramConfig
		{
			public string? BotToken { get; set; }
			public string? ChatId { get; set; }
		}

		public static Config Load()
		{
			try
			{
				var config = new Config { DefaultOutput = "plain" };
				if (File.Exists(ConfigFile))
				{
					var json = File.ReadAllText(ConfigFile);
					config = JsonSerializer.Deserialize(json, TaskJsonContext.Default.Config) ?? config;
				}

				ApplyEnvironmentOverrides(config);
				NormalizeDefaults(config);
				ValidateLoadedConfig(config);
				return config;
			}
			catch
			{
				return CreateDefault();
			}
		}

		public void Save()
		{
			try
			{
				NormalizeDefaults(this);
				Directory.CreateDirectory(ConfigDir);
				var json = JsonSerializer.Serialize(this, TaskJsonContext.Default.Config);
				File.WriteAllText(ConfigFile, json);
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException($"Failed to save config: {ex.Message}", ex);
			}
		}

		public async System.Threading.Tasks.Task SetValueAsync(string key, string value)
		{
			switch (NormalizeKey(key))
			{
				case "api-url":
					await ValidateUrlAsync(value);
					ApiUrl = value;
					break;
				case "defaultoutput":
					if (value != "json" && value != "plain")
					{
						throw new ArgumentException("defaultOutput must be 'json' or 'plain'");
					}
					DefaultOutput = value;
					break;
				case "database.provider":
					EnsureDatabase().Provider = DatabaseProviders.Normalize(value);
					break;
				case "database.sqlite.path":
					EnsureSqlite().Path = value;
					break;
				case "database.pg.connectionstring":
				case "database.postgres.connectionstring":
					EnsurePostgres().ConnectionString = value;
					break;
				case "telegram.bottoken":
					EnsureTelegram().BotToken = value;
					break;
				case "telegram.chatid":
					EnsureTelegram().ChatId = value;
					break;
				default:
					throw new ArgumentException($"Unknown config key: {key}");
			}

			NormalizeDefaults(this);
		}

		public string? GetValue(string key)
		{
			return NormalizeKey(key) switch
			{
				"api-url" => ApiUrl,
				"defaultoutput" => DefaultOutput,
				"database.provider" => GetDatabaseProvider(),
				"database.sqlite.path" => Database?.Sqlite?.Path,
				"database.pg.connectionstring" => Database?.Postgres?.ConnectionString,
				"database.postgres.connectionstring" => Database?.Postgres?.ConnectionString,
				"telegram.bottoken" => Telegram?.BotToken,
				"telegram.chatid" => Telegram?.ChatId,
				_ => null
			};
		}

		public void UnsetValue(string key)
		{
			switch (NormalizeKey(key))
			{
				case "api-url":
					ApiUrl = null;
					break;
				case "defaultoutput":
					DefaultOutput = "plain";
					break;
				case "database.provider":
					EnsureDatabase().Provider = DatabaseProviders.Sqlite;
					break;
				case "database.sqlite.path":
					if (Database?.Sqlite != null)
					{
						Database.Sqlite.Path = null;
					}
					break;
				case "database.pg.connectionstring":
				case "database.postgres.connectionstring":
					if (Database?.Postgres != null)
					{
						Database.Postgres.ConnectionString = null;
					}
					break;
				case "telegram.bottoken":
					if (Telegram != null)
					{
						Telegram.BotToken = null;
					}
					break;
				case "telegram.chatid":
					if (Telegram != null)
					{
						Telegram.ChatId = null;
					}
					break;
			}

			NormalizeDefaults(this);
		}

		public string GetDatabaseProvider()
		{
			return DatabaseProviders.Normalize(Database?.Provider);
		}

		public DatabaseConnectionSettings ToDatabaseConnectionSettings()
		{
			return DatabaseConnectionSettings.Create(
				provider: Database?.Provider,
				sqliteDatabasePath: Database?.Sqlite?.Path,
				postgresConnectionString: Database?.Postgres?.ConnectionString);
		}

		public static async System.Threading.Tasks.Task ValidateUrlAsync(string url)
		{
			if (!IsValidUrlFormat(url))
			{
				throw new ArgumentException("Invalid URL format. Must be a valid HTTP or HTTPS URL.");
			}

			using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
			try
			{
				using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
			}
			catch (HttpRequestException ex)
			{
				throw new ArgumentException($"URL is not reachable: {ex.Message}");
			}
			catch (TaskCanceledException)
			{
				throw new ArgumentException("URL validation timed out after 5 seconds.");
			}
		}

		private static Config CreateDefault()
		{
			var config = new Config { DefaultOutput = "plain" };
			NormalizeDefaults(config);
			return config;
		}

		private static void ApplyEnvironmentOverrides(Config config)
		{
			var botTokenEnv = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? Environment.GetEnvironmentVariable("Telegram__BotToken");
			var chatIdEnv = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID") ?? Environment.GetEnvironmentVariable("Telegram__ChatId");

			if (!string.IsNullOrEmpty(botTokenEnv))
			{
				config.EnsureTelegram().BotToken = botTokenEnv;
			}

			if (!string.IsNullOrEmpty(chatIdEnv))
			{
				config.EnsureTelegram().ChatId = chatIdEnv;
			}
		}

		private static void NormalizeDefaults(Config config)
		{
			if (string.IsNullOrWhiteSpace(config.DefaultOutput))
			{
				config.DefaultOutput = "plain";
			}

			config.Database ??= new DatabaseConfig();
			config.Database.Provider = DatabaseProviders.Normalize(config.Database.Provider);
			config.Database.Sqlite ??= new SqliteConfig();
			config.Database.Postgres ??= new PostgresConfig();
		}

		private static void ValidateLoadedConfig(Config config)
		{
			if (!string.IsNullOrWhiteSpace(config.ApiUrl) && !IsValidUrlFormat(config.ApiUrl))
			{
				throw new ArgumentException("Invalid API URL format in config. Must be a valid HTTP or HTTPS URL.");
			}
		}

		private static bool IsValidUrlFormat(string url)
		{
			return Uri.TryCreate(url, UriKind.Absolute, out var uri)
				&& (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
		}

		private static string NormalizeKey(string key)
		{
			return key.Trim().ToLowerInvariant();
		}

		private static string GetConfigDirectory() => TaskPaths.GetConfigDirectory();

		private DatabaseConfig EnsureDatabase()
		{
			Database ??= new DatabaseConfig();
			Database.Provider = DatabaseProviders.Normalize(Database.Provider);
			return Database;
		}

		private SqliteConfig EnsureSqlite()
		{
			var database = EnsureDatabase();
			database.Sqlite ??= new SqliteConfig();
			return database.Sqlite;
		}

		private PostgresConfig EnsurePostgres()
		{
			var database = EnsureDatabase();
			database.Postgres ??= new PostgresConfig();
			return database.Postgres;
		}

		private TelegramConfig EnsureTelegram()
		{
			Telegram ??= new TelegramConfig();
			return Telegram;
		}
	}
}
