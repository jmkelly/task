using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Task.Core;
using Xunit;
using SystemTask = System.Threading.Tasks.Task;

namespace Task.Api.Tests.IntegrationTests
{
    public class KanbanDoneOrderingUiTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;
        private readonly HttpClient _client;
        private readonly IUid _uidGenerator = new Uid();

        private string NewUid() => _uidGenerator.GenerateUid();

        public KanbanDoneOrderingUiTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
            _factory.ClearDatabase();
        }

        [Fact]
        public async SystemTask Board_OrdersDoneTasks_ByCompletionTimestampDescending()
        {
            var olderDoneTask = await CreateTaskAsync("Older done", "done");
            await SystemTask.Delay(TimeSpan.FromSeconds(1.1));
            var newerDoneTask = await CreateTaskAsync("Newer done", "done");

            var html = await RefreshBoardHtmlAsync();

            AssertUidAppearsBefore(html, newerDoneTask.Uid, olderDoneTask.Uid);
        }

        [Fact]
        public async SystemTask Board_PlacesMovedTask_FirstInDoneColumn_WhenMovedToDone()
        {
            var existingDoneTask = await CreateTaskAsync("Already done", "done");
            await SystemTask.Delay(TimeSpan.FromSeconds(1.1));
            var movedTask = await CreateTaskAsync("Move me", "todo");

            var response = await PostRazorHandlerAsync("UpdateStatus", new Dictionary<string, string>
            {
                ["uid"] = movedTask.Uid,
                ["status"] = "done",
                ["blockReason"] = string.Empty
            });

            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();

            AssertUidAppearsBefore(html, movedTask.Uid, existingDoneTask.Uid);
        }

        [Fact]
        public async SystemTask Board_HandlesMissingOrInvalidCompletionTimestamps_Gracefully()
        {
            var validDoneTask = await CreateTaskAsync("Valid done", "done");
            var missingTimestampTask = await CreateTaskAsync("Missing timestamp", "done");
            var invalidTimestampTask = await CreateTaskAsync("Invalid timestamp", "done");

            await UpdateStoredUpdatedAtAsync(missingTimestampTask.Uid, string.Empty);
            await UpdateStoredUpdatedAtAsync(invalidTimestampTask.Uid, "not-a-date");

            var response = await _client.GetAsync("/Index?handler=Refresh");
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains(validDoneTask.Title, html);
            Assert.Contains(missingTimestampTask.Title, html);
            Assert.Contains(invalidTimestampTask.Title, html);
            AssertUidAppearsBefore(html, validDoneTask.Uid, missingTimestampTask.Uid);
            AssertUidAppearsBefore(html, validDoneTask.Uid, invalidTimestampTask.Uid);
        }

        private async System.Threading.Tasks.Task<string> RefreshBoardHtmlAsync()
        {
            var response = await _client.GetAsync("/Index?handler=Refresh");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private async System.Threading.Tasks.Task<TaskDto> CreateTaskAsync(string title, string status)
        {
            var response = await _client.PostAsJsonAsync("/api/tasks", new TaskCreateDto
            {
                Uid = NewUid(),
                Title = title,
                Priority = "medium",
                Status = status
            });

            response.EnsureSuccessStatusCode();

            var task = await response.Content.ReadFromJsonAsync<TaskDto>();
            Assert.NotNull(task);
            return task!;
        }

        private async System.Threading.Tasks.Task<HttpResponseMessage> PostRazorHandlerAsync(string handler, Dictionary<string, string> formValues)
        {
            var pageResponse = await _client.GetAsync("/");
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

            return await _client.SendAsync(request);
        }

        private async SystemTask UpdateStoredUpdatedAtAsync(string uid, string updatedAt)
        {
            await using var connection = new SqliteConnection($"Data Source={_factory.TestDbPath}");
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE tasks SET updated_at = @updatedAt WHERE uid = @uid";
            command.Parameters.AddWithValue("@uid", uid);
            command.Parameters.AddWithValue("@updatedAt", updatedAt);
            await command.ExecuteNonQueryAsync();
        }

        private static void AssertUidAppearsBefore(string html, string firstUid, string secondUid)
        {
            var firstIndex = html.IndexOf($"data-uid=\"{firstUid}\"", StringComparison.Ordinal);
            var secondIndex = html.IndexOf($"data-uid=\"{secondUid}\"", StringComparison.Ordinal);

            Assert.True(firstIndex >= 0, $"Expected to find card with uid {firstUid} in board HTML.");
            Assert.True(secondIndex >= 0, $"Expected to find card with uid {secondUid} in board HTML.");
            Assert.True(firstIndex < secondIndex, $"Expected uid {firstUid} to appear before uid {secondUid}.");
        }
    }
}
