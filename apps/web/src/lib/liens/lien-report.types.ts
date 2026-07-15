import { CaseListItem } from "../cases";
import { ReportListItem } from "./lien-reports.mapper";

export interface ReportColumnOption {
  key: string;
  label: string;
}

export type ReportColumnValue = string | ReportColumnOption;

export interface CreateReports {
  name: string;
  description: string;
  config: {
    columns: Array<ReportColumnValue>;
  };
  attorneyIds: Array<string>;
  caseManagerIds: Array<string>;
  closedDateFrom?: null;
  closedDateTo?: null;
  fundingCompanyIds: Array<string>;
  isBulk: string;
  lawFirmIds: Array<string>;
  lienStatusIds: Array<string>;
  medicalFacilityIds: Array<string>;
  medicalProviderIds: Array<string>;
  plaintiffCaseIds: Array<string>;
  purchaseDateFrom: string;
  purchaseDateTo: string;
  reportType: string;
  statusView: string;
}

export interface ReportListResponse {
  name?: string;
  description?: string;
  columns?: Array<string>;
  data?: ReportsResponse[];
  items: Array<unknown>;
  page?: number;
  pageSize?: number;
  reportId?: string;
  totalCount?: number;
  reportName?: string;
  reportDescription?: string;
  reportType?: string;
  config?: ReportConfig;
  limit?: number;
  exportCsv?: false;
  isBoldReady?: false;
  program?: null;
  user?: null;
  createdAt?: string;
  createdBy?: string;
  updatedAt?: string;
  reportConfig?: ReportConfig;
  columnCount?: number;
  summaryTotals?: ReportTotals;
}

export interface ReportConfigResponse {
  config: {
    columns: Array<ReportColumnValue>;
  };

  createdAt: string;
  createdAtUtc: string;
  createdBy: string;
  id: string;
  name: string;
  reportDescription: string | undefined | null;
  description?: string | null;
  reportConfig: {
    columns: Array<ReportColumnValue>;
  };
  reportId: string;
  reportName: string;
  reportType: string;
  updatedAt: string;
  tenantId: string;
  updatedAtUtc: string;
  userId: string;
}

export interface ReportTemplate {
  name?: string;
  description?: string;
  reportType: string;
  statusView: string;
  lienStatusIds: Array<string>;
  purchaseDateFrom: string | null;
  purchaseDateTo: string | null;
  closedDateFrom: string | null;
  closedDateTo: string | null;
  isBulk: string;
  plaintiffCaseIds: Array<string>;
  lawFirmIds: Array<string>;
  attorneyIds: Array<string>;
  fundingCompanyIds: Array<string>;
  medicalFacilityIds: Array<string>;
  caseManagerIds: Array<string>;
  medicalProviderIds: Array<string>;
  columns: Array<string>;
  page: string;
  limit: string;
}
export interface ReportsResponse {
  billing_amt: string;
  case_id: string;
  case_manager: string;
  case_status: string;
  case_type: string;
  date_closed: any;
  date_of_loss: string;
  id: number;
  initial_service_date: string;
  l_id: number;
  lawfirm: string;
  lien_id: string;
  medical_facility: string;
  plaintiff_first_name: string;
  plaintiff_last_name: string;
  purchase_amt: string;
  purchase_date: string;
  returned_amt: null;
  reportConfig?: ReportConfig;
  summaryTotals?: ReportTotals;
  reportType: string;
}

interface ReportConfig {
  reportType: string;
  statusView: string;
  lienStatusIds: Array<string>;
  purchaseDateFrom: string;
  purchaseDateTo: string;
  closedDateFrom: string;
  closedDateTo: string;
  isBulk: string;
  plaintiffCaseIds: Array<string>;
  lawFirmIds: Array<string>;
  attorneyIds: Array<string>;
  fundingCompanyIds: Array<string>;
  medicalFacilityIds: Array<string>;
  caseManagerIds: Array<string>;
  medicalProviderIds: Array<string>;
  columns: Array<ReportColumnValue>;
  page: number | string;
  limit: number | string;
}

export interface ReportTotals {
  totalCases: number;
  totalLiens: number;
  totalPurchaseAmt: number;
  totalBillingAmt: number;
  totalAmtToSettle: number;
  totalReturnedAmt: number;
  totalOpenCases: number;
  totalClosedCases: number;
  totalOpenLiens: number;
  totalClosedLiens: number;
}

export interface UpdateReportConfigRequest {
  name: string;
  config: Record<string, unknown>;
}

export interface ExportReportRequest {
  reportId: string;
  filters: Record<string, unknown>;
  columns: Array<unknown>;
  format: string;
}

export interface ApiResponse {
  isSuccess: boolean;
  message: string;
  data: Array<Record<string, unknown>>;
}
