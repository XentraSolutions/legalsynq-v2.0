import { liensApi } from "./selling-liens.api";
import {
  sellingLookupsApi,
  type SellingLookupItem,
  type SellingFundingCompanyContactItem,
  type SellingFacilityItem,
  type SellingMedicalCodeLookupItem,
} from "./lookup.api";
import { mapLienItem, mapOfferToItem, mapPagination } from "./liens.mapper";
import type {
  LiensQuery,
  LienListItem,
  LienDetail,
  LienOfferItem,
  PaginationMeta,
  CreateLienRequestDto,
  UpdateLienRequestDto,
  CreateLienOfferRequestDto,
  SaleFinalizationResultDto,
  SaveSellingLienInformationRequest,
  SaveSellingProviderFundingRequest,
  SaveSellingMedicalPricingRequest,
  SaveSellingDocumentsRequest,
  PrepareSellingLienRequest,
  ConfirmSellingLienSaleRequest,
  WithdrawSellingLienRequest,
  ArchiveSellingLienRequest,
  LienArchivedStatusResult,
  SubmitSellingLienRequest,
  MoveToManagementRequest,
  CaseDraftRequest,
  CaseDraftResult,
  FinalizeCaseDraftRequest,
  FinalizeCaseDraftResult,
  UpdateCaseRequest,
  UpdateCaseResult,
  CaseDetailResult,
  UpdateCasePlaintiffRequest,
  UpdateCasePlaintiffResult,
  CaseSearchQuery,
  CaseSearchItem,
} from "./liens.types";
import type {
  MonthlyAgingReport,
  MonthlyAgingReportQuery,
} from "./aging-report.types";
import type {
  SellingOperationsDashboardQuery,
  SellingOperationsDashboardResponse,
} from "./dashboard-analytics.types";
import { DashboardQuery } from "./dashboard.types";
import {
  CreateLienParams,
  CreateLienResult,
  LienInfoParams,
} from "../liens/liens.types";
import { LienDetailsResult } from "@/types/lien-selling";
import type {
  BulkImportSummary,
  BulkImportRowsResult,
  BulkImportRowStatus,
  SellerLienMessage,
  SellerLienMessagesResult,
} from "./liens.types";

export interface LienListResult {
  items: LienListItem[];
  pagination: PaginationMeta;
}

export interface LienOffersResult {
  items: LienOfferItem[];
}

export interface CaseSearchResult {
  items: CaseSearchItem[];
  pagination: PaginationMeta;
}

async function readJson(response: Response): Promise<string | null> {
  const text = await response.text();
  if (!text.trim()) return null;

  try {
    return JSON.parse(text) as any;
  } catch {
    return null;
  }
}

export const liensService = {
  async getLiens(query: LiensQuery = {}): Promise<LienListResult> {
    const { data } = await liensApi.list(query);
    return {
      items: data.items.map((item) => mapLienItem(item)),
      pagination: mapPagination(data),
    };
  },

  async getLienById(id: string): Promise<LienDetailsResult> {
    const { data } = await liensApi.getById(id);
    return data;
  },

  async getArchivedStatus(id: string): Promise<LienArchivedStatusResult> {
    const { data } = await liensApi.getArchivedStatus(id);
    return data;
  },

  async getLienActivity(id: string) {
    const { data } = await liensApi.getActivity(id);
    return data;
  },

  async getLienMessages(id: string): Promise<SellerLienMessagesResult> {
    const { data } = await liensApi.getMessages(id);
    return data;
  },

  async sendLienMessage(id: string, message: string, files: File[] = []): Promise<SellerLienMessage> {
    const trimmed = message.trim();
    const response = files.length > 0
      ? await liensApi.sendMessageForm(id, buildMessageForm(trimmed, files))
      : await liensApi.sendMessage(id, { message: trimmed });
    const { data } = response;
    return { ...data, isCurrentUser: true };
  },

  async getSellingDashboard(query: DashboardQuery = {}): Promise<any> {
    const { data } = await liensApi.getDashboard(query);
    return data;
  },

  async getMonthlyAgingReport(
    query: MonthlyAgingReportQuery,
  ): Promise<MonthlyAgingReport> {
    const { data } = await liensApi.getMonthlyAgingReport(query);
    return data;
  },

  async getAnalyticsDashboard(
    query: SellingOperationsDashboardQuery = {},
  ): Promise<SellingOperationsDashboardResponse> {
    const { data } = await liensApi.getAnalyticsDashboard(query);
    return data;
  },

  async upload(request: FormData): Promise<any> {
    const { data } = await liensApi.bulkUpload(request);
    return data;
  },

  async downloadTemplate(): Promise<Blob> {
    return liensApi.downloadTemplate();
  },

  async validateUpload(id: string): Promise<any> {
    const { data } = await liensApi.validateUpload(id);
    return data;
  },

  async confirmUpload(id: string): Promise<any> {
    const { data } = await liensApi.confirmUpload(id);
    return data;
  },

  async cancelUpload(id: string): Promise<any> {
    const { data } = await liensApi.cancelUpload(id);
    return data;
  },

  async getBulkImport(id: string): Promise<BulkImportSummary> {
    const { data } = await liensApi.getBulkImport(id);
    return data;
  },

  async getBulkImportRows(
    id: string,
    params: { status?: BulkImportRowStatus | "all"; page?: number; pageSize?: number } = {},
  ): Promise<BulkImportRowsResult> {
    const { data } = await liensApi.getBulkImportRows(id, params);
    return data;
  },

  async createLienInfo(
    lienId: string,
    request: LienInfoParams,
  ): Promise<LienDetail> {
    const { data } = await liensApi.createLienInfo(lienId, request);
    return data;
  },

  async createLien(request: CreateLienParams): Promise<CreateLienResult> {
    const { data } = await liensApi.createLien(request);
    return data;
  },

  async createCaseDraft(request: CaseDraftRequest): Promise<CaseDraftResult> {
    const { data } = await liensApi.createCaseDraft(request);
    return data;
  },

  async updateCaseDraft(
    draftId: string,
    request: CaseDraftRequest,
  ): Promise<CaseDraftResult> {
    const { data } = await liensApi.updateCaseDraft(draftId, request);
    return data;
  },

  async getCaseDraftById(draftId: string): Promise<CaseDraftResult> {
    const { data } = await liensApi.getCaseDraftById(draftId);
    return data;
  },

  async finalizeCaseDraft(
    draftId: string,
    request: FinalizeCaseDraftRequest,
  ): Promise<FinalizeCaseDraftResult> {
    const { data } = await liensApi.finalizeCaseDraft(draftId, request);
    return data;
  },

  async updateCase(
    caseId: string,
    request: UpdateCaseRequest,
  ): Promise<UpdateCaseResult> {
    const { data } = await liensApi.updateCase(caseId, request);
    return data;
  },

  async getCaseById(caseId: string): Promise<CaseDetailResult> {
    const { data } = await liensApi.getCaseById(caseId);
    return data;
  },

  async searchCases(query: CaseSearchQuery = {}): Promise<CaseSearchResult> {
    const { data } = await liensApi.searchCases(query);
    // The endpoint only returns `items`/`totalCount` — page/pageSize aren't
    // echoed back, unlike PaginatedResultDto<T>, so pagination is built from
    // what was requested rather than mapPagination().
    const page = query.page ?? 1;
    const pageSize = query.pageSize ?? (data.items.length || 1);
    return {
      items: data.items,
      pagination: {
        page,
        pageSize,
        totalCount: data.totalCount,
        totalPages: Math.ceil(data.totalCount / Math.max(pageSize, 1)),
      },
    };
  },

  async updateCasePlaintiff(
    caseId: string,
    request: UpdateCasePlaintiffRequest,
  ): Promise<UpdateCasePlaintiffResult> {
    const { data } = await liensApi.updateCasePlaintiff(caseId, request);
    return data;
  },

  async saveLienInformation(
    lienId: string,
    request: SaveSellingLienInformationRequest,
  ): Promise<any> {
    const { data } = await liensApi.saveLienInformation(lienId, request);
    return data;
  },

  async saveProviderFundingDetails(
    lienId: string,
    request: SaveSellingProviderFundingRequest,
  ): Promise<any> {
    const { data } = await liensApi.saveProviderFundingDetails(lienId, request);
    return data;
  },

  async saveMedicalPricing(
    lienId: string,
    request: SaveSellingMedicalPricingRequest,
  ): Promise<any> {
    const { data } = await liensApi.saveMedicalPricing(lienId, request);
    return data;
  },

  async saveDocuments(
    lienId: string,
    request: SaveSellingDocumentsRequest,
  ): Promise<any> {
    const { data } = await liensApi.saveDocuments(lienId, request);
    return data;
  },

  async prepareSale(
    lienId: string,
    request: PrepareSellingLienRequest,
  ): Promise<any> {
    const { data } = await liensApi.prepareSale(lienId, request);
    return data;
  },

  async confirmSale(
    lienId: string,
    request: ConfirmSellingLienSaleRequest,
  ): Promise<any> {
    const { data } = await liensApi.confirmSale(lienId, request);
    return data;
  },

  async withdrawSale(
    lienId: string,
    request: WithdrawSellingLienRequest = {},
  ): Promise<any> {
    const { data } = await liensApi.withdrawSale(lienId, request);
    return data;
  },

  async archiveLien(
    lienId: string,
    request: ArchiveSellingLienRequest = {},
  ): Promise<any> {
    const { data } = await liensApi.archiveLien(lienId, request);
    return data;
  },

  async restoreLien(lienId: string): Promise<any> {
    const { data } = await liensApi.restoreLien(lienId);
    return data;
  },

  async submitLien(
    lienId: string,
    request: SubmitSellingLienRequest,
  ): Promise<any> {
    const { data } = await liensApi.submitLien(lienId, request);
    return data;
  },

  async moveToManagement(
    lienId: string,
    request: MoveToManagementRequest = {},
  ): Promise<any> {
    const { data } = await liensApi.moveToManagement(lienId, request);
    return data;
  },

  async getFundingCompanies(): Promise<SellingLookupItem[]> {
    const { data } = await sellingLookupsApi.fundingCompanies();
    return data.items;
  },

  async getFundingCompanyContacts(
    fundingCompanyId: string,
  ): Promise<SellingFundingCompanyContactItem[]> {
    const { data } =
      await sellingLookupsApi.fundingCompanyContacts(fundingCompanyId);
    return data.items;
  },

  async getMedicalCodes(
    search: string,
  ): Promise<SellingMedicalCodeLookupItem[]> {
    const { data } = await sellingLookupsApi.medicalCodes(search);
    return data.data;
  },

  async getFacilities(): Promise<SellingFacilityItem[]> {
    const { data } = await sellingLookupsApi.facilities();
    return data.items;
  },

  async getLawFirms(): Promise<SellingLookupItem[]> {
    const { data } = await sellingLookupsApi.lawFirms();
    return data.items;
  },

  async getCaseManagers(lawFirmId: string): Promise<SellingLookupItem[]> {
    const { data } = await sellingLookupsApi.caseManagers(lawFirmId);
    return data.items;
  },
};

function buildMessageForm(message: string, files: File[]): FormData {
  const form = new FormData();
  form.append("message", message);
  files.forEach((file) => form.append("files", file, file.name));
  return form;
}
