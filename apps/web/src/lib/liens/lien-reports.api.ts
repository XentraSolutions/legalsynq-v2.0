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
  FilterQuery,
  ReportColumnReponse,
  ReportListResponse,
  ReportsResponse,
  ReportTemplate,
  UpdateReportConfigRequest,
  ViewType,
} from "./lien-report.types";
import { ReportColumnDto } from "../reports/reports.types";
import { ExportResponse } from "../cases/cases.types";

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
  getColumns(viewType: ViewType) {
    return apiClient.get<ReportColumnReponse>(
      `${BASE_PATH}/columns?reportType=${viewType}`,
    );
  },

  getFilterOptions(query: FilterQuery) {
    return apiClient.post<any>(`${BASE_PATH}/filter-options`, query);
  },

  getAllFilterOptions(viewType: ViewType) {
    return apiClient.get<any>(
      `${BASE_PATH}/all-filters?reportType=${viewType}&limit=20`,
    );
  },

  createTemplate(request: ReportTemplate) {
    return apiClient.post<ReportListResponse>(`${BASE_PATH}`, request);
  },

  createReport(request: CreateReports) {
    return apiClient.post<ApiResponse>(`${BASE_PATH}/save`, request);
  },

  export(request: ExportReportRequest) {
    return apiClient.post<ExportResponse>(`${BASE_PATH}/export`, request);
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
