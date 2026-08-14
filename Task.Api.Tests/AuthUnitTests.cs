using System;
using System.IO;
using Task.Core;
using Task.Core.Auth;
using Xunit;
using SystemTask = System.Threading.Tasks.Task;

namespace Task.Api.Tests.UnitTests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_And_Verify_RoundTrip()
    {
        var hash = PasswordHasher.Hash("correct horse battery staple");
        Assert.StartsWith("pbkdf2$", hash);
        Assert.True(PasswordHasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void Verify_WrongPassword_Fails()
    {
        var hash = PasswordHasher.Hash("right-password");
        Assert.False(PasswordHasher.Verify("wrong-password", hash));
    }

    [Fact]
    public void Hash_IsUnique_PerPassword()
    {
        var hash1 = PasswordHasher.Hash("same-password");
        var hash2 = PasswordHasher.Hash("same-password");
        Assert.NotEqual(hash1, hash2); // random salt
        Assert.True(PasswordHasher.Verify("same-password", hash1));
        Assert.True(PasswordHasher.Verify("same-password", hash2));
    }

    [Fact]
    public void Verify_GarbageHash_ReturnsFalse()
    {
        Assert.False(PasswordHasher.Verify("anything", "not-a-hash"));
        Assert.False(PasswordHasher.Verify("anything", "pbkdf2$0$AA$AA"));
        Assert.False(PasswordHasher.Verify("anything", string.Empty));
    }
}

public class ApiKeyGeneratorTests
{
    [Fact]
    public void Generate_ProducesValidFormat()
    {
        var key = ApiKeyGenerator.Generate();
        Assert.StartsWith("tk_", key);
        Assert.True(ApiKeyGenerator.IsValidFormat(key));
    }

    [Fact]
    public void Generate_IsUnique()
    {
        var keys = Enumerable.Range(0, 100).Select(_ => ApiKeyGenerator.Generate()).ToList();
        Assert.Equal(100, keys.Distinct().Count());
    }

    [Fact]
    public void Hash_IsDeterministic_AndNotPlaintext()
    {
        var key = ApiKeyGenerator.Generate();
        var hash1 = ApiKeyGenerator.Hash(key);
        var hash2 = ApiKeyGenerator.Hash(key);
        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length);
        Assert.DoesNotContain(key, hash1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-key")]
    [InlineData("tk_short")]
    [InlineData("TK_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("tk_!!!invalid!!!characters!!!")]
    public void IsValidFormat_RejectsBadKeys(string key)
    {
        Assert.False(ApiKeyGenerator.IsValidFormat(key));
    }
}

public class MigrationIdempotencyTests
{
    [Fact]
    public async SystemTask Initialize_RunsTwice_OnFreshDatabase()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"migration_fresh_{Guid.NewGuid()}.db");
        try
        {
            var db = new Database(dbPath);
            await db.InitializeAsync();
            await db.InitializeAsync(); // second run must be a no-op

            // New auth tables exist and work.
            await db.InsertApiKeyAsync("key-1", "user-1", "test", "hash");
            var found = await db.FindApiKeyByHashAsync("hash");
            Assert.NotNull(found);
            Assert.Equal("user-1", found!.UserId);
        }
        finally
        {
            try { File.Delete(dbPath); } catch { }
        }
    }

    [Fact]
    public async SystemTask Initialize_RunsOn_PopulatedLegacyDatabase()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"migration_legacy_{Guid.NewGuid()}.db");
        try
        {
            // Simulate a pre-auth database: tasks table without user_id columns.
            using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE tasks (
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
                    );";
                command.ExecuteNonQuery();
            }

            var db = new Database(dbPath);
            await db.InitializeAsync();

            // Legacy row is unowned; users/api_keys tables exist.
            var task = await db.AddTaskAsync("legacy-uid", "Legacy", null, "medium", null, new List<string>(), userId: null);
            Assert.NotNull(task);

            var user = await db.CreateUserAndClaimTasksAsync("user-legacy", "legacyuser", "hash", forceIsAdmin: false);
            Assert.NotNull(user);
            Assert.True(user.Value.IsFirstUser); // no users existed → first user becomes admin

            var claimed = await db.GetAllTasksAsync("user-legacy");
            Assert.Empty(claimed); // assignee did not match

            var all = await db.GetAllTasksAsync();
            Assert.Single(all);
        }
        finally
        {
            try { File.Delete(dbPath); } catch { }
        }
    }

    [Fact]
    public async SystemTask CreateUserAndClaimTasks_ClaimsMatchingAssignee()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"migration_claim_{Guid.NewGuid()}.db");
        try
        {
            var db = new Database(dbPath);
            await db.InitializeAsync();

            await db.AddTaskAsync("t1", "Task 1", null, "medium", null, new List<string>(), assignee: "Alice", userId: null);
            await db.AddTaskAsync("t2", "Task 2", null, "medium", null, new List<string>(), assignee: "Bob", userId: null);

            var result = await db.CreateUserAndClaimTasksAsync("u-alice", "alice", "hash", forceIsAdmin: false);
            Assert.NotNull(result);
            Assert.True(result.Value.IsFirstUser);

            var aliceTasks = await db.GetAllTasksAsync("u-alice");
            var bobTasks = await db.GetAllTasksAsync("u-bob");
            Assert.Single(aliceTasks);
            Assert.Equal("t1", aliceTasks[0].Uid);
            Assert.Empty(bobTasks);
        }
        finally
        {
            try { File.Delete(dbPath); } catch { }
        }
    }

    [Fact]
    public async SystemTask DuplicateUsername_ReturnsNull()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"migration_dup_{Guid.NewGuid()}.db");
        try
        {
            var db = new Database(dbPath);
            await db.InitializeAsync();

            var first = await db.CreateUserAndClaimTasksAsync("u1", "alice", "hash", forceIsAdmin: false);
            Assert.NotNull(first);

            var second = await db.CreateUserAndClaimTasksAsync("u2", "ALICE", "hash2", forceIsAdmin: false);
            Assert.Null(second);
        }
        finally
        {
            try { File.Delete(dbPath); } catch { }
        }
    }
}
