import { apiClient } from '@/shared/api/client';
import type { PagedResult } from '@/shared/types/api';

import type {
  AddCaseNoteRequest,
  Case,
  CaseQueryParams,
  DashboardPiechart,
  DashboardLawFirmCaseReportRow,
  DashboardMedicalProviderReportRow,
  DashboardTaskSummary,
  Note,
  ReportFilterRequest,
} from './types';

const CASES_BASE_PATH = '/liens/api/liens/cases';

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

  async getDashboardTaskSummary(): Promise<DashboardTaskSummary> {
    const response = await apiClient.get<DashboardTaskSummary>(`${CASES_BASE_PATH}/dashboard/task-summary`);
    return response.data;
  },

  async getDashboardTotalLienReport(): Promise<unknown[]> {
    const response = await apiClient.get<unknown[]>(`${CASES_BASE_PATH}/dashboard/total-lien-report-export`);
    return response.data;
  },

  async getDashboardTotalLienReportV3(body: ReportFilterRequest): Promise<PagedResult<unknown>> {
    const response = await apiClient.post<PagedResult<unknown>>(`${CASES_BASE_PATH}/dashboard/total-lien-report-export/v3`, body);
    return response.data;
  },

  async getDashboardTotalCaseReport(): Promise<unknown[]> {
    const response = await apiClient.get<unknown[]>(`${CASES_BASE_PATH}/dashboard/total-case-report-export`);
    return response.data;
  },

  async getDashboardTotalCaseReportV3(body: ReportFilterRequest): Promise<PagedResult<unknown>> {
    const response = await apiClient.post<PagedResult<unknown>>(`${CASES_BASE_PATH}/dashboard/total-case-report-export/v3`, body);
    return response.data;
  },

  async getDashboardLawFirmCaseReport(): Promise<DashboardLawFirmCaseReportRow[]> {
    const response = await apiClient.get<DashboardLawFirmCaseReportRow[]>(`${CASES_BASE_PATH}/dashboard/lawfirm-case-report-export`);
    return response.data;
  },

  async getDashboardLawFirmCaseReportV3(body: ReportFilterRequest): Promise<PagedResult<DashboardLawFirmCaseReportRow>> {
    const response = await apiClient.post<PagedResult<DashboardLawFirmCaseReportRow>>(`${CASES_BASE_PATH}/dashboard/lawfirm-case-report-export/v3`, body);
    return response.data;
  },

  async getDashboardMedicalProviderReport(): Promise<DashboardMedicalProviderReportRow[]> {
    const response = await apiClient.get<DashboardMedicalProviderReportRow[]>(`${CASES_BASE_PATH}/dashboard/medical-provider-report-export`);
    return response.data;
  },

  async getDashboardMedicalProviderReportV3(body: ReportFilterRequest): Promise<PagedResult<DashboardMedicalProviderReportRow>> {
    const response = await apiClient.post<PagedResult<DashboardMedicalProviderReportRow>>(`${CASES_BASE_PATH}/dashboard/medical-provider-report-export/v3`, body);
    return response.data;
  },
};
