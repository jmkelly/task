using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Task.Core;
using Task.Core.Providers.Telegram;

namespace Task.Api;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly Database _database;
    private readonly ILogger<TasksController> _logger;
    private readonly TelegramNotificationService _telegramNotifications;

    public TasksController(
        Database database,
        ILogger<TasksController> logger,
        TelegramNotificationService telegramNotifications)
    {
        _database = database;
        _logger = logger;
        _telegramNotifications = telegramNotifications;
    }

    [HttpGet]
    public async Task<IActionResult> GetTasks(
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] string? project,
        [FromQuery] string? assignee,
        [FromQuery] string? tags,
        [FromQuery] DateTime? dueBefore,
        [FromQuery] DateTime? dueAfter,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortOrder)
    {
        try
        {
            var allTasks = await _database.GetAllTasksAsync();
            var filteredTasks = allTasks;

            if (!string.IsNullOrEmpty(status))
            {
                filteredTasks = filteredTasks.Where(t => t.Status == status).ToList();
            }

            if (!string.IsNullOrEmpty(priority))
            {
                filteredTasks = filteredTasks.Where(t => t.Priority == priority).ToList();
            }

            if (!string.IsNullOrEmpty(project))
            {
                filteredTasks = filteredTasks.Where(t => t.Project == project).ToList();
            }

            if (!string.IsNullOrEmpty(assignee))
            {
                filteredTasks = filteredTasks.Where(t => t.Assignee == assignee).ToList();
            }

            if (!string.IsNullOrEmpty(tags))
            {
                var tagList = tags.Split(',').Select(t => t.Trim()).ToList();
                filteredTasks = filteredTasks.Where(t => tagList.Any(tag => t.Tags.Contains(tag))).ToList();
            }

            if (dueBefore.HasValue)
            {
                filteredTasks = filteredTasks.Where(t => t.DueDate.HasValue && t.DueDate <= dueBefore).ToList();
            }

            if (dueAfter.HasValue)
            {
                filteredTasks = filteredTasks.Where(t => t.DueDate.HasValue && t.DueDate >= dueAfter).ToList();
            }

            if (!string.IsNullOrEmpty(sortBy))
            {
                filteredTasks = sortBy.ToLower() switch
                {
                    "priority" => sortOrder?.ToLower() == "desc" ?
                        filteredTasks.OrderByDescending(t => t.Priority).ToList() :
                        filteredTasks.OrderBy(t => t.Priority).ToList(),
                    "duedate" => sortOrder?.ToLower() == "desc" ?
                        filteredTasks.OrderByDescending(t => t.DueDate).ToList() :
                        filteredTasks.OrderBy(t => t.DueDate).ToList(),
                    "createdat" => sortOrder?.ToLower() == "desc" ?
                        filteredTasks.OrderByDescending(t => t.CreatedAt).ToList() :
                        filteredTasks.OrderBy(t => t.CreatedAt).ToList(),
                    "title" => sortOrder?.ToLower() == "desc" ?
                        filteredTasks.OrderByDescending(t => t.Title).ToList() :
                        filteredTasks.OrderBy(t => t.Title).ToList(),
                    _ => filteredTasks
                };
            }

            if (offset.HasValue)
            {
                filteredTasks = filteredTasks.Skip(offset.Value).ToList();
            }

            if (limit.HasValue)
            {
                filteredTasks = filteredTasks.Take(limit.Value).ToList();
            }

            await _telegramNotifications.NotifyWhenNoActiveTasksAsync(allTasks, HttpContext.RequestAborted);

            var dtos = filteredTasks.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tasks");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{uid}")]
    public async Task<IActionResult> GetTask(string uid)
    {
        try
        {
            var task = await _database.GetTaskByUidAsync(uid);
            if (task == null)
            {
                return NotFound();
            }

            return Ok(MapToDto(task));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task {Uid}", uid);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask([FromBody] TaskCreateDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var task = await _database.AddTaskAsync(
                dto.Title,
                dto.Description,
                dto.Priority,
                dto.DueDate,
                dto.Tags,
                dto.Project,
                dto.Assignee);

            if (dto.Archived.HasValue || dto.ArchivedAt.HasValue)
            {
                task.Archived = dto.Archived ?? false;
                task.ArchivedAt = dto.ArchivedAt;
                await _database.UpdateTaskAsync(task);
            }

            return CreatedAtAction(nameof(GetTask), new { uid = task.Uid }, MapToDto(task));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating task");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{uid}")]
    public async Task<IActionResult> UpdateTask(string uid, [FromBody] TaskUpdateDto dto)
    {
        try
        {
            var existingTask = await _database.GetTaskByUidAsync(uid);
            if (existingTask == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(dto.Title))
                existingTask.Title = dto.Title;
            if (dto.Description != null)
                existingTask.Description = dto.Description;
            if (!string.IsNullOrEmpty(dto.Priority))
                existingTask.Priority = dto.Priority;
            if (dto.DueDate.HasValue)
                existingTask.DueDate = dto.DueDate;
            if (dto.Tags != null)
                existingTask.Tags = dto.Tags;
            if (!string.IsNullOrEmpty(dto.Status))
                existingTask.Status = dto.Status;
            if (!string.IsNullOrEmpty(dto.Project))
                existingTask.Project = dto.Project;
            if (dto.Assignee != null)
                existingTask.Assignee = dto.Assignee;
            if (dto.Archived.HasValue)
                existingTask.Archived = dto.Archived.Value;
            if (dto.ArchivedAt.HasValue || dto.Archived.HasValue)
                existingTask.ArchivedAt = dto.ArchivedAt;

            await _database.UpdateTaskAsync(existingTask);

            return Ok(MapToDto(existingTask));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating task {Uid}", uid);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{uid}")]
    public async Task<IActionResult> DeleteTask(string uid)
    {
        try
        {
            var existingTask = await _database.GetTaskByUidAsync(uid);
            if (existingTask == null)
            {
                return NotFound();
            }

            await _database.DeleteTaskAsync(existingTask.Uid);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting task {Uid}", uid);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPatch("{uid}/complete")]
    public async Task<IActionResult> CompleteTask(string uid)
    {
        try
        {
            var existingTask = await _database.GetTaskByUidAsync(uid);
            if (existingTask == null)
            {
                return NotFound();
            }

            await _database.CompleteTaskAsync(existingTask.Uid);

            var updatedTask = await _database.GetTaskByUidAsync(uid);
            return Ok(MapToDto(updatedTask!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing task {Uid}", uid);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchTasks([FromQuery] string q, [FromQuery] string? type = "fts")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest("Query parameter 'q' is required");
            }

            List<TaskItem> tasks;
            switch (type?.ToLower())
            {
                case "semantic":
                    tasks = await _database.SearchTasksSemanticAsync(q);
                    break;
                case "hybrid":
                    tasks = await _database.SearchTasksHybridAsync(q);
                    break;
                case "fts":
                default:
                    tasks = await _database.SearchTasksFTSAsync(q);
                    break;
            }

            var dtos = tasks.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching tasks with query '{Query}'", q);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportTasks([FromQuery] string format = "json")
    {
        try
        {
            var tasks = await _database.GetAllTasksAsync();

            if (format.ToLower() == "csv")
            {
                var csv = "Id,Uid,Title,Description,Priority,DueDate,Tags,Status,Archived,ArchivedAt,CreatedAt,UpdatedAt\n";
                foreach (var task in tasks)
                {
                    csv += $"{task.Id},{task.Uid},\"{task.Title.Replace("\"", "\"\"")}\",\"{(task.Description ?? "").Replace("\"", "\"\"")}\",{task.Priority},{task.DueDateString},\"{task.TagsString}\",{task.Status},{(task.Archived ? 1 : 0)},{task.ArchivedAt:yyyy-MM-dd HH:mm:ss},{task.CreatedAt:yyyy-MM-dd HH:mm:ss},{task.UpdatedAt:yyyy-MM-dd HH:mm:ss}\n";
                }
                return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "tasks.csv");
            }

            var dtos = tasks.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting tasks");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportTasks([FromBody] object data, [FromQuery] string format = "json")
    {
        try
        {
            List<TaskItem> tasksToImport;

            if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                if (data is not string csvContent)
                {
                    return BadRequest("CSV import requires string content in request body");
                }
                tasksToImport = ParseCsvTasks(csvContent);
            }
            else if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
            {
                var jsonString = JsonSerializer.Serialize(data);
                var importDtos = JsonSerializer.Deserialize<List<TaskImportDto>>(jsonString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                tasksToImport = importDtos?.Select(MapImportDtoToTaskItem).ToList() ?? new List<TaskItem>();
            }
            else
            {
                return BadRequest($"Unsupported format: {format}. Supported formats: json, csv");
            }

            if (!tasksToImport.Any())
            {
                return BadRequest("No valid tasks found to import");
            }

            var importedTasks = new List<TaskDto>();
            var errors = new List<string>();

            foreach (var task in tasksToImport)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(task.Title))
                    {
                        errors.Add("Task with empty title skipped");
                        continue;
                    }

                    task.Priority = string.IsNullOrEmpty(task.Priority) ? "medium" : task.Priority;
                    task.Status = string.IsNullOrEmpty(task.Status) ? "pending" : task.Status;

                    var addedTask = await _database.AddTaskAsync(
                        task.Title,
                        task.Description,
                        task.Priority,
                        task.DueDate,
                        task.Tags,
                        task.Project,
                        task.Assignee);

                    if (task.Archived || task.ArchivedAt.HasValue)
                    {
                        addedTask.Archived = task.Archived;
                        addedTask.ArchivedAt = task.ArchivedAt;
                        await _database.UpdateTaskAsync(addedTask);
                    }

                    importedTasks.Add(MapToDto(addedTask));
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to import task '{task.Title}': {ex.Message}");
                }
            }

            return Ok(new
            {
                imported = importedTasks.Count,
                total = tasksToImport.Count,
                tasks = importedTasks,
                errors = errors.Any() ? errors : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing tasks");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("/api/tags")]
    public async Task<IActionResult> GetTags()
    {
        try
        {
            var tags = await _database.GetAllUniqueTagsAsync();
            return Ok(tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tags");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("/api/assignees")]
    public async Task<IActionResult> GetAssignees()
    {
        try
        {
            var tasks = await _database.GetAllTasksAsync();
            var assignees = tasks.Where(t => !string.IsNullOrEmpty(t.Assignee)).Select(t => t.Assignee!).Distinct().OrderBy(a => a).ToList();
            return Ok(assignees);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting assignees");
            return StatusCode(500, "Internal server error");
        }
    }

    private static List<TaskItem> ParseCsvTasks(string csvContent)
    {
        var tasks = new List<TaskItem>();
        var lines = csvContent.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)).ToArray();

        if (lines.Length < 2)
        {
            throw new Exception("CSV must have at least a header row and one data row");
        }

        var headers = ParseCsvLine(lines[0]);
        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            headerMap[headers[i]] = i;
        }

        if (!headerMap.ContainsKey("title"))
        {
            throw new Exception("CSV must contain a 'Title' column");
        }

        for (int i = 1; i < lines.Length; i++)
        {
            var values = ParseCsvLine(lines[i]);
            if (values.Length != headers.Length)
            {
                throw new Exception($"Row {i + 1} has {values.Length} columns, expected {headers.Length}");
            }

            var task = new TaskItem
            {
                Id = 0,
                Uid = "",
                Title = values[headerMap["title"]],
                Description = headerMap.ContainsKey("description") ? values[headerMap["description"]] : null,
                Priority = headerMap.ContainsKey("priority") ? values[headerMap["priority"]] : "medium",
                DueDate = headerMap.ContainsKey("duedate") && !string.IsNullOrEmpty(values[headerMap["duedate"]]) &&
                         DateTime.TryParse(values[headerMap["duedate"]], out var dd) ? dd : null,
                Tags = headerMap.ContainsKey("tags") && !string.IsNullOrEmpty(values[headerMap["tags"]]) ?
                    values[headerMap["tags"]].Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList() :
                    new List<string>(),
                Status = headerMap.ContainsKey("status") ? values[headerMap["status"]] : "pending",
                Archived = headerMap.ContainsKey("archived") &&
                           (values[headerMap["archived"]] == "1" || values[headerMap["archived"]].Equals("true", StringComparison.OrdinalIgnoreCase)),
                ArchivedAt = headerMap.ContainsKey("archivedat") && DateTime.TryParse(values[headerMap["archivedat"]], out var archivedAt)
                    ? archivedAt
                    : null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            tasks.Add(task);
        }

        return tasks;
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result.ToArray();
    }

    private static TaskItem MapImportDtoToTaskItem(TaskImportDto dto)
    {
        return new TaskItem
        {
            Id = 0,
            Uid = "",
            Title = dto.Title,
            Description = dto.Description,
            Priority = dto.Priority ?? "medium",
            DueDate = dto.DueDate,
            Tags = dto.Tags ?? new List<string>(),
            Project = dto.Project,
            Assignee = dto.Assignee,
            Status = dto.Status ?? "pending",
            Archived = dto.Archived ?? false,
            ArchivedAt = dto.ArchivedAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static TaskDto MapToDto(TaskItem task)
    {
        return new TaskDto
        {
            Id = task.Id,
            Uid = task.Uid,
            Title = task.Title,
            Description = task.Description,
            Priority = task.Priority,
            DueDate = task.DueDate,
            Tags = task.Tags,
            Project = task.Project,
            Assignee = task.Assignee,
            Status = task.Status,
            Archived = task.Archived,
            ArchivedAt = task.ArchivedAt,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt
        };
    }
}
