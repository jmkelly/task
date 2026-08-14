using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Task.Core;
using Xunit;
using SystemTask = System.Threading.Tasks.Task;

namespace Task.Api.Tests.IntegrationTests;

/// <summary>
/// Multi-user auth matrix: accounts, sessions, API keys, per-user isolation,
/// admin powers, and the legacy claim-at-signup path. Runs against SQLite;
/// the same matrix is exercised for Postgres in Tests/IntegrationTests.
/// </summary>
public class AuthIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ClearDatabase();
        _client = _factory.CreateUserClient();
    }

    // ------------------------------------------------------------------
    // Accounts & sessions
    // ------------------------------------------------------------------

    [Fact]
    public async SystemTask Me_ReturnsIdentity_WhenSignedIn()
    {
        var response = await _client.GetAsync("/api/auth/me");
        response.EnsureSuccessStatusCode();
        var me = await response.Content.ReadFromJsonAsync<MeDto>();
        Assert.NotNull(me);
        Assert.False(string.IsNullOrWhiteSpace(me.Username));
    }

    [Fact]
    public async SystemTask FirstUserBecomesAdmin_SecondUserDoesNot()
    {
        var adminMe = await GetMeAsync(_client);
        Assert.True(adminMe.IsAdmin);

        var secondClient = _factory.CreateUserClient();
        var secondMe = await GetMeAsync(secondClient);
        Assert.False(secondMe.IsAdmin);
    }

    [Fact]
    public async SystemTask Login_WithBadPassword_Returns401()
    {
        var anon = _factory.CreateClient();
        var response = await anon.PostAsJsonAsync("/api/auth/login", new { username = "nobody", password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async SystemTask Signup_WithDuplicateUsername_Returns409()
    {
        var username = $"dup_{Guid.NewGuid():N}"[..12];
        var anon = _factory.CreateClient();
        var first = await anon.PostAsJsonAsync("/api/auth/signup", new { username, password = "password123" });
        first.EnsureSuccessStatusCode();

        var second = await anon.PostAsJsonAsync("/api/auth/signup", new { username, password = "password123" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async SystemTask Signup_WithInvalidUsername_Returns400()
    {
        var anon = _factory.CreateClient();
        var response = await anon.PostAsJsonAsync("/api/auth/signup", new { username = "a", password = "password123" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async SystemTask Signup_WhenDisabled_Returns403()
    {
        using var closedFactory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Auth:AllowSignup", "false"));

        var anon = closedFactory.CreateClient();
        var response = await anon.PostAsJsonAsync("/api/auth/signup", new { username = "latecomer", password = "password123" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async SystemTask Login_WithDisabledUser_Returns401()
    {
        var username = $"dis_{Guid.NewGuid():N}"[..12];
        var userClient = _factory.CreateUserClient(username);
        _factory.SetUserDisabled(username);

        var anon = _factory.CreateClient();
        var response = await anon.PostAsJsonAsync("/api/auth/login", new { username, password = "password123" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async SystemTask Logout_EndsSession()
    {
        var response = await _client.PostAsync("/api/auth/logout", null);
        response.EnsureSuccessStatusCode();

        var me = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async SystemTask Anonymous_DataAccess_Returns401WithActionableBody()
    {
        var anon = _factory.CreateClient();
        var response = await anon.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("/keys", body);
    }

    [Fact]
    public async SystemTask Health_IsAnonymous()
    {
        var anon = _factory.CreateClient();
        var response = await anon.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async SystemTask BoardPage_RedirectsToLogin_WhenAnonymous()
    {
        var anon = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await anon.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.ToString() ?? string.Empty);
    }

    // ------------------------------------------------------------------
    // API keys
    // ------------------------------------------------------------------

    [Fact]
    public async SystemTask KeylessRequest_Returns401()
    {
        var anon = _factory.CreateClient();
        var response = await anon.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async SystemTask MalformedKey_Returns401()
    {
        var anon = _factory.CreateClient();
        anon.DefaultRequestHeaders.Add("X-Api-Key", "not-a-key");
        var response = await anon.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async SystemTask UnknownKey_Returns401()
    {
        var anon = _factory.CreateClient();
        anon.DefaultRequestHeaders.Add("X-Api-Key", "tk_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
        var response = await anon.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async SystemTask ValidKey_AllowsAccess()
    {
        var keyClient = _factory.CreateKeyClient();
        var response = await keyClient.GetAsync("/api/tasks");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async SystemTask CreateKey_ReturnsPlaintextOnce_AndNeverAgain()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/keys", new { name = "cli-laptop" });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<TestWebApplicationFactory.KeyDto>();
        Assert.NotNull(created);
        Assert.NotNull(created.Key);
        Assert.StartsWith("tk_", created.Key);

        var listResponse = await _client.GetAsync("/api/keys");
        listResponse.EnsureSuccessStatusCode();
        var listBody = await listResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(created.Key, listBody);
    }

    [Fact]
    public async SystemTask RevokedKey_FailsImmediately()
    {
        var keyClient = _factory.CreateKeyClient();
        var keyId = await GetFirstKeyIdAsync(keyClient);

        var revoke = await keyClient.DeleteAsync($"/api/keys/{keyId}");
        revoke.EnsureSuccessStatusCode();

        var response = await keyClient.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async SystemTask KeyOfDisabledUser_Fails()
    {
        var username = $"kd_{Guid.NewGuid():N}"[..12];
        var keyClient = _factory.CreateKeyClient(username);
        _factory.SetUserDisabled(username);

        var response = await keyClient.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async SystemTask Keys_AreScopedToOwner()
    {
        var otherClient = _factory.CreateUserClient();
        var response = await otherClient.GetAsync("/api/keys");
        response.EnsureSuccessStatusCode();
        var keys = await response.Content.ReadFromJsonAsync<List<TestWebApplicationFactory.KeyDto>>();
        Assert.NotNull(keys);
        Assert.Empty(keys);
    }

    [Fact]
    public async SystemTask Revoke_AnotherUsersKey_Returns404()
    {
        var otherClient = _factory.CreateUserClient();
        var (otherKeyId, _) = await CreateKeyForUserAsync(otherClient);

        var revoke = await _client.DeleteAsync($"/api/keys/{otherKeyId}");
        Assert.Equal(HttpStatusCode.NotFound, revoke.StatusCode);
    }

    // ------------------------------------------------------------------
    // Per-user isolation
    // ------------------------------------------------------------------

    [Fact]
    public async SystemTask UserB_CannotSee_UserAsTasks()
    {
        var task = await CreateTaskAsync(_client, "A's secret task");
        var otherClient = _factory.CreateUserClient();

        var list = await otherClient.GetAsync("/api/tasks");
        var tasks = await list.Content.ReadFromJsonAsync<List<TaskDto>>();
        Assert.DoesNotContain(tasks!, t => t.Uid == task.Uid);

        var get = await otherClient.GetAsync($"/api/tasks/{task.Uid}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async SystemTask UserB_CannotSearch_UserAsTasks()
    {
        await CreateTaskAsync(_client, "SearchableAlpha secret");
        var otherClient = _factory.CreateUserClient();

        var search = await otherClient.GetAsync("/api/tasks/search?q=SearchableAlpha");
        search.EnsureSuccessStatusCode();
        var results = await search.Content.ReadFromJsonAsync<List<TaskDto>>();
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async SystemTask UserB_CannotEdit_UserAsTasks()
    {
        var task = await CreateTaskAsync(_client, "Do not touch");
        var otherClient = _factory.CreateUserClient();

        var update = await otherClient.PutAsJsonAsync($"/api/tasks/{task.Uid}", new TaskUpdateDto { Title = "Hacked" });
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    [Fact]
    public async SystemTask UserB_CannotCompleteOrDelete_UserAsTasks()
    {
        var task = await CreateTaskAsync(_client, "Do not complete or delete");
        var otherClient = _factory.CreateUserClient();

        var complete = await otherClient.PatchAsync($"/api/tasks/{task.Uid}/complete", null);
        Assert.Equal(HttpStatusCode.NotFound, complete.StatusCode);

        var delete = await otherClient.DeleteAsync($"/api/tasks/{task.Uid}");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async SystemTask Export_IsScopedToUser()
    {
        await CreateTaskAsync(_client, "Export me only");
        var otherClient = _factory.CreateUserClient();

        var export = await otherClient.GetAsync("/api/tasks/export");
        export.EnsureSuccessStatusCode();
        var tasks = await export.Content.ReadFromJsonAsync<List<TaskDto>>();
        Assert.NotNull(tasks);
        Assert.DoesNotContain(tasks, t => t.Title == "Export me only");
    }

    [Fact]
    public async SystemTask Import_LandsInImportingUsersBoard()
    {
        var otherClient = _factory.CreateUserClient();
        var import = await otherClient.PostAsJsonAsync("/api/tasks/import", new[]
        {
            new { Title = "Imported by B", Priority = "medium" }
        });
        import.EnsureSuccessStatusCode();

        var aTasks = await _client.GetFromJsonAsync<List<TaskDto>>("/api/tasks");
        Assert.DoesNotContain(aTasks!, t => t.Title == "Imported by B");

        var bTasks = await otherClient.GetFromJsonAsync<List<TaskDto>>("/api/tasks");
        Assert.Contains(bTasks!, t => t.Title == "Imported by B");
    }

    [Fact]
    public async SystemTask Board_ShowsOnlyOwnTasks()
    {
        await CreateTaskAsync(_client, "Visible only to me");
        var otherClient = _factory.CreateUserClient();

        var otherBoard = await otherClient.GetAsync("/Index?handler=Refresh");
        otherBoard.EnsureSuccessStatusCode();
        Assert.DoesNotContain("Visible only to me", await otherBoard.Content.ReadAsStringAsync());

        var myBoard = await _client.GetAsync("/Index?handler=Refresh");
        Assert.Contains("Visible only to me", await myBoard.Content.ReadAsStringAsync());
    }

    [Fact]
    public async SystemTask BoardClear_OnlyArchivesOwnTasks()
    {
        await CreateTaskAsync(_client, "Survivor task");
        var otherClient = _factory.CreateUserClient();

        var response = await PostRazorHandlerAsync(otherClient, "ClearBoard", new Dictionary<string, string>());
        response.EnsureSuccessStatusCode();

        var myTasks = await _client.GetFromJsonAsync<List<TaskDto>>("/api/tasks");
        Assert.Contains(myTasks!, t => t.Title == "Survivor task");
    }

    [Fact]
    public async SystemTask BoardStatusUpdate_CannotTouchForeignTasks()
    {
        var task = await CreateTaskAsync(_client, "Foreign status");
        var otherClient = _factory.CreateUserClient();

        var response = await PostRazorHandlerAsync(otherClient, "UpdateStatus", new Dictionary<string, string>
        {
            ["uid"] = task.Uid,
            ["status"] = "done",
            ["blockReason"] = string.Empty
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async SystemTask UnownedTask_InvisibleToNormalUsers_VisibleToAdmin()
    {
        _factory.AddUnownedTask("Legacy unowned task", assignee: "nobody");

        // _client is the first user on this database → admin.
        var adminResponse = await _client.GetAsync("/api/admin/tasks");
        adminResponse.EnsureSuccessStatusCode();
        var adminBody = await adminResponse.Content.ReadAsStringAsync();
        Assert.Contains("Legacy unowned task", adminBody);

        var normalClient = _factory.CreateUserClient();
        var normalTasks = await normalClient.GetFromJsonAsync<List<TaskDto>>("/api/tasks");
        Assert.DoesNotContain(normalTasks!, t => t.Title == "Legacy unowned task");

        // The admin's own board view is still scoped to their own tasks.
        var adminOwn = await _client.GetFromJsonAsync<List<TaskDto>>("/api/tasks");
        Assert.DoesNotContain(adminOwn!, t => t.Title == "Legacy unowned task");
    }

    [Fact]
    public async SystemTask Signup_ClaimsMatchingLegacyTasks()
    {
        _factory.AddUnownedTask("Alice's legacy task", assignee: "alice");

        var aliceClient = _factory.CreateUserClient("alice");
        var tasks = await aliceClient.GetFromJsonAsync<List<TaskDto>>("/api/tasks");
        Assert.Contains(tasks!, t => t.Title == "Alice's legacy task");
    }

    [Fact]
    public async SystemTask Signup_ClaimIsCaseInsensitive()
    {
        _factory.AddUnownedTask("Bob's legacy task", assignee: "BoB");

        var bobClient = _factory.CreateUserClient("bob");
        var tasks = await bobClient.GetFromJsonAsync<List<TaskDto>>("/api/tasks");
        Assert.Contains(tasks!, t => t.Title == "Bob's legacy task");
    }

    // ------------------------------------------------------------------
    // Admin
    // ------------------------------------------------------------------

    [Fact]
    public async SystemTask Admin_CanListUsers()
    {
        _factory.CreateUserClient("admin1");
        _factory.CreateUserClient("regular1");

        var response = await _client.GetAsync("/api/admin/users");
        response.EnsureSuccessStatusCode();
        var users = await response.Content.ReadFromJsonAsync<List<AdminUserDto>>();
        Assert.NotNull(users);
        Assert.Contains(users, u => u.Username == "admin1");
        Assert.Contains(users, u => u.Username == "regular1");
    }

    [Fact]
    public async SystemTask Admin_CanSeeEveryBoard()
    {
        await CreateTaskAsync(_client, "A board task");
        var otherClient = _factory.CreateUserClient();
        await CreateTaskAsync(otherClient, "B board task");
        _factory.AddUnownedTask("Unowned legacy", assignee: "ghost");

        var response = await _client.GetAsync("/api/admin/tasks");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("A board task", body);
        Assert.Contains("B board task", body);
        Assert.Contains("Unowned legacy", body);
    }

    [Fact]
    public async SystemTask Admin_CanRevokeAnyKey()
    {
        var otherClient = _factory.CreateUserClient();
        var (keyId, plaintext) = await CreateKeyForUserAsync(otherClient);

        // The key works before revocation.
        var keyClient = _factory.CreateClient();
        keyClient.DefaultRequestHeaders.Add("X-Api-Key", plaintext);
        var before = await keyClient.GetAsync("/api/tasks");
        before.EnsureSuccessStatusCode();

        var revoke = await _client.PostAsync($"/api/admin/keys/{keyId}/revoke", null);
        revoke.EnsureSuccessStatusCode();

        var after = await keyClient.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async SystemTask NonAdmin_CannotAccessAdminEndpoints()
    {
        var normalClient = _factory.CreateUserClient();
        var response = await normalClient.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

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

    private static async System.Threading.Tasks.Task<string> GetFirstKeyIdAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/keys");
        response.EnsureSuccessStatusCode();
        var keys = await response.Content.ReadFromJsonAsync<List<TestWebApplicationFactory.KeyDto>>();
        Assert.NotNull(keys);
        var key = Assert.Single(keys);
        return key.Id;
    }

    private static async System.Threading.Tasks.Task<(string KeyId, string Plaintext)> CreateKeyForUserAsync(HttpClient userClient)
    {
        var response = await userClient.PostAsJsonAsync("/api/keys", new { name = "revoke-me" });
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<TestWebApplicationFactory.KeyDto>();
        Assert.NotNull(dto);
        Assert.NotNull(dto!.Key);
        return (dto.Id, dto.Key!);
    }

    private static async System.Threading.Tasks.Task<HttpResponseMessage> PostRazorHandlerAsync(HttpClient client, string handler, Dictionary<string, string> formValues)
    {
        var pageResponse = await client.GetAsync("/");
        pageResponse.EnsureSuccessStatusCode();

        var html = await pageResponse.Content.ReadAsStringAsync();
        var tokenMatch = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"\\s+type=\"hidden\"\\s+value=\"([^\"]+)\"");

        if (!tokenMatch.Success)
        {
            throw new InvalidOperationException("Could not find antiforgery token on Index page.");
        }

        var token = tokenMatch.Groups[1].Value;
        formValues["__RequestVerificationToken"] = token;

        var request = new HttpRequestMessage(HttpMethod.Post, $"/Index?handler={handler}")
        {
            Content = new FormUrlEncodedContent(formValues)
        };

        request.Headers.Add("RequestVerificationToken", token);

        return await client.SendAsync(request);
    }

    public sealed class MeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
    }

    public sealed class AdminUserDto
    {
        public string Username { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
    }
}
