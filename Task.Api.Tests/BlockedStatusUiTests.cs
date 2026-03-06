using System.Net.Http;
using System.Net.Http.Json;
using SystemTask = System.Threading.Tasks.Task;
using Task.Core;
using Xunit;

namespace Task.Api.Tests.IntegrationTests
{
    public class BlockedStatusUiTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public BlockedStatusUiTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
            _factory.ClearDatabase();
        }

        [Fact]
        public async SystemTask Board_IncludesBlockedColumn()
        {
            var response = await _client.GetAsync("/");
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("data-status=\"blocked\"", html);
            Assert.Contains("<h2>Blocked</h2>", html);
        }

        [Fact]
        public async SystemTask CreateModal_ShowsBlockedStatusAndBlockReasonGroup()
        {
            var response = await _client.GetAsync("/Index?handler=CreateModal");
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("value=\"blocked\"", html);
            Assert.Contains("block-reason-group", html);
            Assert.Contains("Reason for block (optional)", html);
        }

        [Fact]
        public async SystemTask EditModal_ShowsBlockedReasonWhenTaskIsBlocked()
        {
            var task = await CreateBlockedTaskAsync("Needs approval", "Waiting on approval");

            var response = await _client.GetAsync($"/Index?handler=EditModal&uid={task.Uid}");
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("value=\"blocked\"", html);
            Assert.Contains("edit-block-reason-group", html);
            Assert.Contains("display: block", html);
            Assert.Contains("Waiting on approval", html);
        }

        [Fact]
        public async SystemTask Board_DisplaysBlockedReason_WhenProvided()
        {
            await CreateBlockedTaskAsync("Blocked UI", "Waiting on dependency");

            var response = await _client.GetAsync("/Index?handler=Refresh");
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("task-block-reason", html);
            Assert.Contains("Waiting on dependency", html);
        }

        [Fact]
        public async SystemTask Board_OmitsBlockedReason_WhenEmpty()
        {
            await CreateBlockedTaskAsync("Blocked UI", null);

            var response = await _client.GetAsync("/Index?handler=Refresh");
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();

            Assert.DoesNotContain("task-block-reason", html);
        }

        private async System.Threading.Tasks.Task<TaskDto> CreateBlockedTaskAsync(string title, string? blockReason)
        {
            var newTask = new TaskCreateDto
            {
                Title = title,
                Priority = "medium",
                Status = "blocked",
                BlockReason = blockReason
            };

            var response = await _client.PostAsJsonAsync("/api/tasks", newTask);
            response.EnsureSuccessStatusCode();

            var task = await response.Content.ReadFromJsonAsync<TaskDto>();
            Assert.NotNull(task);
            return task!;
        }
    }
}
