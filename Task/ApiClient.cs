using System.Net.Http.Json;
using System.Text.Json;

namespace TaskApp;

public class ApiClient : ITaskService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiClient(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // API client doesn't need initialization
        await System.Threading.Tasks.Task.CompletedTask;
    }

    public async Task<List<TaskItem>> GetAllTasksAsync(
        string? status = null,
        string? priority = null,
        string? project = null,
        string? tags = null,
        DateTime? dueBefore = null,
        DateTime? dueAfter = null,
        int? limit = null,
        int? offset = null,
        string? sortBy = null,
        string? sortOrder = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>();

        if (!string.IsNullOrEmpty(status)) queryParams.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrEmpty(priority)) queryParams.Add($"priority={Uri.EscapeDataString(priority)}");
        if (!string.IsNullOrEmpty(project)) queryParams.Add($"project={Uri.EscapeDataString(project)}");
        if (!string.IsNullOrEmpty(tags)) queryParams.Add($"tags={Uri.EscapeDataString(tags)}");
        if (dueBefore.HasValue) queryParams.Add($"dueBefore={dueBefore.Value:yyyy-MM-dd}");
        if (dueAfter.HasValue) queryParams.Add($"dueAfter={dueAfter.Value:yyyy-MM-dd}");
        if (limit.HasValue) queryParams.Add($"limit={limit}");
        if (offset.HasValue) queryParams.Add($"offset={offset}");
        if (!string.IsNullOrEmpty(sortBy)) queryParams.Add($"sortBy={Uri.EscapeDataString(sortBy)}");
        if (!string.IsNullOrEmpty(sortOrder)) queryParams.Add($"sortOrder={Uri.EscapeDataString(sortOrder)}");

        var queryString = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
        var url = $"{_baseUrl}/api/tasks{queryString}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var dtos = await response.Content.ReadFromJsonAsync<List<TaskDto>>(_jsonOptions, cancellationToken);
        return dtos?.Select(MapFromDto).ToList() ?? new List<TaskItem>();
    }

    public async Task<TaskItem?> GetTaskByUidAsync(string uid, CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/api/tasks/{uid}";
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<TaskDto>(_jsonOptions, cancellationToken);
        return dto != null ? MapFromDto(dto) : null;
    }

    public async Task<TaskItem> AddTaskAsync(
        string title,
        string? description,
        string priority,
        DateTime? dueDate,
        List<string> tags,
        string? project = null,
        List<string>? dependsOn = null,
        string? status = "todo",
        CancellationToken cancellationToken = default)
    {
        var taskStatus = !string.IsNullOrEmpty(status) && new[] { "todo", "done", "in_progress" }.Contains(status.ToLower())
            ? status.ToLower()
            : "todo";

        var createDto = new TaskCreateDto
        {
            Title = title,
            Description = description,
            Priority = priority,
            DueDate = dueDate,
            Tags = tags,
            Project = project,
            DependsOn = dependsOn,
            Status = taskStatus
        };

        var url = $"{_baseUrl}/api/tasks";
        var response = await _httpClient.PostAsJsonAsync(url, createDto, _jsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<TaskDto>(_jsonOptions, cancellationToken);
        return MapFromDto(dto!);
    }

    public async Task UpdateTaskAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        var updateDto = new TaskUpdateDto
        {
            Title = task.Title,
            Description = task.Description,
            Priority = task.Priority,
            DueDate = task.DueDate,
            Tags = task.Tags,
            Status = task.Status
        };

        var url = $"{_baseUrl}/api/tasks/{task.Uid}";
        var response = await _httpClient.PutAsJsonAsync(url, updateDto, _jsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteTaskAsync(string uid, CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/api/tasks/{uid}";
        var response = await _httpClient.DeleteAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task CompleteTaskAsync(string uid, CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/api/tasks/{uid}/complete";
        var response = await _httpClient.PatchAsync(url, null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<TaskItem>> SearchTasksAsync(
        string query,
        string type = "fts",
        CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/api/tasks/search?q={Uri.EscapeDataString(query)}&type={Uri.EscapeDataString(type)}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var dtos = await response.Content.ReadFromJsonAsync<List<TaskDto>>(_jsonOptions, cancellationToken);
        return dtos?.Select(MapFromDto).ToList() ?? new List<TaskItem>();
    }

    public async Task<List<string>> GetAllUniqueTagsAsync(CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/api/tags";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<string>>(_jsonOptions, cancellationToken) ?? new List<string>();
    }

    public async Task<List<string>> GetAllUniqueProjectsAsync(CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/api/projects";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<string>>(_jsonOptions, cancellationToken) ?? new List<string>();
    }

    public async Task<List<TaskItem>> GetTasksDependingOnAsync(string uid, CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/api/tasks/{uid}/dependencies";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var dtos = await response.Content.ReadFromJsonAsync<List<TaskDto>>(_jsonOptions, cancellationToken);
        return dtos?.Select(MapFromDto).ToList() ?? new List<TaskItem>();
    }

    public async Task<bool> ValidateDependenciesAsync(string uid, List<string> dependsOn, CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/api/tasks/{uid}/validate-dependencies";
        var response = await _httpClient.PostAsJsonAsync(url, dependsOn, _jsonOptions, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private static TaskItem MapFromDto(TaskDto dto)
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
            DependsOn = dto.DependsOn ?? new List<string>(),
            Status = dto.Status,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt
        };
    }
}

// DTOs for API communication (simplified versions)
public class TaskDto
{
    public int Id { get; set; }
    public required string Uid { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required string Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? Project { get; set; }
    public List<string> DependsOn { get; set; } = new();
    public required string Status { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }
}

public class TaskCreateDto
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required string Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? Project { get; set; }
    public List<string>? DependsOn { get; set; }
    public string? Status { get; set; }
}

public class TaskUpdateDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public List<string>? Tags { get; set; }
    public string? Project { get; set; }
    public List<string>? DependsOn { get; set; }
    public string? Status { get; set; }
}