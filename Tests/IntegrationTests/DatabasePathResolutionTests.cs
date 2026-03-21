using System;
using System.IO;
using Task.Core;
using Xunit;

namespace Task.Cli.Tests.IntegrationTests
{
	public sealed class DatabasePathResolutionTests : IDisposable
	{
		private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"task-db-path-tests-{Guid.NewGuid():N}");

		[Fact]
		public void ResolveDatabasePath_UsesConfigDirectory_WhenDatabasePathIsNotProvided()
		{
			var resolvedPath = TaskPaths.ResolveDatabasePath(
				configuredDatabasePath: null,
				configHomeOverride: Path.Combine(_tempRoot, "config-home"));

			Assert.Equal(
				Path.Combine(_tempRoot, "config-home", "task", "tasks.db"),
				resolvedPath);
		}

		[Fact]
		public void ResolveDatabasePath_PreservesExplicitOverride_WhenDatabasePathIsProvided()
		{
			var explicitPath = Path.Combine(_tempRoot, "custom-db", "team.db");

			var resolvedPath = TaskPaths.ResolveDatabasePath(
				explicitPath,
				configHomeOverride: Path.Combine(_tempRoot, "config-home"));

			Assert.Equal(explicitPath, resolvedPath);
		}

		[Fact]
		public void Initialize_CreatesParentDirectory_ForDefaultResolvedPath()
		{
			var databasePath = TaskPaths.ResolveDatabasePath(
				configuredDatabasePath: null,
				configHomeOverride: Path.Combine(_tempRoot, "config-home"));

			var database = new Database(databasePath);
			database.Initialize();

			Assert.True(File.Exists(databasePath));
			Assert.True(Directory.Exists(Path.GetDirectoryName(databasePath)!));
		}

		[Fact]
		public void Initialize_CreatesParentDirectory_ForExplicitOverridePath()
		{
			var databasePath = TaskPaths.ResolveDatabasePath(
				Path.Combine(_tempRoot, "nested", "override", "tasks.db"),
				configHomeOverride: Path.Combine(_tempRoot, "config-home"));

			var database = new Database(databasePath);
			database.Initialize();

			Assert.True(File.Exists(databasePath));
			Assert.True(Directory.Exists(Path.GetDirectoryName(databasePath)!));
		}

		public void Dispose()
		{
			if (Directory.Exists(_tempRoot))
			{
				Directory.Delete(_tempRoot, recursive: true);
			}
		}
	}
}
