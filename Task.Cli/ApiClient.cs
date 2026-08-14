using System.Net.Http.Json;
using System.Text.Json;
using Task.Core;
using System.Threading.Tasks;

namespace Task.Cli;

public class ApiClient : ITaskService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>API key sent as the X-Api-Key header. When null, requests go out without credentials.</summary>
    public string? ApiKey { get; set; }

    public ApiClient(string baseUrl)
        : this(baseUrl, new HttpClient())
    {
    }

    /// <summary>Allows tests to inject an HttpClient wired to an in-process TestServer.</summary>
    public ApiClient(string baseUrl, HttpClient httpClient)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    private async System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
    {
        using var request = requestFactory();
        if (!string.IsNullOrEmpty(ApiKey))
        {
            request.Headers.Add("X-Api-Key", ApiKey);
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException(BuildUnauthorizedMessage());
        }

        return response;
    }

    private string BuildUnauthorizedMessage()
    {
        return $"Your API key is missing, invalid, or revoked. Create one at {_baseUrl}/keys or run `task config set api.key <key>`.";
    }

    public async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await System.Threading.Tasks.Task.CompletedTask;
    }

    public async System.Threading.Tasks.Task<List<TaskItem>> GetAllTasksAsync(
        string? status = null,
        string? priority = null,
        string? project = null,
        string? assignee = null,
        string? tags = null,
        DateTime? dueBefore = null,
        DateTime? dueAfter = null,
        int? limit = null,
        int? offset = null,
        string? sortBy = null,
        string? sortOrder = null,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>();

        if (!string.IsNullOrEmpty(status)) queryParams.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrEmpty(priority)) queryParams.Add($"priority={Uri.EscapeDataString(priority)}");
        if (!string.IsNullOrEmpty(project)) queryParams.Add($"project={Uri.EscapeDataString(project)}");
        if (!string.IsNullOrEmpty(assignee)) queryParams.Add($"assignee={Uri.EscapeDataString(assignee)}");
        if (!string.IsNullOrEmpty(tags)) queryParams.Add($"tags={Uri.EscapeDataString(tags)}");
        if (dueBefore.HasValue) queryParams.Add($"dueBefore={dueBefore.Value:yyyy-MM-dd}");
        if (dueAfter.HasValue) queryParams.Add($"dueAfter={dueAfter.Value:yyyy-MM-dd}");
        if (limit.HasValue) queryParams.Add($"limit={limit}");
        if (offset.HasValue) queryParams.Add($"offset={offset}");
        if (!string.IsNullOrEmpty(sortBy)) queryParams.Add($"sortBy={Uri.EscapeDataString(sortBy)}");
        if (!string.IsNullOrEmpty(sortOrder)) queryParams.Add($"sortOrder={Uri.EscapeDataString(sortOrder)}");

        var queryString = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
        var url = $"{_baseUrl}/api/tasks{queryString}";

        var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);
        response.EnsureSuccessStatusCode();

        var dtos = await response.Content.ReadFromJsonAsync<List<TaskDto>>(_jsonOptions, cancellationToken);
        return dtos?.Select(MapFromDto).ToList() ?? new List<TaskItem>();
    }

    public async System.Threading.Tasks.Task<TaskItem?> GetTaskByUidAsync(string uid, string? userId = null, CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/api/tasks/{uid}";
        var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<TaskDto>(_jsonOptions, cancellationToken);
        return dto != null ? MapFromDto(dto) : null;
    }

    public async System.Threading.Tasks.Task<TaskItem> AddTaskAsync(
        string uid,
        string title,
        string? description,
        string priority,
        DateTime? dueDate,
        List<string> tags,
        string? project = null,
        List<string>? dependsOn = null,
        string? assignee = null,
        string? status = "todo",
        string? blockReason = null,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var taskStatus = !string.IsNullOrEmpty(status) && new[] { "todo", "done", "in_progress", "blocked" }.Contains(status.ToLower())
            ? status.ToLower()
            : "todo";

        if (!string.IsNullOrEmpty(blockReason) && !string.Equals(taskStatus, "blocked", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Block reason is only allowed when status is blocked.");
        }

        var createDto = new TaskCreateDto
        {
            Uid = uid,
            Title = title,
            Description = description,
            Priority = priority,
            DueDate = dueDate,
            Tags = tags,
            Project = project,
            Assignee = assignee,
            DependsOn = dependsOn,
            Status = taskStatus,
            BlockReason = blockReason
        };

        var url = $"{_baseUrl}/api/tasks";
        var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(createDto, options: _jsonOptions)
        }, cancellationToken);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<TaskDto>(_jsonOptions, cancellationToken);
        return MapFromDto(dto!);
    }

    public async System.Threading.Tasks.Task UpdateTaskAsync(TaskItem task, string? userId = null, CancellationToken cancellationToken = default)
    {
        var updateDto = new TaskUpdateDto
        {
            Title = task.Title,
            Description = task.Description,
            Priority = task.Priority,
            DueDate = task.DueDate,
            Tags = task.Tags,
            Status = task.Status,
            BlockReason = task.BlockReason,
            Archived = task.Archived,
            ArchivedAt = task.ArchivedAt
        };

        var url = $"{_baseUrl}/api/tasks/{task.Uid}";
        var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(updateDto, options: _jsonOptions)
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async System.Threading.Tasks.Task DeleteTaskAsync(string uid, string? userId = null, CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/api/tasks/{uid}";
        var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Delete, url), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async System.Threading.Tasks.Task CompleteTaskAsync(string uid, string? userId = null, CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/api/tasks/{uid}/complete";
        var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Patch, url), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async System.Threading.Tasks.Task<List<TaskItem>> SearchTasksAsync(
        string query,
        string type = "fts",
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/api/tasks/search?q={Uri.EscapeDataString(query)}&type={Uri.EscapeDataString(type)}";
        var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);
        response.EnsureSuccessStatusCode();

        var dtos = await response.Content.ReadFromJsonAsync<List<TaskDto>>(_jsonOptions, cancellationToken);
        return dtos?.Select(MapFromDto).ToList() ?? new List<TaskItem>();
    }

    public async System.Threading.Tasks.Task<List<string>> GetAllUniqueTagsAsync(string? userId = null, CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/api/tags";
        var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<string>>(_jsonOptions, cancellationToken) ?? new List<string>();
    }

    public async System.Threading.Tasks.Task<List<string>> GetAllUniqueProjectsAsync(string? userId = null, CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/api/projects";
        var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<string>>(_jsonOptions, cancellationToken) ?? new List<string>();
    }

    public async System.Threading.Tasks.Task<List<string>> GetAllUniqueAssigneesAsync(string? userId = null, CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/api/assignees";
        var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<string>>(_jsonOptions, cancellationToken) ?? new List<string>();
    }

    public async System.Threading.Tasks.Task<List<TaskItem>> GetTasksDependingOnAsync(string uid, string? userId = null, CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/api/tasks/{uid}/dependencies";
        var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);
        response.EnsureSuccessStatusCode();

        var dtos = await response.Content.ReadFromJsonAsync<List<TaskDto>>(_jsonOptions, cancellationToken);
        return dtos?.Select(MapFromDto).ToList() ?? new List<TaskItem>();
    }

    public async System.Threading.Tasks.Task<bool> ValidateDependenciesAsync(string uid, List<string> dependsOn, string? userId = null, CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/api/tasks/{uid}/validate-dependencies";
        var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(dependsOn, options: _jsonOptions)
        }, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public System.Threading.Tasks.Task ArchiveAllTasksAsync(string? userId = null, System.Threading.CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private static TaskItem MapFromDto(Task.Core.TaskDto dto)
    {
        return new TaskItem
        {
            Id = dto.Id,
            Uid = dto.Uid,
            Title = dto.Title,
            Description = dto.Description,
            Priority = dto.Priority,
            DueDate = dto.DueDate,
            Tags = dto.Tags,
            Project = dto.Project,
            Assignee = dto.Assignee,
            DependsOn = dto.DependsOn ?? new List<string>(),
            Status = dto.Status ?? "todo",
            BlockReason = dto.BlockReason,
            Archived = dto.Archived,
            ArchivedAt = dto.ArchivedAt,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt
        };
    }
}
