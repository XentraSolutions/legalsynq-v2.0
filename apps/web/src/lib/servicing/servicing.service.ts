import { batchApi } from "../batch/batch.api";
import {
  GenericPaginatedResult,
  GenericPaginationData,
} from "../lookup/lookup.types";
import { servicingApi } from "./servicing.api";
import {
  mapServicingToListItem,
  mapServicingToDetail,
  mapServicingPagination,
} from "./servicing.mapper";
import type {
  ServicingQuery,
  ServicingListItem,
  ServicingDetail,
  PaginationMeta,
  CreateServicingItemRequestDto,
  UpdateServicingItemRequestDto,
  UpdateServicingDetailsRequestDto,
  ServicingListItemResponseDto,
  ExportResponse,
  ServicingPaginationData,
  PaginatedResultDto,
  ServicingLienItem,
} from "./servicing.types";

export interface ServicingListResult {
  items: ServicingListItem[];
  pagination: PaginationMeta;
}

export const servicingService = {
  async getItems(query: ServicingPaginationData): Promise<ServicingListResult> {
    const { data } = await servicingApi.list(query);
    return {
      items: data.data.map(mapServicingToListItem),
      pagination: mapServicingPagination(data),
    };
  },

  async allLiensList(id: string): Promise<{ items: ServicingLienItem[] }> {
    const { data } = await servicingApi.allLiensList(id);
    return data;
  },

  async getCase(query: string): Promise<ServicingListResult> {
    const { data } = await servicingApi.getCase(query);
    return data;
  },

  async getItem(id: string): Promise<ServicingDetail> {
    const { data } = await servicingApi.getById(id);
    return mapServicingToDetail(data);
  },

  async createItem(
    request: CreateServicingItemRequestDto,
  ): Promise<ServicingDetail> {
    const { data } = await servicingApi.create(request);
    return mapServicingToDetail(data);
  },

  async updateItem(
    id: string,
    request: UpdateServicingItemRequestDto,
  ): Promise<ServicingDetail> {
    const { data } = await servicingApi.update(id, request);
    return mapServicingToDetail(data);
  },
  async updateDetails(
    request: UpdateServicingDetailsRequestDto,
  ): Promise<ServicingDetail> {
    const { data } = await servicingApi.updateDetails(request);
    return mapServicingToDetail(data);
  },

  async updateStatus(
    id: string,
    status: string,
    resolution?: string,
  ): Promise<ServicingDetail> {
    const { data } = await servicingApi.updateStatus(id, {
      status,
      resolution,
    });
    return mapServicingToDetail(data);
  },

  async export(): Promise<ExportResponse> {
    const { data } = await servicingApi.export();
    return data as ExportResponse;
  },
};
