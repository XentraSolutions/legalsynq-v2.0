import { apiClient } from '@/shared/api/client';

import type { ReportConfig, RunReportRequest, RunReportResult, SavedReport } from './types';

const BASE_PATH = '/liens/api/liens/reports/diy';

export const ReportsApi = {
  async listSaved(): Promise<SavedReport[]> {
    const response = await apiClient.get<SavedReport[]>(`${BASE_PATH}/saved`);
    return response.data;
  },

  async getSaved(id: string): Promise<SavedReport> {
    const response = await apiClient.get<SavedReport>(`${BASE_PATH}/saved/${id}`);
    return response.data;
  },

  async save(name: string, config: ReportConfig): Promise<SavedReport> {
    const response = await apiClient.post<SavedReport>(`${BASE_PATH}/save`, { name, config });
    return response.data;
  },

  async deleteSaved(id: string): Promise<void> {
    await apiClient.delete(`${BASE_PATH}/saved/${id}`);
  },

  async run(body: RunReportRequest): Promise<RunReportResult> {
    const response = await apiClient.post<RunReportResult>(`${BASE_PATH}/run`, body);
    return response.data;
  },

  async exportCsv(body: RunReportRequest): Promise<string> {
    const response = await apiClient.post<{ data: string }>(`${BASE_PATH}/export`, body);
    return response.data.data;
  },
};
