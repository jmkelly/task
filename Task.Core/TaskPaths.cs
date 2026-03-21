using System;
using System.IO;

namespace Task.Core
{
	public static class TaskPaths
	{
		public static string GetConfigDirectory(string? configHomeOverride = null, string? userHomeOverride = null)
		{
			var configHome = configHomeOverride;
			if (string.IsNullOrWhiteSpace(configHome))
			{
				configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
			}

			if (string.IsNullOrWhiteSpace(configHome))
			{
				var userHome = userHomeOverride;
				if (string.IsNullOrWhiteSpace(userHome))
				{
					userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
				}

				if (string.IsNullOrWhiteSpace(userHome))
				{
					throw new InvalidOperationException("Unable to resolve user home directory for config storage.");
				}

				configHome = Path.Combine(userHome, ".config");
			}

			return Path.Combine(configHome, "task");
		}

		public static string GetDefaultDatabasePath(string? configHomeOverride = null, string? userHomeOverride = null)
		{
			return Path.Combine(GetConfigDirectory(configHomeOverride, userHomeOverride), "tasks.db");
		}

		public static string ResolveDatabasePath(string? configuredDatabasePath, string? configHomeOverride = null, string? userHomeOverride = null)
		{
			if (!string.IsNullOrWhiteSpace(configuredDatabasePath))
			{
				return configuredDatabasePath;
			}

			return GetDefaultDatabasePath(configHomeOverride, userHomeOverride);
		}
	}
}
