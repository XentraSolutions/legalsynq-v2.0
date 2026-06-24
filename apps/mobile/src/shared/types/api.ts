export interface PagedResult<TItem> {
  items: TItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ApiErrorPayload {
  code: string;
  message: string;
  statusCode?: number;
  correlationId?: string;
  details?: unknown;
}

export class ApiError extends Error implements ApiErrorPayload {
  code: string;
  statusCode?: number;
  correlationId?: string;
  details?: unknown;

  constructor(payload: ApiErrorPayload) {
    super(payload.message);
    this.name = 'ApiError';
    this.code = payload.code;
    this.statusCode = payload.statusCode;
    this.correlationId = payload.correlationId;
    this.details = payload.details;
  }
}

export interface PaginationParams {
  page?: number;
  pageSize?: number;
}
