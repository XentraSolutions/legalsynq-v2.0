export type ReportConfig = Record<string, unknown>;

export interface SavedReport {
  id: string;
  tenantId: string;
  userId: string;
  name: string;
  config: ReportConfig;
  createdAtUtc: string;
  updatedAtUtc: string;
  reportId: string;
  reportName: string;
  reportDescription?: string | null;
  reportType: string;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  reportConfig: ReportConfig;
  columnCount: number;
}

export interface RunReportRequest {
  config: ReportConfig;
  page?: number;
  limit?: number;
  sortBy?: string;
  sortDir?: string;
}

export interface ReportSummaryTotals {
  totalLiens: number;
  totalPurchaseAmt: number;
  totalBillingAmt: number;
  totalAmtToSettle: number;
  totalReturnedAmt: number;
  totalGrossProfit: number;
  avgRoi: number;
  totalOpenCases: number;
  totalClosedCases: number;
  totalOpenLiens: number;
  totalClosedLiens: number;
}

export interface ReportRow {
  plaintiff_first_name: string;
  plaintiff_last_name: string;
  case_id: string;
  lien_id: string;
  purchase_amt: string;
  billing_amt: string;
  returned_amt: string;
  case_status: string;
  date_of_loss: string;
  id: string;
  l_id: string;
  [key: string]: string;
}

export interface RunReportResult {
  isSuccess: boolean;
  message: string;
  summaryTotals: ReportSummaryTotals;
  data: ReportRow[];
  page: number;
  limit: number;
  totalCount: number;
}
