import { CaseLienItem } from "../cases";
import {
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
  lienStatusIds: string;
  purchaseDateFrom: string;
  purchaseDateTo: string;
  closedDateFrom: string;
  closedDateTo: string;
  isBulk: string;
  plaintiffCaseIds: string;
  lawFirmIds: string;
  attorneyIds: string;
  fundingCompanyIds: string;
  medicalFacilityIds: string;
  caseManagerIds: string;
  medicalProviderIds: string;
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
    purchaseDateFrom: dto.purchase_date,
    returnedAmt: dto.returned_amt,
    clientName: dto.plaintiff_first_name + dto.plaintiff_last_name,
    reportType: dto.reportType,
  };
}

export function mapReportToListItem(dto: ReportConfigResponse): ReportListItem {
  return {
    id: dto.id,
    name: dto.name,
    description: dto.description,
    createdAt: formatDateField(dto.createdAtUtc),
    updatedAt: formatDateField(dto.updatedAtUtc),
    config: dto.config,
  };
}
