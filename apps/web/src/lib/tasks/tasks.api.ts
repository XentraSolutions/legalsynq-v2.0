/**
 * LS-FLOW-E11.6 — thin API adapter for the My Work UI.
 *
 * Talks to the Flow service via the BFF proxy at /api/flow/* which
 * forwards to GATEWAY/flow/* (see app/api/flow/[...path]/route.ts and
 * Gateway.Api `flow-protected` route). The browser never talks to Flow
 * directly; the BFF rewrites the platform_session cookie into a Bearer
 * header.
 *
 * Surface mirrors the four endpoints E11.5 + E11.7 own:
 *   GET    /api/v1/tasks/me
 *   POST   /api/v1/workflow-tasks/{id}/start
 *   POST   /api/v1/workflow-tasks/{id}/complete
 *   POST   /api/v1/workflow-tasks/{id}/cancel
 *
 * Errors bubble as ApiError (see lib/api-client.ts) so callers can
 * branch on .isNotFound / .isConflict / status === 422 without parsing
 * raw response bodies.
 */
import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types';
import type {
  MyTask,
  PagedTasks,
  TaskCompletionResult,
  TaskTransitionResult,
  WorkflowTaskStatus,
} from './tasks.types';

export interface ListMyTasksParams {
  /** Repeat-able status filter. Empty / undefined returns all. */
  status?: WorkflowTaskStatus[];
  page?: number;
  pageSize?: number;
}

const FLOW_PREFIX = '/flow/api/v1';

function buildQuery(params: ListMyTasksParams): string {
  const qs = new URLSearchParams();
  if (params.status && params.status.length > 0) {
    for (const s of params.status) qs.append('status', s);
  }
  if (params.page)     qs.set('page', String(params.page));
  if (params.pageSize) qs.set('pageSize', String(params.pageSize));
  const s = qs.toString();
  return s ? `?${s}` : '';
}

export const tasksApi = {
  listMine(params: ListMyTasksParams = {}): Promise<ApiResponse<PagedTasks>> {
    return apiClient.get<PagedTasks>(`${FLOW_PREFIX}/tasks/me${buildQuery(params)}`);
  },
  start(taskId: string): Promise<ApiResponse<TaskTransitionResult>> {
    return apiClient.post<TaskTransitionResult>(
      `${FLOW_PREFIX}/workflow-tasks/${encodeURIComponent(taskId)}/start`,
      {},
    );
  },
  complete(taskId: string): Promise<ApiResponse<TaskCompletionResult>> {
    return apiClient.post<TaskCompletionResult>(
      `${FLOW_PREFIX}/workflow-tasks/${encodeURIComponent(taskId)}/complete`,
      {},
    );
  },
  cancel(taskId: string): Promise<ApiResponse<TaskTransitionResult>> {
    return apiClient.post<TaskTransitionResult>(
      `${FLOW_PREFIX}/workflow-tasks/${encodeURIComponent(taskId)}/cancel`,
      {},
    );
  },
};

export type { MyTask, PagedTasks, TaskTransitionResult, TaskCompletionResult };
