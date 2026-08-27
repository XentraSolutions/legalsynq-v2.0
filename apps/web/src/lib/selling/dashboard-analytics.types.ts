export interface SellingOperationsDashboardQuery {
  startDate?: string;
  endDate?: string;
  compare?: "none" | "previousPeriod";
}

export interface SellingOperationsDashboardPeriod {
  startDate: string;
  endDate: string;
  dateBasis: string;
}

export interface SellingOperationsMetric {
  isAvailable: boolean;
  value: number | null;
  comparisonValue: number | null;
  changeAmount: number | null;
  changePercent: number | null;
  formula: string;
  unavailableReason: string | null;
}

export interface SellingOperationsDashboardMetrics {
  totalLienRevenue: SellingOperationsMetric;
  totalOutstanding: SellingOperationsMetric;
  pastAmountDue: SellingOperationsMetric;
  payments: SellingOperationsMetric;
}

export interface SellingOperationsAgingBucket {
  bucket: string;
  amount: number;
  lienCount: number;
}

export interface SellingOperationsArAgingResponse {
  isAvailable: boolean;
  unavailableReason: string | null;
  total: number | null;
  buckets: SellingOperationsAgingBucket[];
}

export interface SellingOperationsStatusItem {
  status: string;
  lienCount: number;
  originalAmount: number;
  outstandingAmount: number;
  percentOfLiens: number;
}

export interface SellingOperationsTimeseriesPoint {
  bucketStart: string;
  grain: string;
  lienCount: number;
  lienRevenue: number;
  outstandingAmount: number;
}

export interface SellingOperationsTopBuyerItem {
  buyerOrgId: string;
  buyerCompanyId: string | null;
  buyerName: string;
  activeLienCount: number;
  totalBalance: number;
  completedPurchaseAmount: number;
  percentOfTotalBalance: number;
}

export interface SellingOperationsBuyerAgingItem {
  buyerOrgId: string;
  buyerCompanyId: string | null;
  buyerName: string;
  total: number;
  pastDuePercent: number | null;
  buckets: SellingOperationsAgingBucket[];
}

export interface SellingOperationsBuyerAgingResponse {
  isAvailable: boolean;
  unavailableReason: string | null;
  items: SellingOperationsBuyerAgingItem[];
}

export interface SellingOperationsDashboardResponse {
  period: SellingOperationsDashboardPeriod;
  comparisonPeriod: SellingOperationsDashboardPeriod | null;
  currency: string;
  metrics: SellingOperationsDashboardMetrics;
  arAging: SellingOperationsArAgingResponse;
  lienStatuses: SellingOperationsStatusItem[];
  sellerStatuses: SellingOperationsStatusItem[];
  timeSeries: SellingOperationsTimeseriesPoint[];
  topBuyers: SellingOperationsTopBuyerItem[];
  buyerAging: SellingOperationsBuyerAgingResponse;
  generatedAtUtc: string;
}
