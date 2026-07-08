export interface CreateBatchRequestDto {
  label: string;
  template: string;
  caseId: string;
  file: string;
  date: string;
  rows: number | string;
  dataContext: string;
}

export interface UpdateBatchRequestDto {
  id: string;
  label: string;
  template: string;
  caseId: string;
  file: string;
  date: string;
  rows: number | string;
  dataContext: string;
}

export interface ProcessBatchDto {
  batchUploadId: string;
  templateId: string;
  caseId: string;
}

export interface PaginationMeta {
  page: number;
  pageSize?: number;
  totalCount?: number;
  totalPages?: number;
  limit?: number;
  keyword?: string;
  template?: string;
  status?: string;
}

export interface PaginatedResultDto<T> {
  items?: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}
