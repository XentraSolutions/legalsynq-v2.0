import { apiClient } from '@/shared/api/client';
import type { PagedResult } from '@/shared/types/api';

import type { AddCaseNoteRequest, Case, CaseQueryParams, DashboardPiechart, Note } from './types';

const CASES_BASE_PATH = '/api/liens/cases';

export const caseKeys = {
  all: ['cases'] as const,
  list: (params: CaseQueryParams) => [...caseKeys.all, 'list', params] as const,
  detail: (id: string) => [...caseKeys.all, 'detail', id] as const,
};

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
    const response = await apiClient.get<{ data: DashboardPiechart }>(`${CASES_BASE_PATH}/dashboard/piechart`);
    return response.data.data;
  },
};
