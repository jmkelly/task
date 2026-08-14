using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Task.Core;
using Task.Core.Providers.Telegram;
using Testcontainers.PostgreSql;
using Xunit;

namespace Task.Cli.Tests.IntegrationTests;

/// <summary>
/// The auth + per-user isolation matrix against PostgreSQL (same-commit dual-provider
/// rule). Mirrors the SQLite matrix in Task.Api.Tests/AuthIntegrationTests.cs.
/// </summary>
/// <remarks>
/// The Postgres container and in-process API host live in a class fixture (started
/// once per class) while each test resets the database in <see cref="InitializeAsync"/>.
/// Tests within a class run sequentially, so per-test resets keep full test-level
/// isolation without paying for a container startup per test — the suite runs in
/// parallel with other test classes, and container churn is kept to one per class.
/// </remarks>
public sealed class PostgreSqlAuthIsolationTests : IClassFixture<PostgreSqlAuthIsolationFixture>, IAsyncLifetime
{
    private readonly PostgreSqlAuthIsolationFixture _fixture;
    private HttpClient _client = null!;

    public PostgreSqlAuthIsolationTests(PostgreSqlAuthIsolationFixture fixture)
    {
        _fixture = fixture;
    }

    public async System.Threading.Tasks.Task InitializeAsync()
    {
        // Each test starts from a clean slate: the first signup of the test
        // becomes the admin, exactly like a fresh database.
        await _fixture.ResetDatabaseAsync();
        _client = await CreateUserClientAsync();
    }

    public System.Threading.Tasks.Task DisposeAsync() => System.Threading.Tasks.Task.CompletedTask;

    [Fact]
    public async System.Threading.Tasks.Task FirstUserBecomesAdmin_SecondUserDoesNot()
    {
        var me = await GetMeAsync(_client);
        Assert.True(me.IsAdmin);

        var secondClient = await CreateUserClientAsync();
        var secondMe = await GetMeAsync(secondClient);
        Assert.False(secondMe.IsAdmin);
    }

    [Fact]
    public async System.Threading.Tasks.Task UserB_CannotGetOrList_UserAsTasks()
    {
        var task = await CreateTaskAsync(_client, "A's postgres task");
        var otherClient = await CreateUserClientAsync();

        var tasks = await otherClient.GetFromJsonAsync<List<TaskDto>>("/api/tasks");
        Assert.DoesNotContain(tasks!, t => t.Uid == task.Uid);

        var get = await otherClient.GetAsync($"/api/tasks/{task.Uid}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async System.Threading.Tasks.Task UserB_CannotEdit_UserAsTasks()
    {
        var task = await CreateTaskAsync(_client, "Do not touch (pg)");
        var otherClient = await CreateUserClientAsync();

        var update = await otherClient.PutAsJsonAsync($"/api/tasks/{task.Uid}", new TaskUpdateDto { Title = "Hacked" });
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    [Fact]
    public async System.Threading.Tasks.Task UserB_CannotCompleteOrDelete_UserAsTasks()
    {
        var task = await CreateTaskAsync(_client, "Do not complete or delete (pg)");
        var otherClient = await CreateUserClientAsync();

        var complete = await otherClient.PatchAsync($"/api/tasks/{task.Uid}/complete", null);
        Assert.Equal(HttpStatusCode.NotFound, complete.StatusCode);

        var delete = await otherClient.DeleteAsync($"/api/tasks/{task.Uid}");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async System.Threading.Tasks.Task Search_IsScopedPerUser()
    {
        await CreateTaskAsync(_client, "PostgresSearchable secret");
        var otherClient = await CreateUserClientAsync();

        var search = await otherClient.GetAsync("/api/tasks/search?q=PostgresSearchable");
        search.EnsureSuccessStatusCode();
        var results = await search.Content.ReadFromJsonAsync<List<TaskDto>>();
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async System.Threading.Tasks.Task ApiKey_CreateUseRevoke_Lifecycle()
    {
        var keyResponse = await _client.PostAsJsonAsync("/api/keys", new { name = "pg-cli" });
        keyResponse.EnsureSuccessStatusCode();
        var keyDto = await keyResponse.Content.ReadFromJsonAsync<KeyDto>();
        Assert.NotNull(keyDto);
        Assert.NotNull(keyDto!.Key);
        Assert.StartsWith("tk_", keyDto.Key);

        var keyClient = _fixture.CreateClient();
        keyClient.DefaultRequestHeaders.Add("X-Api-Key", keyDto.Key);
        var ok = await keyClient.GetAsync("/api/tasks");
        ok.EnsureSuccessStatusCode();

        var revoke = await _client.DeleteAsync($"/api/keys/{keyDto.Id}");
        revoke.EnsureSuccessStatusCode();

        var after = await keyClient.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async System.Threading.Tasks.Task Anonymous_DataAccess_Returns401()
    {
        var anon = _fixture.CreateClient();
        var response = await anon.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async System.Threading.Tasks.Task UnownedTask_InvisibleToNormalUsers_VisibleToAdmin()
    {
        // Insert a legacy-style unowned task directly into Postgres.
        await using (var connection = new NpgsqlConnection(_fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO tasks (uid, title, description, priority, due_date, tags, project, assignee, status, block_reason, created_at, updated_at, archived, archived_at, user_id)
                VALUES ('legacy-pg', 'Unowned postgres task', NULL, 'medium', NULL, '', '', 'ghost', 'todo', NULL, now(), now(), FALSE, NULL, NULL)";
            await command.ExecuteNonQueryAsync();
        }

        var normalClient = await CreateUserClientAsync();
        var normalTasks = await normalClient.GetFromJsonAsync<List<TaskDto>>("/api/tasks");
        Assert.DoesNotContain(normalTasks!, t => t.Uid == "legacy-pg");

        var adminBody = await (await _client.GetAsync("/api/admin/tasks")).Content.ReadAsStringAsync();
        Assert.Contains("legacy-pg", adminBody);
    }

    [Fact]
    public async System.Threading.Tasks.Task Signup_ClaimsMatchingLegacyTasks()
    {
        await using (var connection = new NpgsqlConnection(_fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO tasks (uid, title, description, priority, due_date, tags, project, assignee, status, block_reason, created_at, updated_at, archived, archived_at, user_id)
                VALUES ('legacy-alice', 'Alice postgres legacy', NULL, 'medium', NULL, '', '', 'alice', 'todo', NULL, now(), now(), FALSE, NULL, NULL)";
            await command.ExecuteNonQueryAsync();
        }

        var aliceClient = await CreateUserClientAsync("alice");
        var tasks = await aliceClient.GetFromJsonAsync<List<TaskDto>>("/api/tasks");
        Assert.Contains(tasks!, t => t.Uid == "legacy-alice");
    }

    private async System.Threading.Tasks.Task<HttpClient> CreateUserClientAsync(string? username = null)
    {
        var client = _fixture.CreateClient();
        username ??= $"pg_{Guid.NewGuid():N}"[..12];
        const string password = "password123";

        var signup = await client.PostAsJsonAsync("/api/auth/signup", new { username, password });
        signup.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        login.EnsureSuccessStatusCode();
        return client;
    }

    private static async System.Threading.Tasks.Task<MeDto> GetMeAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/me");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MeDto>() ?? throw new InvalidOperationException();
    }

    private static async System.Threading.Tasks.Task<TaskDto> CreateTaskAsync(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync("/api/tasks", new TaskCreateDto
        {
            Uid = new Uid().GenerateUid(),
            Title = title,
            Priority = "medium"
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TaskDto>() ?? throw new InvalidOperationException();
    }

    public sealed class KeyDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Key { get; set; }
    }

    public sealed class MeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
    }
}

/// <summary>
/// Owns the PostgreSQL container and in-process API host for the whole test class.
/// Starting the container once per class instead of once per test eliminates the
/// dominant cost of the auth matrix (8 container startups ≈ 25s) while per-test
/// database resets keep each test isolated.
/// </summary>
public sealed class PostgreSqlAuthIsolationFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private PostgreSqlTestApplicationFactory _factory = null!;

    public PostgreSqlAuthIsolationFixture()
    {
        _container = new PostgreSqlBuilder("postgres:16.4")
            .WithDatabase("task_tests")
            .WithUsername("task")
            .WithPassword("task")
            .Build();
    }

    public string ConnectionString => _container.GetConnectionString();

    public async System.Threading.Tasks.Task InitializeAsync()
    {
        await _container.StartAsync();
        Console.WriteLine(
            $"[PostgreSqlAuthIsolationFixture] container={_container.Id} host={_container.Hostname}:{_container.GetMappedPublicPort(5432)}");

        _factory = new PostgreSqlTestApplicationFactory(ConnectionString);

        // Trigger host start (runs the schema migrations), then reset to a clean slate.
        _factory.CreateClient().Dispose();
    }

    public async System.Threading.Tasks.Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    public HttpClient CreateClient() => _factory.CreateClient();

    public async System.Threading.Tasks.Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "TRUNCATE TABLE tasks, users, api_keys RESTART IDENTITY CASCADE";
        await command.ExecuteNonQueryAsync();
    }

    private sealed class PostgreSqlTestApplicationFactory : WebApplicationFactory<Task.Api.Program>
    {
        private readonly string _connectionString;

        public PostgreSqlTestApplicationFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("DatabaseProvider", "pg");
            builder.UseSetting("Postgres:ConnectionString", _connectionString);

            // Tests must be hermetic: never call the real Telegram API (credentials are
            // often present in the environment; GET /api/tasks on an empty board would
            // otherwise await a live api.telegram.org POST and flake with 500s/stalls).
            builder.UseSetting("Telegram:Enabled", "false");

            builder.ConfigureServices(services =>
            {
                var telegramProviderDescriptors = services
                    .Where(descriptor => descriptor.ServiceType == typeof(ITelegramProvider))
                    .ToList();
                foreach (var descriptor in telegramProviderDescriptors)
                {
                    services.Remove(descriptor);
                }

                var notificationServiceDescriptors = services
                    .Where(descriptor => descriptor.ServiceType == typeof(TelegramNotificationService))
                    .ToList();
                foreach (var descriptor in notificationServiceDescriptors)
                {
                    services.Remove(descriptor);
                }

                services.AddSingleton<ITelegramProvider>(new FakeTelegramProvider());
                services.AddSingleton<TelegramNotificationService>(sp => new TelegramNotificationService(
                    sp.GetRequiredService<ITelegramProvider>(),
                    Options.Create(new TelegramProviderOptions { Enabled = false }),
                    NullLogger<TelegramNotificationService>.Instance));
            });

            Console.WriteLine("[PostgreSqlAuthIsolationTests] telegram=disabled(fake)");
        }

        /// <summary>No-op Telegram provider: records nothing, never touches the network.</summary>
        private sealed class FakeTelegramProvider : ITelegramProvider
        {
            public System.Threading.Tasks.Task SendMessageAsync(string message, System.Threading.CancellationToken cancellationToken = default)
            {
                return System.Threading.Tasks.Task.CompletedTask;
            }
        }
    }
}
