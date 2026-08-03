import { apiClient } from '@/shared/api/client';
import type { PagedResult } from '@/shared/types/api';

import type {
  AddCaseNoteRequest,
  Case,
  CaseDetailResponse,
  CaseDetailsUpdateRequest,
  CaseExportFile,
  CaseExportFilter,
  CaseQueryParams,
  CreateCaseRequest,
  DashboardPiechart,
  DashboardLawFirmCaseReportRow,
  DashboardMedicalProviderReportRow,
  DashboardStatRequest,
  DashboardStatResponse,
  DashboardTaskSummary,
  DashboardTotalCaseReportRow,
  DashboardTotalLienReportRow,
  Note,
  PayoffQuote,
  PersonalCaseUpdateRequest,
  PrimaryCaseUpdateRequest,
  CaseUpdate,
  ReportFilterRequest,
} from './types';

const CASES_BASE_PATH = '/liens/api/liens/cases';
const MANAGE_CASES_BASE_PATH = '/api/lien/api/liens/cases';

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

function normalizeArray<TItem>(payload: unknown): TItem[] {
  const unwrapped = unwrapEnvelope(payload);
  if (Array.isArray(unwrapped)) return unwrapped as TItem[];
  const record = asRecord(unwrapped);
  return record ? readArray<TItem>(record, ['items', 'data', 'results', 'records', 'rows']) : [];
}

function normalizeNote(raw: unknown): Note {
  const note = asRecord(raw) ?? {};
  return {
    id: String(note.id ?? ''),
    caseId: String(note.caseId ?? ''),
    authorId: String(note.authorId ?? note.createdByUserId ?? ''),
    authorName: String(note.authorName ?? note.createdByName ?? 'Unknown user'),
    content: String(note.content ?? note.note ?? ''),
    createdAt: String(note.createdAt ?? note.createdAtUtc ?? new Date(0).toISOString()),
  };
}

export function normalizePagedResult<TItem>(payload: unknown): PagedResult<TItem> {
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

  async getCase(id: string): Promise<CaseDetailResponse> {
    const response = await apiClient.get<CaseDetailResponse>(`${CASES_BASE_PATH}/${id}`);
    return response.data;
  },

  async getPayoffQuote(caseId: string): Promise<PayoffQuote> {
    const response = await apiClient.get<unknown>(
      `${MANAGE_CASES_BASE_PATH}/payoff-quote/${caseId}`
    );
    const payload = asRecord(response.data);
    const url = payload?.url;
    if (typeof url !== 'string' || !url.trim()) {
      throw new Error('A payoff quote is not available for this case.');
    }
    return { url };
  },

  async mergeCase(caseIdA: string, caseIdB: string): Promise<void> {
    await apiClient.post(`${MANAGE_CASES_BASE_PATH}/mergecase`, { caseIdA, caseIdB });
  },

  async deleteCase(caseId: string): Promise<void> {
    await apiClient.delete(`${MANAGE_CASES_BASE_PATH}/delete/${caseId}`);
  },

  async updatePersonalInfo(body: PersonalCaseUpdateRequest): Promise<void> {
    await apiClient.patch(`${CASES_BASE_PATH}/personal-update`, body);
  },

  async updatePrimaryInfo(body: PrimaryCaseUpdateRequest): Promise<void> {
    await apiClient.patch(`${CASES_BASE_PATH}/primary-update`, body);
  },

  async updateCaseDetails(body: CaseDetailsUpdateRequest): Promise<void> {
    await apiClient.patch(`${CASES_BASE_PATH}/details-update`, body);
  },

  async getCaseUpdates(caseId: string): Promise<CaseUpdate[]> {
    const response = await apiClient.post<unknown>(`${CASES_BASE_PATH}/case-updates/v3`, {
      caseId,
      page: 1,
      limit: 10,
    });

    return normalizeArray<CaseUpdate>(response.data).map((update) => ({
      ...update,
      createdAt:
        update.createdAt ?? (typeof update.created === 'string' ? update.created : undefined),
      updatedAt:
        update.updatedAt ?? (typeof update.updated === 'string' ? update.updated : undefined),
    }));
  },

  async getLienUpdates(caseId: string): Promise<CaseUpdate[]> {
    const response = await apiClient.post<unknown>(`${CASES_BASE_PATH}/liens-updates/v3`, {
      caseId,
      page: 1,
      limit: 100,
    });

    return normalizeArray<CaseUpdate>(response.data).map((update) => ({
      ...update,
      title: update.title ?? update.action ?? 'Lien Update',
      updatedAt:
        update.updatedAt ??
        (typeof update.timestamp === 'string' ? update.timestamp : undefined),
    }));
  },

  async createCase(body: CreateCaseRequest): Promise<CaseDetailResponse> {
    const response = await apiClient.post<CaseDetailResponse>(CASES_BASE_PATH, body);
    return response.data;
  },

  async updateCaseStatus(id: string, status: string): Promise<Case> {
    const response = await apiClient.patch<Case>(`${CASES_BASE_PATH}/${id}/status`, { status });
    return response.data;
  },

  async getCaseNotes(caseId: string): Promise<Note[]> {
    const response = await apiClient.get<unknown>(`${CASES_BASE_PATH}/${caseId}/notes`);
    return normalizeArray<unknown>(response.data).map(normalizeNote);
  },

  async addCaseNote(caseId: string, body: AddCaseNoteRequest): Promise<Note> {
    const response = await apiClient.post<unknown>(`${CASES_BASE_PATH}/${caseId}/notes`, body);
    return normalizeNote(response.data);
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

  async getDashboardTotalCaseReport(): Promise<DashboardTotalCaseReportRow[]> {
    const response = await apiClient.get<unknown>(
      `${CASES_BASE_PATH}/dashboard/total-case-report-export`
    );
    return normalizeArray<DashboardTotalCaseReportRow>(response.data);
  },

  async exportCases(body: CaseExportFilter): Promise<CaseExportFile> {
    const response = await apiClient.post<unknown>(`${CASES_BASE_PATH}/generate-csv`, body);
    const files = normalizeArray<CaseExportFile>(response.data);
    if (!files[0]) {
      throw new Error('The case export did not contain a file.');
    }
    return files[0];
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
