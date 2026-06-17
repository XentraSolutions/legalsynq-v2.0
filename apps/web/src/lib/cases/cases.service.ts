import { lookupApi } from "../lookup/lookup.api";
import { casesApi } from "./cases.api";
import {
  mapCaseToListItem,
  mapCaseToDetail,
  mapLienToListItem,
  mapPagination,
  mapDtoToUpdateRequest,
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
} from "./cases.types";

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

  async createCase(request: CreateCaseRequestDto): Promise<CaseDetail> {
    const { data } = await casesApi.create(request);
    return mapCaseToDetail(data);
  },

  async updateCase(
    caseId: string,
    request: UpdateCaseRequestDto,
  ): Promise<CaseDetail> {
    const { data } = await casesApi.update(caseId, request);
    return mapCaseToDetail(data);
  },

  async updateCaseStatus(
    caseId: string,
    newStatus: string,
  ): Promise<CaseDetail> {
    const { data: freshDto } = await casesApi.getById(caseId);
    const request = mapDtoToUpdateRequest(freshDto);
    request.status = newStatus;
    return this.updateCase(caseId, request);
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
    return data;
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
    return data.data;
  },

  async exportCases(request: CasesFilters): Promise<ExportResponse> {
    const { data } = await casesApi.export(request);
    return data;
  },
};
