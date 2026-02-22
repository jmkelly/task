# Plan: Add Optional "Project" and "Depends On" Support for Tasks in CLI App

## 1. Summary
Enhance the CLI task management app so that each task:
- Can optionally belong to a project.
- Can optionally depend on other tasks (dependencies).
Defaults: If not provided, the task belongs to no project and has no dependencies.

## 2. Motivation
- Enable logical organization of tasks into projects for better structure and filtering.
- Support task dependencies to enable workflow management and block tasks until prerequisites are complete.
- Backward compatible so existing tasks remain valid with current or default fields.

## 3. Goals & Non-goals
**Goals:**
- Add optional "project" and "depends on" fields to tasks.
- Allow user to specify these fields via CLI on creation/edit.
- Make it possible to list, filter, and query by project/dependees.
- Prevent circular dependencies where feasible.

**Non-goals:**
- Deep project management features (e.g. Gantt chart, detailed scheduling).
- External API sync or integrations.

## 4. Data Model Updates
- Update the task data structure to include:
  - `project: string | null` – Optional, defaults to null/unset.
  - `dependsOn: string[]` (array of task IDs) – Optional, default empty array.

- When persisting/loading tasks, ensure backward compatibility by inferring `project: null` and `dependsOn: []` for legacy tasks.

## 5. CLI and UI Changes
- CLI task creation and editing:
  - Add optional `--project <project_name>` flag.
  - Add optional `--depends-on <task-id>[,<task-id>...]` flag.
  - When listing or showing tasks, display project and dependencies if present.

- Filtering/Search:
  - Support filtering/listing tasks by project.
  - Support listing tasks waiting on completion of other tasks.

- Help text and docs: Update help messages for relevant commands to document new options.

## 6. Dependency Management & Error Handling
- On creation/edit:
  - Validate that referenced dependencies exist.
  - Prevent self-reference (task depending on itself).
  - Prevent circular dependencies if possible (optional advanced: detect indirect cycles).

- On marking a task complete:
  - Optionally warn or block if other tasks depend on it.

- When completing a dependent task, provide commands/UI to:
  - Show which incomplete dependencies are blocking it.

## 7. Migration & Backward Compatibility
- If persistent storage format changes, provide migration for legacy tasks (e.g., on first load, missing fields get default values).
- Do not require users to update existing tasks/data for new features to work on new tasks.

## 8. Testing Plan
- Unit tests for:
  - Creating tasks with/without project/dependencies.
  - Editing and validating dependencies (including cycle checks).
  - Listing/filtering/sorting tasks by project/dependency.
  - Backward compatibility with older data/tasks.

- Integration tests for:
  - Realistic CLI flows using new flags and options.

## 9. Documentation
- Update CLI help and documentation with new options, usage examples, and explanations.
- Provide sample config/data/task files illustrating both with and without the new fields.
