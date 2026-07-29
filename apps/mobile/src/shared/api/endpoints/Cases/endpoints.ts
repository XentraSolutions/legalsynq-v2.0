import { apiClient } from '@/shared/api/client';
import type { PagedResult } from '@/shared/types/api';

import type {
  AddCaseNoteRequest,
  Case,
  CaseQueryParams,
  DashboardPiechart,
  DashboardLawFirmCaseReportRow,
  DashboardMedicalProviderReportRow,
  DashboardStatRequest,
  DashboardStatResponse,
  DashboardTaskSummary,
  DashboardTotalCaseReportRow,
  DashboardTotalLienReportRow,
  Note,
  ReportFilterRequest,
} from './types';

const CASES_BASE_PATH = '/liens/api/liens/cases';

export const caseKeys = {
  all: ['cases'] as const,
  list: (params: CaseQueryParams) => [...caseKeys.all, 'list', params] as const,
  detail: (id: string) => [...caseKeys.all, 'detail', id] as const,
};

function asRecord(value: unknown): Record<string, unknown> | undefined {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : undefined;
}

function unwrapEnvelope(value: unknown): unknown {
  let current = value;

  for (let index = 0; index < 2; index += 1) {
    const record = asRecord(current);
    if (!record || !('data' in record)) {
      break;
    }

    const data = record.data;
    if (!Array.isArray(data) && !asRecord(data)) {
      break;
    }

    current = data;
  }

  return current;
}

function readNumber(record: Record<string, unknown>, keys: string[], fallback: number): number {
  for (const key of keys) {
    const value = record[key];

    if (typeof value === 'number' && Number.isFinite(value)) {
      return value;
    }

    if (typeof value === 'string') {
      const parsed = Number(value.replace(/[^0-9.-]/g, ''));
      if (Number.isFinite(parsed)) {
        return parsed;
      }
    }
  }

  return fallback;
}

function readArray<TItem>(record: Record<string, unknown>, keys: string[]): TItem[] {
  for (const key of keys) {
    const value = record[key];
    if (Array.isArray(value)) {
      return value as TItem[];
    }
  }

  return [];
}

function normalizePagedResult<TItem>(payload: unknown): PagedResult<TItem> {
  const unwrapped = unwrapEnvelope(payload);

  if (Array.isArray(unwrapped)) {
    return {
      items: unwrapped as TItem[],
      page: 1,
      pageSize: unwrapped.length,
      totalCount: unwrapped.length,
      totalPages: 1,
    };
  }

  const record = asRecord(unwrapped);
  if (!record) {
    return {
      items: [],
      page: 1,
      pageSize: 0,
      totalCount: 0,
      totalPages: 0,
    };
  }

  const items = readArray<TItem>(record, ['items', 'data', 'results', 'records', 'rows']);
  const page = readNumber(record, ['page', 'currentPage'], 1);
  const pageSize = readNumber(record, ['pageSize', 'limit', 'perPage'], items.length);
  const totalCount = readNumber(
    record,
    ['totalCount', 'total', 'count', 'recordsTotal'],
    items.length
  );
  const totalPages = readNumber(
    record,
    ['totalPages', 'pageCount'],
    pageSize > 0 ? Math.max(1, Math.ceil(totalCount / pageSize)) : 0
  );

  return { items, page, pageSize, totalCount, totalPages };
}

export const CasesApi = {
  async listCases(params: CaseQueryParams): Promise<PagedResult<Case>> {
    const response = await apiClient.get<PagedResult<Case>>(CASES_BASE_PATH, { params });
    return response.data;
  },

  async getCase(id: string): Promise<Case> {
    const response = await apiClient.get<Case>(`${CASES_BASE_PATH}/${id}`);
    return response.data;
  },

  async updateCaseStatus(id: string, status: string): Promise<Case> {
    const response = await apiClient.patch<Case>(`${CASES_BASE_PATH}/${id}/status`, { status });
    return response.data;
  },

  async getCaseNotes(caseId: string): Promise<Note[]> {
    const response = await apiClient.get<Note[]>(`${CASES_BASE_PATH}/${caseId}/notes`);
    return response.data;
  },

  async addCaseNote(caseId: string, body: AddCaseNoteRequest): Promise<Note> {
    const response = await apiClient.post<Note>(`${CASES_BASE_PATH}/${caseId}/notes`, body);
    return response.data;
  },

  async getDashboardPiechart(): Promise<DashboardPiechart> {
    const response = await apiClient.get<{ data: DashboardPiechart }>(
      `${CASES_BASE_PATH}/dashboard/piechart`
    );
    return response.data.data;
  },

  async getDashboardTaskSummary(): Promise<DashboardTaskSummary> {
    const response = await apiClient.get<DashboardTaskSummary>(
      `${CASES_BASE_PATH}/dashboard/task-summary`
    );
    return response.data;
  },

  async getDashboardTotalLienReport(): Promise<unknown[]> {
    const response = await apiClient.get<unknown[]>(
      `${CASES_BASE_PATH}/dashboard/total-lien-report-export`
    );
    return response.data;
  },

  async getDashboardTotalLienReportV3(
    body: ReportFilterRequest
  ): Promise<PagedResult<DashboardTotalLienReportRow>> {
    const response = await apiClient.post<unknown>(
      `${CASES_BASE_PATH}/dashboard/total-lien-report-export/v3`,
      body
    );
    return normalizePagedResult<DashboardTotalLienReportRow>(response.data);
  },

  async getDashboardTotalCaseReport(): Promise<unknown[]> {
    const response = await apiClient.get<unknown[]>(
      `${CASES_BASE_PATH}/dashboard/total-case-report-export`
    );
    return response.data;
  },

  async getDashboardTotalCaseReportV3(
    body: ReportFilterRequest
  ): Promise<PagedResult<DashboardTotalCaseReportRow>> {
    const response = await apiClient.post<unknown>(
      `${CASES_BASE_PATH}/dashboard/total-case-report-export/v3`,
      body
    );
    return normalizePagedResult<DashboardTotalCaseReportRow>(response.data);
  },

  async getDashboardLawFirmCaseReport(): Promise<DashboardLawFirmCaseReportRow[]> {
    const response = await apiClient.get<DashboardLawFirmCaseReportRow[]>(
      `${CASES_BASE_PATH}/dashboard/lawfirm-case-report-export`
    );
    return response.data;
  },

  async getDashboardLawFirmCaseReportV3(
    body: ReportFilterRequest
  ): Promise<PagedResult<DashboardLawFirmCaseReportRow>> {
    const response = await apiClient.post<unknown>(
      `${CASES_BASE_PATH}/dashboard/lawfirm-case-report-export/v3`,
      body
    );
    return normalizePagedResult<DashboardLawFirmCaseReportRow>(response.data);
  },

  async getDashboardMedicalProviderReport(): Promise<DashboardMedicalProviderReportRow[]> {
    const response = await apiClient.get<DashboardMedicalProviderReportRow[]>(
      `${CASES_BASE_PATH}/dashboard/medical-provider-report-export`
    );
    return response.data;
  },

  async getDashboardMedicalProviderReportV3(
    body: ReportFilterRequest
  ): Promise<PagedResult<DashboardMedicalProviderReportRow>> {
    const response = await apiClient.post<unknown>(
      `${CASES_BASE_PATH}/dashboard/medical-provider-report-export/v3`,
      body
    );
    return normalizePagedResult<DashboardMedicalProviderReportRow>(response.data);
  },

  async getDashboardDeployed(body: DashboardStatRequest): Promise<DashboardStatResponse> {
    const response = await apiClient.post<unknown>(`${CASES_BASE_PATH}/dashboard/deployed`, body);
    return (asRecord(response.data)?.data ?? response.data ?? {}) as DashboardStatResponse;
  },

  async getDashboardCashReceived(body: DashboardStatRequest): Promise<DashboardStatResponse> {
    const response = await apiClient.post<unknown>(
      `${CASES_BASE_PATH}/dashboard/cash-received`,
      body
    );
    return (asRecord(response.data)?.data ?? response.data ?? {}) as DashboardStatResponse;
  },
};
