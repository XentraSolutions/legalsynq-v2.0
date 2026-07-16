import { ApiResponse } from "@/types";
import { CaseLienItem } from "../cases";
import {
  ColumnGroup,
  Columns,
  ReportConfigResponse,
  ReportListResponse,
  ReportsResponse,
} from "./lien-report.types";

function formatDateField(val: string | null | undefined): string {
  if (!val) return "";
  try {
    const d = new Date(val);
    if (isNaN(d.getTime())) return val;
    return d.toLocaleDateString("en-US", {
      month: "short",
      day: "numeric",
      year: "numeric",
    });
  } catch {
    return val;
  }
}
export interface ReportListItem {
  id: string;
  name: string;
  description?: string | null | undefined;
  createdAt: string;
  updatedAt?: string;
  config: Record<string, unknown>;
}

export interface ReportTemplate {
  reportType: string;
  statusView: string;
  lienStatusIds: string | string[];
  purchaseDateFrom: string | null;
  purchaseDateTo: string | null;
  closedDateFrom: string | null;
  closedDateTo: string | null;
  isBulk: string;
  plaintiffCaseIds: string | string[];
  lawFirmIds: string | string[];
  attorneyIds: string | string[];
  fundingCompanyIds: string | string[];
  medicalFacilityIds: string | string[];
  caseManagerIds: string | string[];
  medicalProviderIds: string | string[];
  columns: Array<unknown>;
  page: string;
  limit: string;
  billingAmt: string;
  caseId: string;
  caseManager: string;
  caseStatus: string;
  caseType: string;
  dateClosed: string;
  dateOfIncident: string;
  id: number;
  lawfirmId: number;
  lawfirm: string;
  lienId: string;
  serviceDate: string;
  facility: string;
  firtsName: string;
  lastName: string;
  purchaseAmt: string;
  returnedAmt: string | null;
  caseNumber: string;
  clientName: string;
  status: string;
}

export function mapReportToTemplate(dto: ReportsResponse): ReportTemplate {
  return {
    billingAmt: dto.billing_amt,
    caseId: dto.case_id,
    caseNumber: dto.case_id,
    caseManager: dto.case_manager,
    caseStatus: dto.case_status,
    status: dto.case_status,
    caseType: dto.case_type,
    dateClosed: dto.date_closed,
    dateOfIncident: dto.date_of_loss,
    id: dto.id,
    serviceDate: dto.initial_service_date,
    lawfirmId: dto.l_id,
    lawfirm: dto.lawfirm,
    lienId: dto.lien_id,
    facility: dto.medical_facility,
    firtsName: dto.plaintiff_first_name,
    lastName: dto.plaintiff_last_name,
    purchaseAmt: dto.purchase_amt,
    purchaseDateFrom: dto.purchase_date ?? null,
    purchaseDateTo: dto.purchase_date ?? null,
    closedDateFrom: dto.date_closed ?? null,
    closedDateTo: dto.date_closed ?? null,
    statusView: dto.case_status ?? "ALL",
    lienStatusIds: [],
    isBulk: "N",
    plaintiffCaseIds: [],
    lawFirmIds: [],
    attorneyIds: [],
    fundingCompanyIds: [],
    medicalFacilityIds: [],
    caseManagerIds: [],
    medicalProviderIds: [],
    columns: [],
    page: "1",
    limit: "50",
    returnedAmt: dto.returned_amt,
    clientName: `${dto.plaintiff_first_name} ${dto.plaintiff_last_name}`.trim(),
    reportType: dto.reportType,
  };
}

export function mapReportToListItem(dto: ReportConfigResponse): ReportListItem {
  return {
    id: dto.id,
    name: dto.name,
    description: dto.description ?? dto.reportDescription ?? null,
    createdAt: formatDateField(dto.createdAtUtc),
    updatedAt: formatDateField(dto.updatedAtUtc),
    config: dto.config,
  };
}

// grouped columns
export function mapAllColumns(viewBy: string, dto: any[]): any[] {
  const excludedKeys = new Set([
    "isSuccess",
    "message",
    "reportType",
    "data",
    "defaultColumn",
  ]);

  const categories: ColumnGroup[] = Object.entries(dto)
    .filter(([key]) => !excludedKeys.has(key))
    .map(([key, value]) => ({
      key,
      value: value as Columns[],
    }));

  return categories;
}
