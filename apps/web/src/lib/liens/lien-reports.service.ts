import { CaseListItem } from "../cases";
import {
  ApiResponse,
  ColumnGroup,
  CreateReports,
  ExportReportRequest,
  FilterQuery,
  ReportColumnReponse,
  ReportListResponse,
  ReportsResponse,
  ReportTemplate,
  ReportTotals,
  UpdateReportConfigRequest,
  ViewType,
} from "./lien-report.types";
import { lienReportsApi } from "./lien-reports.api";
import {
  mapReportToListItem,
  mapReportToTemplate,
} from "./lien-reports.mapper";

export const lienReportsService = {
  async getReports(): Promise<ReportListResponse> {
    const { data } = await lienReportsApi.list();
    const payload = (data as unknown as { data?: unknown })?.data ?? data;
    const items = Array.isArray(payload) ? payload : [];
    return {
      ...(data as unknown as Record<string, unknown>),
      items: items.map(mapReportToListItem),
    } as ReportListResponse;
  },

  async getReportsById(id: string): Promise<ReportsResponse> {
    const { data } = await lienReportsApi.getById(id);
    return (data as ReportsResponse) ?? ({} as ReportsResponse);
  },

  async generateTemplate(
    request: ReportTemplate | ReportsResponse,
  ): Promise<ReportListResponse> {
    const { data } = await lienReportsApi.createTemplate(
      request as ReportTemplate,
    );
    if (!data) throw new Error("Failed to create report");
    const payload =
      (data as unknown as { data?: unknown; summaryTotals?: ReportTotals })
        ?.data ?? data;
    const items = Array.isArray(payload) ? payload : [];
    return {
      ...(data as unknown as Record<string, unknown>),
      items: items.map(mapReportToTemplate),
      data: items as CaseListItem[],
      summaryTotals: (data as unknown as { summaryTotals?: ReportTotals })
        .summaryTotals,
    };
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

  async getColumns(viewBy: ViewType): Promise<ReportColumnReponse> {
    const { data } = await lienReportsApi.getColumns(viewBy);
    return data;
  },

  async getFilterOptions(query: FilterQuery): Promise<ApiResponse[]> {
    const { data } = await lienReportsApi.getFilterOptions(query);
    return data;
  },
  async getAllFilterOptions(viewBy: ViewType): Promise<ApiResponse[]> {
    const { data } = await lienReportsApi.getAllFilterOptions(viewBy);
    return data.data;
  },
};
