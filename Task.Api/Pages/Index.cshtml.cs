using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using TaskItem = Task.Api.TaskItem;

namespace Task.Api.Pages
{
    public class IndexModel : PageModel
    {
        private readonly Database _database;

        public IndexModel(Database database)
        {
            _database = database;
        }

        public List<TaskItem> TaskItems { get; set; } = new();

        public async System.Threading.Tasks.Task OnGetAsync()
        {
            TaskItems = await _database.GetAllTasks();
        }

        public async System.Threading.Tasks.Task<IActionResult> OnGetRefresh()
        {
            TaskItems = await _database.GetAllTasks();
            return Partial("_KanbanBoard", TaskItems);
        }

        public async System.Threading.Tasks.Task<IActionResult> OnPostUpdateStatus(string uid, string status)
        {
            var task = await _database.GetTaskByUid(uid);
            if (task == null)
            {
                return NotFound();
            }

            task.Status = status;
            await _database.UpdateTask(task);

            TaskItems = await _database.GetAllTasks();
            return Partial("_KanbanBoard", TaskItems);
        }

        public async System.Threading.Tasks.Task<IActionResult> OnPostCreateTask(string title, string? description, string priority, DateTime? dueDate, List<string>? tags)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return BadRequest("Title is required");
            }

            tags ??= new List<string>();

            var task = await _database.AddTask(title, description, priority, dueDate, tags);
            TaskItems = await _database.GetAllTasks();
            return Partial("_KanbanBoard", TaskItems);
        }

        public async System.Threading.Tasks.Task<IActionResult> OnPostEditTask(string uid, string title, string? description, string priority, DateTime? dueDate, List<string>? tags)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return BadRequest("Title is required");
            }

            var task = await _database.GetTaskByUid(uid);
            if (task == null)
            {
                return NotFound();
            }

            task.Title = title;
            task.Description = description;
            task.Priority = priority;
            task.DueDate = dueDate;
            task.Tags = tags ?? new List<string>();
            task.UpdatedAt = DateTime.Now;

            await _database.UpdateTask(task);

            TaskItems = await _database.GetAllTasks();
            return Partial("_KanbanBoard", TaskItems);
        }

        public IActionResult OnGetCreateModal()
        {
            return Partial("_CreateTaskModal");
        }

        public async System.Threading.Tasks.Task<IActionResult> OnGetEditModal(string uid)
        {
            var task = await _database.GetTaskByUid(uid);
            if (task == null)
            {
                return NotFound();
            }
            return Partial("_EditTaskModal", task);
        }

        public async System.Threading.Tasks.Task<IActionResult> OnPostDeleteTask(string uid)
        {
            var task = await _database.GetTaskByUid(uid);
            if (task == null)
            {
                return NotFound();
            }

            await _database.DeleteTask(task.Id.ToString());

            TaskItems = await _database.GetAllTasks();
            return Partial("_KanbanBoard", TaskItems);
        }

        public async System.Threading.Tasks.Task<IActionResult> OnPostClearBoard()
        {
            await _database.ClearAllTasksAsync();

            TaskItems = await _database.GetAllTasks();
            return Partial("_KanbanBoard", TaskItems);
        }
    }
}