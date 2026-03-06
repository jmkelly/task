# Design Specification: Blocked Status Column

## Overview
This document outlines the user interface and user experience (UI/UX) specifications for introducing "Blocked" as a distinct, full status in our task management system. Currently, tasks move through standard statuses. This addition allows users to explicitly mark tasks that cannot progress, along with an optional reason for the blockage.

## Placement & Workflow
- **Column Order:** The "Blocked" column will be positioned immediately after "In Progress" and before "Done" or "Review" columns (e.g., `To Do` -> `In Progress` -> `Blocked` -> `Done`).
- **Full Status Integration:** "Blocked" is treated as a first-class status, identical in structural behavior to "To Do" or "In Progress". Tasks can be dragged and dropped into the "Blocked" column.

## Visual Design & Aesthetics
This design adheres strictly to the existing Design System and Bootstrap conventions outlined in `DesignSystem.md`.

- **Column Header:** 
  - Title: "Blocked"
  - Badge/Counter: Uses the `$danger` (#DC2626) or `$warning` (#F59E42) background to draw attention to blocked items.
- **Task Cards in Blocked Column:**
  - Standard task card layout is maintained.
  - An optional **Block Reason** badge or text snippet is displayed on the card if a reason was provided.
  - Visual Indicator: A subtle border or left-border highlight using the `$danger` or `$warning` color to quickly identify the blocked state.

## Interactions & UI Components

### 1. Moving to "Blocked" (Drag & Drop)
When a user drags a task into the "Blocked" column:
- A modal dialog or inline popover may appear (depending on technical implementation preference) asking for an **Optional Block Reason**.
- The task is immediately moved, ensuring no friction if the user skips the reason.

### 2. Status Change via Dropdown / Edit Modal
When editing a task's status via a dropdown or details modal:
- "Blocked" appears as an option in the Status dropdown.
- When "Blocked" is selected, a new text input field conditionally appears:
  - **Label:** "Reason for block (Optional)"
  - **Type:** Textarea or Text Input
  - **Placeholder:** "e.g., Waiting on external API access..."
  - **Styling:** Follows standard form input styling (`border-color: $border-color`, rounded corners).

### 3. Displaying the Block Reason
- **On the Board (Card View):** If a reason is provided, display it truncated (e.g., 1-2 lines max) with an icon (e.g., a lock or alert triangle) next to it. Font size should be slightly smaller (e.g., `text-sm`) and use `$secondary` (#64748B) or `$danger` for the text/icon to ensure it stands out but doesn't overwhelm the card title.
- **In Task Details View:** A dedicated section or banner at the top of the task details displays the block reason prominently.

## Accessibility (A11y)
- **Color Contrast:** Ensure the text color used for the block reason and column headers meets WCAG AA/AAA contrast ratios against the background. Do not rely solely on color to indicate the blocked state; use text labels and icons.
- **Keyboard Navigation:** The new column and any associated reason input fields must be fully navigable via keyboard (Tab/Shift+Tab). Status dropdowns must be accessible.
- **Screen Readers:** 
  - ARIA labels should announce the column as "Blocked". 
  - When a task is moved to blocked, use an `aria-live` region to announce the status change.
  - The optional reason input must have a clear `for` attribute linking the label to the input.

## Future Considerations
- Filtering: Add the ability to quickly filter the board by "Blocked" status, even if looking at a list view.
- Notifications: Consider notifying the task owner or assignee when a task is moved to "Blocked".
