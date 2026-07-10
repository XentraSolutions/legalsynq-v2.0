import { apiClient } from "@/lib/api-client";
import type {
  UpdateBatchRequestDto,
  ProcessBatchDto,
  PaginationMeta,
  CreateBatchRequestDto,
  BatchListItem,
} from "./batch.types";
import { ApiResponse } from "../liens/lien-report.types";
import { GenericPaginatedResult } from "../lookup/lookup.types";
import { CaseListItem } from "../cases";

const BASE = "/lien/Batch";

// Report endpoints expect MM/DD/YYYY; the date pickers hand us YYYY-MM-DD.
function toApiDate(value?: string): string | undefined {
  if (!value) return undefined;
  const [y, m, d] = value.split("-");
  if (!y || !m || !d) return value;
  return `${m}/${d}/${y}`;
}

function withApiDates<T extends { startDate?: string; endDate?: string }>(
  request: T,
): T {
  return {
    ...request,
    startDate: toApiDate(request.startDate),
    endDate: toApiDate(request.endDate),
  };
}

export const batchApi = {
  list(request: PaginationMeta) {
    return apiClient.post<GenericPaginatedResult<BatchListItem>>(
      `${BASE}/list`,
      request,
    );
  },
  getById(id: string) {
    return apiClient.get<ApiResponse>(`${BASE}/${id}`);
  },

  create(request: CreateBatchRequestDto) {
    return apiClient.post<CreateBatchRequestDto>(`${BASE}/create`, request);
  },

  upload(request: FormData) {
    return apiClient.postForm<FormData>(`${BASE}/Upload`, request);
  },

  details(id: string) {
    return apiClient.get<ApiResponse>(`${BASE}/details/${id}`);
  },

  process(request: ProcessBatchDto) {
    return apiClient.post<ProcessBatchDto>(`${BASE}/process`, request);
  },

  update(request: UpdateBatchRequestDto) {
    return apiClient.post<UpdateBatchRequestDto>(`${BASE}`, request);
  },

  dataContext(request: { id: string; page: number; limit: number }) {
    return apiClient.post<string>(`${BASE}/data-context`, request);
  },

  delete(id: string) {
    return apiClient.delete<ApiResponse>(`${BASE}/delete/${id}`);
  },

  deleteDetails(id: string) {
    return apiClient.delete<ApiResponse>(`${BASE}/details/delete/${id}`);
  },

  download(id: string) {
    return apiClient.get<ApiResponse>(`${BASE}/download-template/${id}`);
  },
};
