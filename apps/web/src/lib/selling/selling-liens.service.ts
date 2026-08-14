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
  SaveSellingCaseInformationRequest,
  SaveSellingMedicalPricingRequest,
  SaveSellingDocumentsRequest,
  PrepareSellingLienRequest,
  ConfirmSellingLienSaleRequest,
  WithdrawSellingLienRequest,
  ArchiveSellingLienRequest,
  SubmitSellingLienRequest,
} from "./liens.types";
import { DashboardQuery } from "./dashboard.types";
import {
  CreateLienParams,
  CreateLienResult,
  LienInfoParams,
} from "../liens/liens.types";
import { LienDetailsResult } from "@/types/lien-selling";

export interface LienListResult {
  items: LienListItem[];
  pagination: PaginationMeta;
}

export interface LienOffersResult {
  items: LienOfferItem[];
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

  async getLienActivity(id: string) {
    const { data } = await liensApi.getActivity(id);
    return data;
  },

  async getSellingDashboard(query: DashboardQuery = {}): Promise<any> {
    const { data } = await liensApi.getDashboard(query);
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

  async saveLienInformation(
    lienId: string,
    request: SaveSellingLienInformationRequest,
  ): Promise<any> {
    const { data } = await liensApi.saveLienInformation(lienId, request);
    return data;
  },

  async saveCaseInformation(
    lienId: string,
    request: SaveSellingCaseInformationRequest,
  ): Promise<any> {
    const { data } = await liensApi.saveCaseInformation(lienId, request);
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

  async submitLien(
    lienId: string,
    request: SubmitSellingLienRequest,
  ): Promise<any> {
    const { data } = await liensApi.submitLien(lienId, request);
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
