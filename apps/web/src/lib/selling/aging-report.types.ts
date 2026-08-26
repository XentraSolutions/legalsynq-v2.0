export interface MonthlyAgingReportQuery {
  asOfDate: string;
  page: number;
  pageSize: number;
}

export interface MonthlyAgingSummaryTotals {
  totalLiens: number;
  days1To30: number;
  days31To60: number;
  days61To90: number;
  days91To120: number;
  moreThan120: number;
  totalAmount: number;
}

export interface MonthlyAgingReportRow {
  lienCode: string;
  fundingCompany: string;
  days1To30: number;
  days31To60: number;
  days61To90: number;
  days91To120: number;
  moreThan120: number;
  totalAmount: number;
}

export interface MonthlyAgingReport {
  isSuccess: boolean;
  message: string;
  asOfDate: string;
  currency: string;
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  summaryTotals: MonthlyAgingSummaryTotals;
  data: MonthlyAgingReportRow[];
}
