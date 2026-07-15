import { apiClient } from "@/lib/api-client";
import type {
  CaseResponseDto,
  PaginatedResultDto,
  CreateCaseRequestDto,
  UpdateCaseRequestDto,
  CasesQuery,
  DashboardStats,
  CasePaginatedParams,
  CasePaginatedResult,
  CaseListApiResponse,
  ContactCaseLookupParams,
  CaseLiensApiResponse,
  CasesFilters,
  ExportResponse,
  CaseAllocationReportRequest,
  CaseReportItem,
  LienReportItem,
  ReportPaginatedResult,
  CashMetricApiResponse,
  CreateMedicalLiensDto,
  CreateMedicalFacilityDto,
  CreateMedicalPaymentDto,
  CreateMedicalCodeDto,
  CreateMedicalCodeLiensDto,
  CaseLiensFilters,
  UpdateCasePersonalRequestDto,
  UpdateCaseDetailsRequestDto,
  BatchReassignCasesRequestDto,
  ReassignLeadRequestDto,
  ReassignLawFirmRequestDto,
  ReassignCaseManagerRequestDto,
} from "./cases.types";
import { ApiResponse } from "../liens/lien-report.types";

const BASE = "/lien/api/liens/cases";

// Report endpoints expect MM/DD/YYYY; the date pickers hand us YYYY-MM-DD.
function toApiDate(value?: string): string | undefined {
  if (!value) return undefined;
  const [y, m, d] = value.split("-");
  if (!y || !m || !d) return value;
  return `${m}/${d}/${y}`;
}

function withApiDates<T extends { startDate?: string; endDate?: string }>(
  request: T,
): T {
  return {
    ...request,
    startDate: toApiDate(request.startDate),
    endDate: toApiDate(request.endDate),
  };
}

export const casesApi = {
  list(id: CasesQuery) {
    return apiClient.get<CaseResponseDto>(`${BASE}`);
  },

  listBySearch(request: CasesQuery) {
    return apiClient.post<CasePaginatedResult>(`${BASE}/v3`, request);
  },

  getById(id: string) {
    return apiClient.get<CaseResponseDto>(`${BASE}/${id}`);
  },

  getByNumber(caseNumber: string) {
    return apiClient.get<CaseResponseDto>(
      `${BASE}/by-number/${encodeURIComponent(caseNumber)}`,
    );
  },

  create(request: CreateCaseRequestDto) {
    return apiClient.post<{ data: { id: string } }>(`${BASE}/create`, request);
  },

  mergecase(request: { caseIdA: string; caseIdB: string }) {
    return apiClient.post<CaseResponseDto>(`${BASE}/mergecase`, request);
  },

  deleteCase(id: string) {
    return apiClient.delete<ApiResponse>(`${BASE}/delete/${id}`);
  },

  updatePersonal(request: UpdateCasePersonalRequestDto) {
    return apiClient.patch<CaseResponseDto>(`${BASE}/personal-update`, request);
  },
  updateCase(request: UpdateCaseRequestDto) {
    return apiClient.patch<CaseResponseDto>(`${BASE}/details-update`, request);
  },

  update(id: string, request: UpdateCaseRequestDto) {
    return apiClient.put<CaseResponseDto>(`${BASE}/${id}`, request);
  },

  uploadLiensDocument(request: FormData) {
    return apiClient.postForm<any>(`${BASE}/liens/upload/document`, request);
  },

  uploadCaseDocument(request: FormData) {
    return apiClient.postForm<any>(`${BASE}/upload/document`, request);
  },

  listDocumentsByLiens(id: string) {
    return apiClient.get<CaseLiensApiResponse>(
      `/lien/api/liens/cases/liens/get-medicaldocument/${id}`,
    );
  },

  listDocumentsByCase(id: string) {
    return apiClient.get<CaseLiensApiResponse>(
      `/lien/api/liens/cases/get-allcasedocument/${id}`,
    );
  },

  deleteDocumentsByLiens(id: string) {
    return apiClient.delete<CaseLiensApiResponse>(
      `/lien/liens/delete-medicaldocument/${id}`,
    );
  },
  deleteDocumentsByCase(id: string) {
    return apiClient.delete<CaseLiensApiResponse>(
      `/lien/api/liens/liens/delete-casedocument/${id}`,
    );
  },

  listLiensByCase(request: CasePaginatedParams) {
    return apiClient.post<CaseLiensApiResponse>(
      `/lien/api/liens/cases/liens/v3`,
      request,
    );
  },
  listLiensUpdatesByCase(request: CasePaginatedParams) {
    return apiClient.post<PaginatedResultDto<unknown>>(
      `${BASE}/liens-updates/`,
      request,
    );
  },

  listLiensUpdates(request: CasePaginatedParams) {
    return apiClient.post<PaginatedResultDto<unknown>>(
      `${BASE}/liens-updates/v3`,
      request,
    );
  },

  getCaseUpdates(request: CasePaginatedParams) {
    return apiClient.post<PaginatedResultDto<unknown>>(
      `${BASE}/case-updates/v3`,
      request,
    );
  },

  getCaseUpdatesv1(id: string) {
    return apiClient.get<PaginatedResultDto<unknown>>(
      `${BASE}/case-updates/${id}`,
    );
  },

  getCaseLiens(id: string) {
    return apiClient.post<CaseResponseDto>(`${BASE}/liens/v3`, {});
  },

  getDashboardStats() {
    return apiClient.get<DashboardStats>(`${BASE}/dashboard/piechart`);
  },

  getLawFirmCaseReport(request: CaseAllocationReportRequest) {
    return apiClient.post<ReportPaginatedResult<CaseReportItem>>(
      `${BASE}/dashboard/lawfirm-case-report-export/v3`,
      withApiDates(request),
    );
  },

  getMedicalProviderCaseReport(request: CaseAllocationReportRequest) {
    return apiClient.post<ReportPaginatedResult<CaseReportItem>>(
      `${BASE}/dashboard/medical-provider-report-export/v3`,
      withApiDates(request),
    );
  },

  getTotalLienReport(request: CaseAllocationReportRequest) {
    return apiClient.post<ReportPaginatedResult<LienReportItem>>(
      `${BASE}/dashboard/total-lien-report-export/v3`,
      withApiDates(request),
    );
  },

  getTotalCaseReport(request: CaseAllocationReportRequest) {
    return apiClient.post<ReportPaginatedResult<CaseReportItem>>(
      `${BASE}/dashboard/total-case-report-export/v3`,
      withApiDates(request),
    );
  },

  getCashReceived(request: CaseAllocationReportRequest) {
    return apiClient.post<CashMetricApiResponse>(
      `${BASE}/dashboard/cash-received`,
      withApiDates(request),
    );
  },

  getCashDeployed(request: CaseAllocationReportRequest) {
    return apiClient.post<CashMetricApiResponse>(
      `${BASE}/dashboard/deployed`,
      withApiDates(request),
    );
  },

  export(request: CasesFilters) {
    return apiClient.post<ExportResponse>(`${BASE}/generate-csv`, request);
  },

  payoffQoute(caseId: string) {
    return apiClient.get<any>(`${BASE}/payoff-qoute/${caseId}`);
  },

  createMedicalLiens(request: CreateMedicalLiensDto) {
    return apiClient.post<ApiResponse>(`${BASE}/liens/medical`, request);
  },
  createMedicalFacilityLiens(request: CreateMedicalFacilityDto) {
    return apiClient.post<ApiResponse>(`${BASE}/liens/facility`, request);
  },
  createMedicalPaymentLiens(request: CreateMedicalPaymentDto) {
    return apiClient.post<ApiResponse>(`${BASE}/liens/payment`, request);
  },

  createMedicalCodeLiens(request: CreateMedicalCodeLiensDto) {
    return apiClient.post<ApiResponse>(`${BASE}/liens/medicalcode`, request);
  },

  updateMedicalCodeLiens(request: CreateMedicalCodeLiensDto) {
    return apiClient.post<ApiResponse>(
      `${BASE}/liens/update-medicalcode`,
      request,
    );
  },

  deleteMedicalCodeLiens(id: string) {
    return apiClient.delete<ApiResponse>(
      `${BASE}/liens/delete-medicalcode/${id}`,
    );
  },

  updateMedicalLiens(request: CreateMedicalLiensDto) {
    return apiClient.post<ApiResponse>(`${BASE}/liens/update-medical`, request);
  },
  updateMedicalFacilityLiens(request: CreateMedicalFacilityDto) {
    return apiClient.post<ApiResponse>(
      `${BASE}/liens//update-facility`,
      request,
    );
  },

  createMedicalCode(request: CreateMedicalCodeDto) {
    return apiClient.post<ApiResponse>(
      `${BASE}/manual/medical/code/create`,
      request,
    );
  },

  getMedicalInfo(lienId: string) {
    return apiClient.get<ApiResponse>(`${BASE}/liens/get-medical/${lienId}`);
  },

  getMedicalFacility(lienId: string) {
    return apiClient.get<ApiResponse>(`${BASE}/liens/get-facility/${lienId}`);
  },

  getMedicalCode(lienId: string) {
    return apiClient.get<ApiResponse>(
      `${BASE}/liens/get-medicalcode/${lienId}`,
    );
  },

  getMedicalDocument(lienId: string) {
    return apiClient.post<ApiResponse>(
      `${BASE}/liens/liens_get-medicaldocument`,
      { id: lienId },
    );
  },

  getPayee(lienId: string) {
    return apiClient.get<ApiResponse>(
      `${BASE}/liens/get-payee-outbound/${lienId}`,
    );
  },

  exportCaseLiens(request: CaseLiensFilters) {
    return apiClient.post<ApiResponse>(`${BASE}/liens/generate-csv/`, request);
  },

  listByLawFirm(lawFirmId: string, params: ContactCaseLookupParams) {
    return apiClient.post<CaseListApiResponse>(`${BASE}/law/v3`, {
      lawFirmId,
      keyword: params.keyword ?? "",
      page: params.page,
      limit: params.limit,
    });
  },

  listByLead(leadId: string, params: ContactCaseLookupParams) {
    return apiClient.post<CaseListApiResponse>(`${BASE}/leads/v3`, {
      leadId,
      keyword: params.keyword ?? "",
      page: params.page,
      limit: params.limit,
    });
  },

  listByFacility(facilityId: string, params: ContactCaseLookupParams) {
    return apiClient.post<CaseListApiResponse>(`${BASE}/medical/facility/v3`, {
      facilityId,
      keyword: params.keyword ?? "",
      page: params.page,
      limit: params.limit,
    });
  },

  listByMedicalProvider(medicalId: string, params: ContactCaseLookupParams) {
    return apiClient.post<CaseListApiResponse>(`${BASE}/medical/v3`, {
      medicalId,
      keyword: params.keyword ?? "",
      page: params.page,
      limit: params.limit,
    });
  },

  listByFundingCompany(
    fundingCompanyId: string,
    params: ContactCaseLookupParams,
  ) {
    return apiClient.post<CaseListApiResponse>(`${BASE}/funding/v3`, {
      fundingCompanyId,
      keyword: params.keyword ?? "",
      page: params.page,
      limit: params.limit,
    });
  },

  batchReassign(request: BatchReassignCasesRequestDto) {
    return apiClient.post<ApiResponse>(`${BASE}/batch-reassign`, request);
  },

  reassignLead(request: ReassignLeadRequestDto) {
    return apiClient.post<CaseResponseDto>(`${BASE}/reassign/leads`, request);
  },

  reassignLawFirm(request: ReassignLawFirmRequestDto) {
    return apiClient.post<CaseResponseDto>(`${BASE}/reassign/lawfirm`, request);
  },

  reassignCaseManager(request: ReassignCaseManagerRequestDto) {
    return apiClient.post<CaseResponseDto>(
      `${BASE}/reassign/casemanager`,
      request,
    );
  },
};
