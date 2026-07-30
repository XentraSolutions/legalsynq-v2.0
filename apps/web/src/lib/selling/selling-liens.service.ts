import { liensApi } from "./selling-liens.api";
import {
  mapLienToListItem,
  mapLienToDetail,
  mapOfferToItem,
  mapPagination,
} from "./liens.mapper";
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
};
