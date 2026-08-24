import { apiClient } from "@/lib/api-client";
import { ApiResponse } from "../liens/lien-report.types";

const BASE = "/lien/api/liens/cases/global-search";

export const liensGlobalSearch = {
  list(query: any) {
    return apiClient.post<any>(`${BASE}`,query);
  },  
};
