using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using Task.Core;
using TaskItem = Task.Core.TaskItem;

namespace Task.Api.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ITaskService _taskService;

        public IndexModel(ITaskService taskService)
        {
            _taskService = taskService;
        }

        public List<TaskItem> TaskItems { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? SearchQuery { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FilterStatus { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FilterAssignee { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FilterLabel { get; set; }

        public List<string> AllLabels { get; set; } = new();
        public List<string> AllAssignees { get; set; } = new();

        public async System.Threading.Tasks.Task OnGetAsync()
        {
            var tasks = await _taskService.GetAllTasksAsync();
            PopulateOptions(tasks);
            TaskItems = FilterTasks(tasks);
        }

        public async System.Threading.Tasks.Task<IActionResult> OnGetRefresh()
        {
            var tasks = await _taskService.GetAllTasksAsync();
            PopulateOptions(tasks);
            TaskItems = FilterTasks(tasks);
            return Partial("_BoardContainer", this);
        }

        public async System.Threading.Tasks.Task<IActionResult> OnPostUpdateStatus(string uid, string status, string? blockReason)
        {
            var task = await _taskService.GetTaskByUidAsync(uid);
            if (task == null)
            {
                return NotFound();
            }

            task.Status = status;
            if (string.IsNullOrWhiteSpace(status))
            {
                return BadRequest("Status is required");
            }

            if (!string.Equals(status, "blocked", StringComparison.OrdinalIgnoreCase))
            {
                task.BlockReason = null;
            }
            else if (blockReason != null)
            {
                task.BlockReason = string.IsNullOrEmpty(blockReason) ? null : blockReason;
            }
            await _taskService.UpdateTaskAsync(task);

            var tasks = await _taskService.GetAllTasksAsync();
            PopulateOptions(tasks);
            TaskItems = FilterTasks(tasks);
            return Partial("_BoardContainer", this);
        }

        public async System.Threading.Tasks.Task<IActionResult> OnPostCreateTask(string title, string? description, string priority, DateTime? dueDate, List<string>? tags, string? project, string? assignee, string? status, string? blockReason)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return BadRequest("Title is required");
            }

            tags ??= new List<string>();

            if (!string.IsNullOrEmpty(blockReason) && !string.Equals(status, "blocked", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Block reason is only allowed when status is blocked.");
            }

            await _taskService.AddTaskAsync(title, description, priority, dueDate, tags, project, null, assignee, status ?? "todo", blockReason);

            var tasks = await _taskService.GetAllTasksAsync();
            PopulateOptions(tasks);
            TaskItems = FilterTasks(tasks);
            return Partial("_BoardContainer", this);
        }

        public async System.Threading.Tasks.Task<IActionResult> OnPostEditTask(string uid, string title, string? description, string priority, DateTime? dueDate, List<string>? tags, string? project, string? assignee, string? status, string? blockReason)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return BadRequest("Title is required");
            }

            var task = await _taskService.GetTaskByUidAsync(uid);
            if (task == null)
            {
                return NotFound();
            }

            task.Title = title;
            task.Description = description;
            task.Priority = priority;
            task.DueDate = dueDate;
            task.Tags = tags ?? new List<string>();
            task.Project = project;
            task.Assignee = assignee;
            if (!string.IsNullOrWhiteSpace(status))
            {
                task.Status = status;
                if (!string.Equals(status, "blocked", StringComparison.OrdinalIgnoreCase))
                {
                    task.BlockReason = null;
                }
            }
            if (blockReason != null)
            {
                if (!string.Equals(task.Status, "blocked", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("Block reason is only allowed when status is blocked.");
                }
                task.BlockReason = string.IsNullOrEmpty(blockReason) ? null : blockReason;
            }
            task.UpdatedAt = DateTime.UtcNow;

            await _taskService.UpdateTaskAsync(task);

            var tasks = await _taskService.GetAllTasksAsync();
            PopulateOptions(tasks);
            TaskItems = FilterTasks(tasks);
            return Partial("_BoardContainer", this);
        }

        public IActionResult OnGetCreateModal()
        {
            return Partial("_CreateTaskModal");
        }

        public async System.Threading.Tasks.Task<IActionResult> OnGetEditModal(string uid)
        {
            var task = await _taskService.GetTaskByUidAsync(uid);
            if (task == null)
            {
                return NotFound();
            }
            return Partial("_EditTaskModal", task);
        }

        public async System.Threading.Tasks.Task<IActionResult> OnPostDeleteTask(string uid)
        {
            var task = await _taskService.GetTaskByUidAsync(uid);
            if (task == null)
            {
                return NotFound();
            }

            await _taskService.DeleteTaskAsync(task.Uid);

            var tasks = await _taskService.GetAllTasksAsync();
            PopulateOptions(tasks);
            TaskItems = FilterTasks(tasks);
            return Partial("_BoardContainer", this);
        }

        public async System.Threading.Tasks.Task<IActionResult> OnPostClearBoard()
        {
            await _taskService.ArchiveAllTasksAsync();
            var tasks = await _taskService.GetAllTasksAsync();
            PopulateOptions(tasks);
            TaskItems = FilterTasks(tasks);
            return Partial("_BoardContainer", this);
        }

        private void PopulateOptions(List<TaskItem> tasks)
        {
            AllLabels = tasks.SelectMany(t => t.Tags ?? new List<string>()).Distinct().OrderBy(t => t).ToList();
            AllAssignees = tasks.Where(t => !string.IsNullOrEmpty(t.Assignee)).Select(t => t.Assignee!).Distinct().OrderBy(a => a).ToList();
        }

        private List<TaskItem> FilterTasks(List<TaskItem> tasks)
        {
            var query = tasks.AsQueryable();
            query = query.Where(t => !t.Archived);

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                query = query.Where(t => t.Title.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                        (t.Description != null && t.Description.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)) ||
                                        (t.Tags != null && t.Tags.Any(tag => tag.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))));
            }

            if (!string.IsNullOrWhiteSpace(FilterStatus))
            {
                query = query.Where(t => t.Status.Equals(FilterStatus, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(FilterAssignee))
            {
                query = query.Where(t => string.Equals(t.Assignee, FilterAssignee, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(FilterLabel))
            {
                query = query.Where(t => t.Tags != null && t.Tags.Contains(FilterLabel));
            }

            return query.ToList();
        }
    }
}
