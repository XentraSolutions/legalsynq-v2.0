# LS-LIENS-CASE-005 — Case Task Manager Tab UI

## Objective
Implement the Task Manager tab within the Case Detail page (Synq Liens → Cases → Case Detail) with both Kanban and List views.

## Scope
- Only the Task Manager tab body content in `case-detail-client.tsx`
- No changes to top nav, side nav, routing, fonts, colors, APIs, other tabs

## Layout Decision
**Full-width layout chosen** (no LayoutSplit / communications rail). Rationale: the Kanban board requires full horizontal space for four columns. Constraining it within a LayoutSplit left panel would make columns too narrow for practical use. This is documented as an intentional exception from the other tabs.

## Implementation

### View Switcher
- Toggle between Kanban and List views via pill-style switcher in the header
- Kanban is the default view
- Both views share the same task data source

### Header Bar
- Task Manager title with task count badge
- Kanban/List view switcher (segmented control)
- "Add Task" button

### Add Task Form
- Inline expandable form (toggles via Add Task button)
- Fields: Task Name, Priority (dropdown), Due Date, Assignee
- Create Task button is disabled with "Not yet connected to API" label
- Cancel button closes the form

### Kanban View
Four columns:
| Column | Color Scheme |
|--------|-------------|
| Upcoming | Gray |
| In Progress | Blue |
| In Review | Amber |
| Completed | Green |

Each column has:
- Title with count badge
- Add (+) button in header
- Empty state when no tasks
- Task cards with: name, description (2-line clamp), priority badge, due date, assignee avatar

### List View
Table with columns:
- Task Name (with description subtitle)
- Status (colored badge)
- Priority (colored badge)
- Assignee (with avatar icon)
- Due Date
- Updated At
- Actions (View, Edit buttons)

Footer with total task count.

### Task Card Design (Kanban)
- White card with border and subtle shadow
- Hover shadow transition
- Task name (semibold), description (gray, line-clamped)
- Bottom section: priority badge + due date
- Assignee row with avatar

### Files Changed
| File | Change |
|------|--------|
| `apps/web/src/app/(platform)/lien/cases/[id]/case-detail-client.tsx` | Added `TaskManagerTab` function, task types/interfaces, TEMP data, status/priority color maps, replaced `EmptyTab` reference |

### Design Patterns Used
- Same form input styling as other tabs
- Remix icons throughout (ri-task-line, ri-layout-column-line, ri-list-unordered, ri-add-line, ri-user-line, ri-calendar-line, ri-eye-line, ri-pencil-line)
- Same table header styling: `text-[11px]` uppercase tracking-wide
- Consistent hover transitions
- TEMP fallback banner (amber info bar)

### Fallback Data
All data is TEMP visual fallback, clearly labeled:
- `TEMP_TASKS` — 7 tasks across all 4 statuses (2 Upcoming, 2 In Progress, 1 In Review, 2 Completed)
- Task content is case-relevant (medical records, insurance follow-up, lien purchases, demand letters)
- Priority distribution: 3 High, 3 Medium, 1 Low

### Validation
- TypeScript: 0 errors, 0 warnings
- No changes to other tabs (Details, Liens, Documents, Servicing, Notes)
- No fake persistence or workflow success
- Create Task button disabled with explicit "Not yet connected to API" note

### Remaining Gaps
- Create Task not wired to backend API
- View/Edit task actions are UI-only
- Kanban drag-and-drop not implemented (cards are static)
- All task data uses TEMP fallback
- No task filtering/sorting
