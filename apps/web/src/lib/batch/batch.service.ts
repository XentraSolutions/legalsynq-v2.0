import { batchApi } from "./batch.api";
import { mapPagination } from "./batch.mapper";
import type {
  PaginationMeta,
  CreateBatchRequestDto,
  ProcessBatchDto,
  UpdateBatchRequestDto,
} from "./batch.types";

export const batchService = {
  async getBatchList(query: PaginationMeta): Promise<any> {
    const { data } = await batchApi.list(query);
    return {
      items: data.data,
      pagination: mapPagination(data),
    };
  },

  async getBatchById(batchId: string): Promise<any> {
    const { data } = await batchApi.getById(batchId);
    return data;
  },

  async createBatch(request: CreateBatchRequestDto): Promise<any> {
    const { data } = await batchApi.create(request);
    return data;
  },

  async upload(request: FormData): Promise<any> {
    const { data } = await batchApi.upload(request);
    return data;
  },

  async getDetails(id: string): Promise<any> {
    const { data } = await batchApi.details(id);
    return data;
  },

  async process(request: ProcessBatchDto): Promise<any> {
    const { data } = await batchApi.process(request);
    return data;
  },

  async update(request: UpdateBatchRequestDto): Promise<any> {
    const { data } = await batchApi.update(request);
    return data;
  },

  async dataContext(request: {
    id: string;
    page: number;
    limit: number;
  }): Promise<any> {
    const { data } = await batchApi.dataContext(request);
    return data;
  },

  async delete(id: string): Promise<any> {
    const { data } = await batchApi.delete(id);
    return data;
  },

  async deleteDetails(id: string): Promise<any> {
    const { data } = await batchApi.deleteDetails(id);
    return data;
  },

  async download(id: string): Promise<any> {
    const { data } = await batchApi.download(id);
    return data;
  },
};
