import type { PagedResult } from '@/shared/types/api';

export interface ServicingQueryParams {
  search?: string;
  status?: string;
  priority?: string;
  assignedTo?: string;
  caseId?: string;
  lienId?: string;
  page?: number;
  pageSize?: number;
}

export interface ServicingItem {
  id: string;
  taskNumber: string;
  taskType: string;
  description: string;
  status: string;
  priority: string;
  assignedTo: string;
  assignedToUserId?: string | null;
  caseId?: string | null;
  lienId?: string | null;
  dueDate?: string | null;
  notes?: string | null;
  resolution?: string | null;
  startedAtUtc?: string | null;
  completedAtUtc?: string | null;
  escalatedAtUtc?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateServicingItemRequest {
  taskNumber: string;
  taskType: string;
  description: string;
  assignedTo: string;
  assignedToUserId?: string;
  priority?: string;
  caseId?: string;
  lienId?: string;
  dueDate?: string;
  notes?: string;
}

export interface UpdateServicingItemRequest
  extends Omit<CreateServicingItemRequest, 'taskNumber'> {
  status?: string;
  resolution?: string;
}

export type ServicingListResult = Omit<PagedResult<ServicingItem>, 'totalPages'> & {
  totalPages?: number;
};
