import { GenericPaginationData } from "../lookup/lookup.types";

export interface ServicingPaginationData extends GenericPaginationData {
  status: string | undefined;
  priority: string | undefined;
  pageSize: number;
}

export interface ServicingItemResponseDto {
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

export interface CreateServicingItemRequestDto {
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

export interface UpdateServicingItemRequestDto {
  taskType: string;
  description: string;
  assignedTo: string;
  assignedToUserId?: string;
  priority?: string;
  status?: string;
  caseId?: string;
  lienId?: string;
  dueDate?: string;
  notes?: string;
  resolution?: string;
}
export interface UpdateServicingDetailsRequestDto {
  caseId: string;
  caseStatusId: string;
  isUCCFiled: string;
  switchedDate: string;
  lawFirmId?: string;
  attorney?: string;
  caseManager?: string;
}

export interface UpdateServicingStatusRequestDto {
  status: string;
  resolution?: string;
}

export interface PaginatedResultDto<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ServicingQuery {
  search?: string;
  status?: string;
  priority?: string;
  assignedTo?: string;
  caseId?: string;
  lienId?: string;
  page?: number;
  pageSize?: number;
}
//this is the type for the MVP version
// export interface ServicingListItem {
//   id: string;
//   taskNumber: string;
//   taskType: string;
//   description: string;
//   status: string;
//   priority: string;
//   assignedTo: string;
//   caseId: string;
//   lienId: string;
//   dueDate: string;
//   notes: string;
//   resolution: string;
//   startedAt: string;
//   completedAt: string;
//   escalatedAt: string;
//   createdAt: string;
//   updatedAt: string;
// }

export interface ServicingDetail extends ServicingListItem {
  assignedToUserId: string;
  taskNumber: string;
  taskType: string;
  description: string;
  status: string;
  priority: string;
  assignedTo: string;
  dueDate?: string | null;
  notes?: string | null;
  resolution?: string | null;
  startedAt?: string | null;
  completedAt?: string | null;
  escalatedAt?: string | null;
  createdAt: string;
  updatedAt: string;
  lienId?: string | null;
}

export interface PaginationMeta {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ServicingListItem {
  id?: string;
  caseId: string;
  caseCode: string;
  name: string;
  lawfirm: string;
  currentStatus: string;
  settlementStatus: string;
  settlementDate: string;
  settlementAmount: number;
  billingAmount: number;
  purchaseAmount: number;
}

export interface ServicingListItemResponseDto {
  caseId: string;
  caseCode: string;
  plaintiffName: string;
  lawfirm: string;
  status: string;
  settlementStatus: string;
  settlementDate: string;
  settlementAmount: number;
  billingAmount: number;
  purchaseAmount: number;
}

export interface ExportResponse {
  data: Array<{ base64: string; export_format: string; filename: string }>;
}
