# Finalized Plan: Razor Pages & HTMX Kanban Board

Based on your feedback, we'll expand the task statuses to support a proper Kanban workflow. Here's the complete, actionable plan:

## Status Expansion
- **Current**: 2 statuses ('pending', 'completed')
- **New**: Add 3 statuses ('todo', 'in_progress', 'done') for standard Kanban flow
- **Migration**: Update existing 'pending' tasks to 'todo', 'completed' to 'done'
- **Database**: Modify schema constraint to `CHECK(status IN ('todo', 'in_progress', 'done'))`

## Implementation Steps

### 1. Database Schema Update
- Update SCHEMA.sql to include new status values
- Create migration script to update existing tasks
- Update Database.cs if needed for status validation

### 2. API Updates
- Update TaskDto and controllers to handle new statuses
- Ensure all endpoints support the expanded status set

### 3. Add Razor Pages Infrastructure
- Enable Razor Pages in Program.cs
- Create Pages folder structure:
  - Pages/Shared/_Layout.cshtml (with HTMX CDN)
  - Pages/_ViewStart.cshtml
  - Pages/Index.cshtml (main board page)
  - Pages/Index.cshtml.cs (PageModel)
  - Pages/Shared/_KanbanBoard.cshtml (partial view)

### 4. Board UI Implementation
- Index.cshtml: Container div with HTMX polling
- _KanbanBoard.cshtml: Responsive flex layout with 3 columns (Todo, In Progress, Done)
- Card styling with priority indicators and drag handles

### 5. HTMX Polling (1s Refresh)
- PageModel handler: `OnGetRefresh()` returns `PartialView("_KanbanBoard", tasks)`
- HTMX attributes: `hx-get="/Index?handler=Refresh" hx-trigger="every 1s" hx-target="#board"`
- Optimization: Only render when tasks have changed since last poll

### 6. Drag-and-Drop Status Updates
- Cards: `draggable="true"` with data attributes
- Columns: `hx-post="/Index?handler=UpdateStatus"` with drop triggers
- PageModel handler: `OnPostUpdateStatus()` updates status and refreshes board

### 7. Additional Features
- Task creation modal with HTMX
- Inline editing for titles/descriptions
- Delete with confirmation
- Basic filtering by priority

### 8. Styling & UX
- CSS for mobile-responsive Kanban layout
- Loading indicators and error states
- Touch-friendly drag-drop

### 9. Testing & Optimization
- Browser testing for HTMX interactions
- Performance profiling for 1s polling
- Cross-browser compatibility

## Technical Considerations
- **HTMX Version**: 2.0.8 via CDN
- **No API Polling**: Direct page polling for better MVC integration
- **Partial Responses**: Efficient HTML fragment updates
- **Change Detection**: Timestamp-based to minimize renders

## Estimated Effort
- Schema + API updates: 2-3 hours
- Razor Pages setup: 1 hour
- Board implementation: 4-6 hours
- HTMX features: 3-4 hours
- Testing & polish: 2-3 hours

**Total: ~12-17 hours**

This plan provides a fully functional Kanban board with real-time updates. The expansion to 3 statuses will create a proper workflow: Todo → In Progress → Done.

**Ready to begin implementation when you give the go-ahead.** If you'd like any adjustments to the plan, let me know.