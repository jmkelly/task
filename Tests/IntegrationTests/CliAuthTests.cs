using System;
using System.Net.Http;
using Task.Cli;
using Task.Core;
using Xunit;
using Xunit.Sdk;

namespace Task.Cli.Tests.IntegrationTests;

/// <summary>
/// CLI-side auth: config api.key, X-Api-Key header injection, 401 mapping,
/// and TASK_API_KEY environment precedence.
/// </summary>
public sealed class CliAuthTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public CliAuthTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ClearDatabase();
    }

    [Fact]
    public async System.Threading.Tasks.Task Config_ApiKey_SetGetUnset_RoundTrip()
    {
        var config = new Config();
        await config.SetValueAsync("api.key", "tk_testkey123");

        Assert.Equal("tk_testkey123", config.GetValue("api.key"));

        config.UnsetValue("api.key");
        Assert.Null(config.GetValue("api.key"));
    }

    [Fact]
    public async System.Threading.Tasks.Task ApiClient_WithKey_AuthenticatesAgainstSecuredServer()
    {
        var username = $"cli_{Guid.NewGuid():N}"[..12];
        var plaintextKey = _factory.CreateUserApiKey(username);

        var apiClient = CreateApiClient(plaintextKey);

        var task = await apiClient.AddTaskAsync(
            new Uid().GenerateUid(),
            "CLI auth task",
            null,
            "medium",
            null,
            new System.Collections.Generic.List<string>());

        Assert.Equal("CLI auth task", task.Title);

        var tasks = await apiClient.GetAllTasksAsync();
        Assert.Single(tasks);
    }

    [Fact]
    public async System.Threading.Tasks.Task ApiClient_WithoutKey_ThrowsActionable401()
    {
        var apiClient = CreateApiClient(apiKey: null);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await apiClient.GetAllTasksAsync());

        Assert.Contains("config set api.key", ex.Message);
        Assert.Contains("/keys", ex.Message);
    }

    [Fact]
    public async System.Threading.Tasks.Task ApiClient_RevokedKey_FailsWithActionableMessage()
    {
        var username = $"cli_{Guid.NewGuid():N}"[..12];
        var plaintextKey = _factory.CreateUserApiKey(username);

        var db = _factory.Database;
        var key = await db.FindApiKeyByHashAsync(Task.Core.Auth.ApiKeyGenerator.Hash(plaintextKey));
        Assert.NotNull(key);
        await db.RevokeApiKeyAsync(key!.Id, null);

        var apiClient = CreateApiClient(plaintextKey);
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await apiClient.GetAllTasksAsync());

        Assert.Contains("revoked", ex.Message);
    }

    [Fact]
    public async System.Threading.Tasks.Task ApiClient_WithKey_CannotTouchAnotherUsersTasks()
    {
        var keyA = _factory.CreateUserApiKey($"cli_{Guid.NewGuid():N}"[..12]);
        var keyB = _factory.CreateUserApiKey($"cli_{Guid.NewGuid():N}"[..12]);

        var clientA = CreateApiClient(keyA);
        var task = await clientA.AddTaskAsync(
            new Uid().GenerateUid(),
            "A's private task",
            null,
            "medium",
            null,
            new System.Collections.Generic.List<string>());

        var clientB = CreateApiClient(keyB);
        var tasksB = await clientB.GetAllTasksAsync();
        Assert.DoesNotContain(tasksB, t => t.Uid == task.Uid);
    }

    [Fact]
    public void TaskApiKeyEnvironmentVariable_TakesPrecedence()
    {
        // The environment lookup is injected rather than mutating the process-wide
        // environment: the suite runs in parallel with tests that spawn CLI
        // subprocesses, which would inherit a bogus TASK_API_KEY and fail auth.
        var resolved = Program.ResolveApiKey(_ => "tk_envprecedence123");
        Assert.Equal("tk_envprecedence123", resolved);
    }

    private ApiClient CreateApiClient(string? apiKey)
    {
        var handler = _factory.Server.CreateHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = _factory.Server.BaseAddress
        };

        var baseUrl = _factory.Server.BaseAddress?.ToString()
            ?? throw new XunitException("TestServer has no base address.");

        return new ApiClient(baseUrl, httpClient)
        {
            ApiKey = apiKey
        };
    }
}
