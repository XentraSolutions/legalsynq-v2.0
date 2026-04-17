/**
 * LS-FLOW-E11.6 — type contracts for the My Work UI.
 *
 * Mirrors the Flow.Api DTOs:
 *   - MyTaskDto                       (Application/DTOs/MyTaskDtos.cs)
 *   - PagedResponse<T>                (Application/DTOs/TaskDtos.cs)
 *   - WorkflowTaskTransitionResult    (Application/Interfaces/IWorkflowTaskLifecycleService.cs)
 *   - WorkflowTaskCompletionResult    (Application/Interfaces/IWorkflowTaskCompletionService.cs)
 *
 * Field shapes are kept narrow on purpose so the UI does not couple to
 * engine internals. Anything missing here is intentionally not surfaced.
 */

export type WorkflowTaskStatus = 'Open' | 'InProgress' | 'Completed' | 'Cancelled';

export type WorkflowTaskPriority = 'Low' | 'Normal' | 'High' | 'Urgent';

export interface MyTask {
  taskId: string;
  title: string;
  description?: string | null;
  status: WorkflowTaskStatus;
  priority: WorkflowTaskPriority;
  stepKey: string;

  assignedUserId?: string | null;

  createdAt: string;
  updatedAt?: string | null;
  startedAt?: string | null;
  completedAt?: string | null;
  cancelledAt?: string | null;

  workflowInstanceId: string;
  workflowName?: string | null;
  productKey?: string | null;
}

export interface PagedTasks {
  items: MyTask[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** Response from start / cancel. */
export interface TaskTransitionResult {
  taskId: string;
  previousStatus: WorkflowTaskStatus;
  newStatus: WorkflowTaskStatus;
  transitionedAtUtc: string;
}

/**
 * Response from complete (E11.7). Strictly additive over
 * TaskTransitionResult — it carries every legacy field plus the
 * resulting workflow snapshot so the UI can refresh the row in one
 * round-trip.
 */
export interface TaskCompletionResult extends TaskTransitionResult {
  workflowInstanceId: string;
  fromStepKey: string;
  toStepKey: string;
  workflowStatus: string;
  workflowAdvanced: boolean;
}

export type StatusFilter = 'all' | WorkflowTaskStatus;

/** Status values the user can pick in the filter chip. */
export const STATUS_FILTER_OPTIONS: { value: StatusFilter; label: string }[] = [
  { value: 'all',        label: 'All' },
  { value: 'Open',       label: 'Open' },
  { value: 'InProgress', label: 'In Progress' },
  { value: 'Completed',  label: 'Completed' },
  { value: 'Cancelled',  label: 'Cancelled' },
];
