import type { PagedResult } from '@/shared/types/api';

export interface TaskQueryParams {
  search?: string;
  status?: string;
  priority?: string;
  assignedUserId?: string;
  caseId?: string;
  lienId?: string;
  workflowStageId?: string;
  assignmentScope?: string;
  page?: number;
  pageSize?: number;
}

export interface LienTask {
  id: string;
  tenantId: string;
  title: string;
  description?: string | null;
  status: string;
  priority: string;
  assignedUserId?: string | null;
  caseId?: string | null;
  workflowStageId?: string | null;
  dueDate?: string | null;
  completedAt?: string | null;
  linkedLiens: Array<{ taskId: string; lienId: string; createdAtUtc: string }>;
  createdByUserId?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  sourceType: string;
  isSystemGenerated: boolean;
  workflowInstanceId?: string | null;
  workflowStepKey?: string | null;
}

export interface CreateTaskRequest {
  title: string;
  description?: string;
  priority?: string;
  assignedUserId?: string;
  caseId?: string;
  lienIds?: string[];
  workflowStageId?: string;
  dueDate?: string;
}

export type UpdateTaskRequest = Omit<CreateTaskRequest, 'assignedUserId'>;

export interface TaskNote {
  id: string;
  taskId: string;
  content: string;
  createdByUserId: string;
  createdByName: string;
  isEdited: boolean;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
}

export interface TaskHistoryEntry {
  id?: string;
  taskId?: string;
  action?: string;
  fieldName?: string | null;
  oldValue?: string | null;
  newValue?: string | null;
  changedByUserId?: string | null;
  changedAtUtc?: string;
}

export type TaskListResult = Omit<PagedResult<LienTask>, 'totalPages'> & {
  totalPages?: number;
};
