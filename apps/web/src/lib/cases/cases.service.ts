import { useSessionContext } from "@/providers/session-provider";
import { ApiResponse } from "../liens/lien-report.types";
import { lookupApi } from "../lookup/lookup.api";
import { casesApi } from "./cases.api";
import {
  mapCaseToListItem,
  mapCaseToDetail,
  mapLienToListItem,
  mapPagination,
  mapDtoToUpdateRequest,
  mapMedicalInfo,
  mapMedicalCodes,
  mapDocuments,
} from "./cases.mapper";
import type {
  CasesQuery,
  CaseListItem,
  CaseDetail,
  CaseLienItem,
  PaginationMeta,
  CreateCaseRequestDto,
  UpdateCaseRequestDto,
  DashboardStats,
  CaseListResult,
  CaseLiensResult,
  CasePaginatedParams,
  CasesFilters,
  ExportResponse,
  CaseAllocationReportRequest,
  CaseReportItem,
  LienReportItem,
  AllocationSegment,
  CashMetricResponse,
  CreateMedicalLiensDto,
  CreateMedicalFacilityDto,
  CreateMedicalPaymentDto,
  CreateMedicalCodeDto,
  CreateMedicalCodeLiensDto,
  CaseLiensFilters,
  UpdateCasePersonalRequestDto,
  UpdateCaseDetailsRequestDto,
  MedicalCodeLiensResponse,
  CaseResponseDto,
} from "./cases.types";
import { lookupService } from "../lookup";

export const casesService = {
  async getCases(query: CasesQuery = {}): Promise<CaseListResult> {
    const { data } = await casesApi.listBySearch(query);
    const pagination = {
      page: data.page,
      pageSize: data.limit,
      totalCount: data.totalCount,
    };
    return {
      items: data.data.map(mapCaseToListItem),
      pagination: mapPagination({
        ...pagination,
      }),
    };
  },

  async getCase(caseId: string): Promise<CaseDetail> {
    const { data } = await casesApi.getById(caseId);
    return mapCaseToDetail(data);
  },

  async createCase(request: CreateCaseRequestDto): Promise<{ id: string }> {
    const { data } = await casesApi.create(request);
    return { id: data.data.id };
  },

  async deleteCase(id: string): Promise<ApiResponse> {
    const { data } = await casesApi.deleteCase(id);
    return data;
  },

  async updateCasePersonal(
    request: UpdateCasePersonalRequestDto,
  ): Promise<CaseDetail> {
    // const dto: UpdateCaseDetailsRequestDto = {
    //   firstname: request.firstName,
    //   lastname: request.lastName,
    //   request.dob && { dateOfIncident: request.dob }),
    // };
    console.log(request);
    const { data } = await casesApi.updatePersonal(request);
    return mapCaseToDetail(data);
  },

  async updateCase(request: UpdateCaseRequestDto | any): Promise<CaseDetail> {
    const { data } = await casesApi.updateCase(request);
    return mapCaseToDetail(data);
  },

  async updateCaseStatus(
    caseId: string,
    newStatus: string,
  ): Promise<CaseDetail> {
    const { data: freshDto } = await casesApi.getById(caseId);
    const request = mapDtoToUpdateRequest(freshDto);
    request.status = newStatus;
    return this.updateCase(request);
  },

  async getCaseLiens(caseId: string): Promise<CaseLiensResult> {
    const { data } = await casesApi.listLiensByCase({
      CaseId: caseId,
      page: 1,
      limit: 10,
    });

    return {
      items: data.items.map(mapLienToListItem),
      pagination: mapPagination({ ...data, pageSize: data.limit }),
    };
  },

  async getCaseUpdates(caseId: string): Promise<any> {
    const { data } = await casesApi.getCaseUpdates({
      CaseId: caseId,
      page: 1,
      limit: 10,
    });
    const payload = data as unknown as { data?: unknown };
    return Array.isArray(payload.data) ? payload.data : [];
  },

  async getCaseLiensUpdates(caseId: string): Promise<any> {
    const { data } = await casesApi.listLiensUpdates({
      CaseId: caseId,
      page: 1,
      limit: 10,
    });
    return data;
  },

  async getCaseStatus(): Promise<any> {
    const { data } = await lookupApi.getCaseStatus();
    return data.sort((a, b) => a.sortOrder - b.sortOrder);
  },
  async getDashboardStats(): Promise<DashboardStats> {
    const { data } = await casesApi.getDashboardStats();
    return data;
  },

  async getLawFirmCaseAllocation(
    request: CaseAllocationReportRequest,
  ): Promise<{ segments: AllocationSegment[]; rows: CaseReportItem[] }> {
    const { data } = await casesApi.getLawFirmCaseReport(request);
    const rows = data.items ?? [];
    return { segments: groupAndCount(rows, (item) => item.lawFirm), rows };
  },

  async getMedicalFacilityCaseAllocation(
    request: CaseAllocationReportRequest,
  ): Promise<{ segments: AllocationSegment[]; rows: CaseReportItem[] }> {
    const { data } = await casesApi.getMedicalProviderCaseReport(request);
    const rows = data.items ?? [];
    return {
      segments: groupAndCount(rows, (item) => item.medicalFacility),
      rows,
    };
  },

  async getTotalLienReportRows(
    request: CaseAllocationReportRequest,
  ): Promise<{ items: LienReportItem[]; totalCount: number }> {
    const { data } = await casesApi.getTotalLienReport(request);
    return { items: data.items ?? [], totalCount: data.totalCount ?? 0 };
  },

  async getTotalCaseReportRows(
    request: CaseAllocationReportRequest,
  ): Promise<{ items: CaseReportItem[]; totalCount: number }> {
    const { data } = await casesApi.getTotalCaseReport(request);
    return { items: data.items ?? [], totalCount: data.totalCount ?? 0 };
  },

  async getCashReceived(
    request: CaseAllocationReportRequest,
  ): Promise<CashMetricResponse> {
    const { data } = await casesApi.getCashReceived(request);
    return data.data;
  },

  async getCashDeployed(
    request: CaseAllocationReportRequest,
  ): Promise<CashMetricResponse> {
    const { data } = await casesApi.getCashDeployed(request);
    return data.data;
  },

  async exportCases(request: CasesFilters): Promise<ExportResponse> {
    const { data } = await casesApi.export(request);
    return data as ExportResponse;
  },

  async payoffQoute(caseId: string): Promise<{ url: string; message: string }> {
    const { data } = await casesApi.payoffQoute(caseId);
    return {
      url: data.isSuccess ? data?.data?.url : "",
      message: data.message,
    };
  },

  async exportCaseLiens(request: CaseLiensFilters): Promise<ExportResponse> {
    const { data } = await casesApi.exportCaseLiens(request);
    return data as unknown as ExportResponse;
  },

  async createMedicalLiens(request: CreateMedicalLiensDto): Promise<any> {
    const { data } = await casesApi.createMedicalLiens(request);
    return data;
  },

  async createMedicalFacilityLiens(
    request: CreateMedicalFacilityDto,
  ): Promise<any> {
    const { data } = await casesApi.createMedicalFacilityLiens(request);
    return data;
  },

  async updateMedicalLiens(request: CreateMedicalLiensDto): Promise<any> {
    const { data } = await casesApi.updateMedicalLiens(request);
    return data;
  },

  async updateMedicalFacilityLiens(
    request: CreateMedicalFacilityDto,
  ): Promise<any> {
    const { data } = await casesApi.updateMedicalFacilityLiens(request);
    return data;
  },

  async createMedicalPaymentLiens(
    request: CreateMedicalPaymentDto,
  ): Promise<any> {
    const { data } = await casesApi.createMedicalPaymentLiens(request);
    return data;
  },

  async createMedicalCodeLiens(
    request: CreateMedicalCodeLiensDto,
  ): Promise<any> {
    const { data } = await casesApi.createMedicalCodeLiens(request);
    return data;
  },

  async updateMedicalCodeLiens(
    request: CreateMedicalCodeLiensDto,
  ): Promise<ApiResponse> {
    const { data } = await casesApi.updateMedicalCodeLiens(request);
    return data;
  },

  async deleteMedicalCodeLiens(id: string): Promise<ApiResponse> {
    const { data } = await casesApi.deleteMedicalCodeLiens(id);
    return data;
  },

  async createMedicalCode(request: CreateMedicalCodeDto): Promise<any> {
    const { data } = await casesApi.createMedicalCode(request);
    return data;
  },

  async getMedicalInfo(lienId: string): Promise<any> {
    const { data } = await casesApi.getMedicalInfo(lienId);
    const medicalData = (data as unknown as { data?: any }).data ?? data;
    return { data: mapMedicalInfo(medicalData) };
  },

  async getMedicalFacility(lienId: string): Promise<any> {
    const { data } = await casesApi.getMedicalFacility(lienId);
    return data;
  },

  async getMedicalCodes(
    lienId: string,
  ): Promise<{ data: { codeRows: MedicalCodeLiensResponse[] } }> {
    const { data } = await casesApi.getMedicalCode(lienId);
    const codeData = (data as unknown as { data?: any }).data ?? data;
    return { data: mapMedicalCodes(codeData) };
  },

  async getMedicalDocument(lienId: string): Promise<any> {
    const { data } = await casesApi.getMedicalDocument(lienId);
    return data;
  },

  async getPayee(lienId: string): Promise<any> {
    const { data } = await casesApi.getPayee(lienId);
    return data;
  },

  async uploadLiensDocuments(request: any): Promise<any> {
    const { data } = await casesApi.uploadLiensDocument(request);
    return mapCaseToDetail(data);
  },

  async uploadCaseDocuments(request: any): Promise<any> {
    const { data } = await casesApi.uploadCaseDocument(request);
    return mapCaseToDetail(data);
  },

  async loadDocuments(request: any): Promise<any> {
    const { data } = await casesApi.listDocumentsByCase(request);
    const documents = await lookupService.getDocumentType();
    return mapDocuments(data, documents);
  },

  async loadLiensDocuments(liensId: string): Promise<any> {
    const { data } = await casesApi.listDocumentsByLiens(liensId);
    return data;
  },

  async deleteLiensDocument(documentId: string): Promise<any> {
    const { data } = await casesApi.deleteDocumentsByLiens(documentId);
    return data;
  },

  async deleteCaseDocument(documentId: string): Promise<any> {
    const { data } = await casesApi.deleteDocumentsByCase(documentId);
    return data;
  },

  async mergeCase(request: {
    caseIdA: string;
    caseIdB: string;
  }): Promise<CaseResponseDto> {
    const { data } = await casesApi.mergecase(request);
    return data;
  },
};

function groupAndCount(
  items: CaseReportItem[],
  getKey: (item: CaseReportItem) => unknown,
): AllocationSegment[] {
  const counts = new Map<string, number>();
  for (const item of items) {
    const raw = getKey(item);
    const key = typeof raw === "string" && raw.trim() ? raw : "Unknown";
    counts.set(key, (counts.get(key) ?? 0) + 1);
  }
  return Array.from(counts.entries()).map(([label, value]) => ({
    label,
    value,
  }));
}
