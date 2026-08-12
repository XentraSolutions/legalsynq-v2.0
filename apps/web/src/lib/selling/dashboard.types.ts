export interface DashboardQuery {
  tab?: string;
  search?: string;
  fundingCompanyId?: string;
  lawFirmId?: string;
  caseManagerId?: string;
  facilityId?: string;
  initialServiceDateFrom?: string;
  initialServiceDateTo?: string;
  sortBy?: string;
  initialServiceDate?: string;
  sortDirection?: "desc" | "asc";
  page?: number;
  pageSize?: number;
}
