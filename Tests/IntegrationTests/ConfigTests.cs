using System;
using Task.Cli;
using Xunit;

namespace Task.Cli.Tests.IntegrationTests
{
	public sealed class ConfigTests
	{
		[Fact]
		public async System.Threading.Tasks.Task SetDatabaseProviderToPg_DoesNotLoseConfigBeforeConnectionStringIsSet()
		{
			var config = new Config
			{
				ApiUrl = "http://localhost:1234",
				DefaultOutput = "json"
			};

			await config.SetValueAsync("database.provider", "pg");

			Assert.Equal("pg", config.GetDatabaseProvider());
			Assert.Equal("http://localhost:1234", config.ApiUrl);
			Assert.Equal("json", config.DefaultOutput);
			Assert.Null(config.GetValue("database.pg.connectionString"));
		}

		[Fact]
		public void ToDatabaseConnectionSettings_RequiresConnectionStringWhenProviderIsPg()
		{
			var config = new Config();
			config.Database = new Config.DatabaseConfig
			{
				Provider = "pg",
				Sqlite = new Config.SqliteConfig(),
				Postgres = new Config.PostgresConfig()
			};

			var ex = Assert.Throws<ArgumentException>(() => config.ToDatabaseConnectionSettings());

			Assert.Equal("postgres.connectionString must be set when database.provider is 'pg'", ex.Message);
		}
	}
}
