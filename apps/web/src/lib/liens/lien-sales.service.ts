import { lienSalesApi } from './lien-sales.api';
import type {
  AddSellingPortfolioLiensRequestDto,
  CreateSellingPortfolioRequestDto,
  SellingPortfolioQuery,
  UpdateSellingPortfolioRequestDto,
} from './lien-sales.types';

export const lienSalesService = {
  async list(query: SellingPortfolioQuery = {}) {
    const { data } = await lienSalesApi.list(query);
    return data;
  },

  async getPortfolio(id: string) {
    const [{ data: portfolio }, { data: activity }, { data: analytics }, { data: statusHistory }] =
      await Promise.all([
        lienSalesApi.getById(id),
        lienSalesApi.getActivity(id),
        lienSalesApi.getAnalytics(id),
        lienSalesApi.getStatusHistory(id),
      ]);

    return { portfolio, activity, analytics, statusHistory };
  },

  async create(request: CreateSellingPortfolioRequestDto) {
    const { data } = await lienSalesApi.create(request);
    return data;
  },

  async update(id: string, request: UpdateSellingPortfolioRequestDto) {
    const { data } = await lienSalesApi.update(id, request);
    return data;
  },

  async addLiens(id: string, request: AddSellingPortfolioLiensRequestDto) {
    const { data } = await lienSalesApi.addLiens(id, request);
    return data;
  },

  async removeLiens(id: string, lienIds: string[]) {
    const { data } = await lienSalesApi.removeLiens(id, lienIds);
    return data;
  },

  async publish(id: string, notes?: string) {
    const { data } = await lienSalesApi.publish(id, notes);
    return data;
  },

  async withdraw(id: string, notes?: string) {
    const { data } = await lienSalesApi.withdraw(id, notes);
    return data;
  },
};
