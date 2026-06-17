import { apiClient } from "@/lib/api-client";
import type {
  CaseNoteResponse,
  CreateCaseNoteRequest,
  UpdateCaseNoteRequest,
} from "./lien-case-notes.types";
import {
  ApiResponse,
  CreateReports,
  ExportReportRequest,
  ReportsResponse,
  ReportTemplate,
  UpdateReportConfigRequest,
} from "./lien-report.types";

const BASE_PATH = "/lien/report/diy";

export const lienReportsApi = {
  list() {
    return apiClient.get<ApiResponse>(`/lien/api/liens/reports/diy/saved`);
  },

  getById(id: string) {
    return apiClient.get<ReportsResponse>(
      `/lien/api/liens/reports/diy/saved/${id}`,
    );
  },

  createTemplate(request: ReportTemplate) {
    return apiClient.post<ApiResponse>(`${BASE_PATH}`, request);
  },

  createReport(request: CreateReports) {
    return apiClient.post<ApiResponse>(`${BASE_PATH}/save`, request);
  },

  export(request: ExportReportRequest) {
    return apiClient.post<ApiResponse>(`${BASE_PATH}/export`, request);
  },

  update(request: UpdateReportConfigRequest) {
    return apiClient.post<ApiResponse[]>(`${BASE_PATH}/export`, request);
  },

  delete(id: string) {
    return apiClient.delete<ApiResponse[]>(
      `/lien/api/liens/reports/diy/saved/${id}`,
    );
  },
};
