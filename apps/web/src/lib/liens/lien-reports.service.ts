import {
  ApiResponse,
  CreateReports,
  ExportReportRequest,
  ReportConfigResponse,
  ReportListResponse,
  ReportsResponse,
  ReportTemplate,
  UpdateReportConfigRequest,
} from "./lien-report.types";
import { lienReportsApi } from "./lien-reports.api";
import {
  mapReportToListItem,
  mapReportToTemplate,
} from "./lien-reports.mapper";

export const lienReportsService = {
  async getReports(): Promise<ReportListResponse> {
    const { data } = await lienReportsApi.list();
    return { items: data.map(mapReportToListItem) };
  },

  async getReportsById(id: string): Promise<ReportsResponse> {
    const { data } = await lienReportsApi.getById(id);
    return data ?? [];
  },

  async generateTemplate(request: ReportTemplate): Promise<ReportListResponse> {
    const { data } = await lienReportsApi.createTemplate(request);
    if (!data) throw new Error("Failed to create report");
    console.log(data.data.map(mapReportToTemplate));
    return { ...data, items: data.data.map(mapReportToTemplate) };
  },
  async createReports(request: CreateReports): Promise<ApiResponse> {
    const { data } = await lienReportsApi.createReport(request);
    if (!data) throw new Error("Failed to create report");
    return data ?? [];
  },

  async exportReports(request: ExportReportRequest): Promise<ApiResponse> {
    const { data } = await lienReportsApi.export(request);
    if (!data) throw new Error("Failed to export report");
    return {
      isSuccess: data.isSuccess,
      message: data.message,
      data: data.data,
    };
  },

  async updateReports(
    request: UpdateReportConfigRequest,
  ): Promise<ApiResponse[]> {
    const { data } = await lienReportsApi.update(request);
    if (!data) throw new Error("Failed to update report");
    return data ?? [];
  },

  async deleteReports(id: string): Promise<ApiResponse[]> {
    const { data } = await lienReportsApi.delete(id);
    if (!data) throw new Error("Failed to delete report");
    return data ?? [];
  },
};
