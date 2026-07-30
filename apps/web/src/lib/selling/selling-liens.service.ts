import { liensApi } from "./selling-liens.api";
import {
  sellingLookupsApi,
  type SellingLookupItem,
  type SellingMedicalCodeLookupItem,
} from "./lookup.api";
import { lookupService } from "../lookup";
import { mapOfferToItem, mapPagination } from "./liens.mapper";
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
} from "./liens.types";
import { DashboardQuery } from "./dashboard.types";
import { DraftLienParams, LienInfoParams } from "../liens/liens.types";
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
      items: data.items,
      pagination: mapPagination(data),
    };
  },

  async getLienById(id: string): Promise<LienDetailsResult> {
    const { data } = await liensApi.getById(id);
    return data;
  },

  async getSellingDashboard(query: DashboardQuery = {}): Promise<any> {
    const { data } = await liensApi.getDashboard(query);
    console.log(data);
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

  async createLienInfo(request: LienInfoParams): Promise<LienDetail> {
    const { data } = await liensApi.createLienInfo(request);
    return data;
  },

  async createLienDraft(request: DraftLienParams): Promise<LienDetail> {
    const { data } = await liensApi.createLienDraft(request);
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

  async getFundingCompanies(): Promise<SellingLookupItem[]> {
    const { data } = await sellingLookupsApi.fundingCompanies();
    return data.items;
  },

  async getFundingCompanyContacts(
    fundingCompanyId: string,
  ): Promise<SellingLookupItem[]> {
    const { data } =
      await sellingLookupsApi.fundingCompanyContacts(fundingCompanyId);
    return data.items;
  },

  async getMedicalCodes(
    search: string,
  ): Promise<SellingMedicalCodeLookupItem[]> {
    // TODO: swap back once /selling/api/liens/selling/lookups/medical-codes
    // exists on the backend (see sellingLookupsApi.medicalCodes — currently
    // only a stub, route isn't implemented). Until then, reuse the lien
    // module's procedure-code lookup (/lien/lookup/medical/procedure/codes),
    // which already returns { code, description } and is used for the same
    // purpose by MedicalCodesDescription (add-medical-lien flow). It has no
    // `search` param, so filter client-side.
    // const { data } = await sellingLookupsApi.medicalCodes(search);
    // return data.items;
    const { data } = await lookupService.getMedicalProcedureCodes();
    const query = search.trim().toLowerCase();
    const items = data.map((item) => ({
      code: item.code,
      description: item.description,
    }));
    if (!query) return items;
    return items.filter(
      (item) =>
        item.code.toLowerCase().includes(query) ||
        item.description.toLowerCase().includes(query),
    );
  },
};
