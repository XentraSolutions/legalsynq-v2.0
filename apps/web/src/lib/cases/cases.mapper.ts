import { DocumentTypeResponse } from "../lookup/lookup.types";
import type {
  CaseResponseDto,
  CaseListItem,
  CaseDetail,
  CaseLienItem,
  LienResponseDto,
  PaginatedResultDto,
  PaginationMeta,
  UpdateCaseRequestDto,
  CreateMedicalLiensResponse,
  MedicalCodeLiensResponse,
  CaseDocuments,
  CaseDocument,
} from "./cases.types";

const CASE_STATUS_LABELS: Record<string, string> = {
  PreDemand: "Pre-Demand",
  DemandSent: "Demand Sent",
  InNegotiation: "In Negotiation",
  CaseSettled: "Case Settled",
  Closed: "Closed",
};

function safeString(val: string | null | undefined): string {
  return val ?? "";
}

export function formatDateField(val: string | null | undefined): string {
  if (!val) return "";
  try {
    const d = new Date(val);
    if (isNaN(d.getTime())) return val;
    return d.toLocaleDateString("en-US", {
      month: "short",
      day: "numeric",
      year: "numeric",
      timeZone: "UTC",
    });
  } catch {
    return val;
  }
}
export const dateConverter = (dateData: string) => {
  if (!dateData) return "";

  const date = new Date(dateData);

  // Format the date using the US locale to automatically get MM/DD/YYYY
  const formatter = new Intl.DateTimeFormat("en-US", {
    month: "2-digit",
    day: "2-digit",
    year: "numeric",
  });

  const formattedDate = formatter.format(date);
  return formattedDate;
};

export const dateConvertertoIso = (dateData: string) => {
  if (!dateData) return "";
  const d = new Date(dateData);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
};

export function mapCaseToListItem(dto: CaseResponseDto): CaseListItem {
  return {
    id: dto.id,
    caseId: dto.id,
    caseNumber: dto.caseNumber,
    clientName:
      dto.clientDisplayName ||
      `${dto.clientFirstName} ${dto.clientLastName}`.trim(),
    title: safeString(dto.title || dto.externalReference),
    status: dto.status,
    statusLabel: CASE_STATUS_LABELS[dto.status] ?? dto.status,
    lawFirm: safeString((dto as any).lawFirm),
    caseManager: safeString((dto as any).caseManager),
    accidentType: safeString((dto as any).accidentType),
    dateOfIncident: formatDateField(dto.dateOfIncident),
    clientDob: formatDateField(dto.clientDob),
    insuranceCarrier: safeString(dto.insuranceCarrier),
    demandAmount: dto.demandAmount ?? null,
    settlementAmount: dto.settlementAmount ?? null,
    createdAt: formatDateField(dto.createdAtUtc),
    updatedAt: formatDateField(dto.updatedAtUtc),
  };
}

export function mapCaseToDetail(dto: CaseResponseDto): CaseDetail {
  return {
    id: dto.id,
    caseNumber: dto.caseNumber,
    externalReference: safeString(dto.externalReference),
    title: safeString(dto.title),
    clientName:
      dto.clientDisplayName ||
      `${dto.clientFirstName} ${dto.clientLastName}`.trim(),
    clientFirstName: dto.clientFirstName,
    clientLastName: dto.clientLastName,
    status: dto.status,
    statusLabel: CASE_STATUS_LABELS[dto.status] ?? dto.status,
    dateOfIncident: formatDateField(dto.dateOfIncident),
    clientDob: formatDateField(dto.clientDob),
    clientPhone: safeString(dto.clientPhone),
    clientEmail: safeString(dto.clientEmail),
    clientAddress: safeString(dto.clientAddress),
    clientStreetAddress: safeString(dto.clientStreetAddress),
    clientCity: safeString(dto.clientCity),
    clientState: safeString(dto.clientState),
    clientZipcode: safeString(dto.clientZipcode),
    sex: safeString(dto.sex),
    caseType: safeString(dto.caseType),
    currentMedicalStatus: safeString(dto.currentMedicalStatus),
    stateOfIncident: safeString(dto.stateOfIncident),
    trackingFollowUpDate: formatDateField(dto.trackingFollowUpDate),
    trackingFollowUp: safeString(
      dto.trackingFollowUp ?? dto.trackingFollowUpDate,
    ),
    leadId: safeString(dto.leadId),
    insuranceCarrier: safeString(dto.insuranceCarrier),
    policyNumber: safeString(dto.policyNumber),
    claimNumber: safeString(dto.claimNumber),
    demandAmount: dto.demandAmount ?? null,
    settlementAmount: dto.settlementAmount ?? null,
    description: safeString(dto.description),
    notes: safeString(dto.notes),
    openedAt: formatDateField(dto.openedAtUtc),
    closedAt: formatDateField(dto.closedAtUtc),
    createdAt: formatDateField(dto.createdAtUtc),
    updatedAt: formatDateField(dto.updatedAtUtc),
  };
}

export function mapLienToListItem(dto: LienResponseDto): CaseLienItem {
  return {
    id: dto.id,
    lienNumber: dto.lienNumber,
    lienType: dto.lienType,
    status: dto.status,
    originalAmount: dto.originalAmount,
    facility: dto.facility ?? "",
    facilityName: dto.facilityName ?? dto.facility ?? "",
    serviceDate: dto.serviceDate ?? "",
    purchaseDate: dto.purchaseDate ?? "",
    purchaseAmount: dto.purchaseAmount ?? dto.purchasePrice ?? 0,
  };
}

export function mapDtoToUpdateRequest(
  dto: CaseResponseDto,
): UpdateCaseRequestDto {
  return {
    caseId: dto.id,
    currentStatus: dto.status,
    currentMedicalStatus: dto.currentMedicalStatus ?? "",
    caseType: dto.caseType ?? "",
    stateOfIncident: dto.stateOfIncident ?? "",
    trackingFollowUp: safeString(
      dto.trackingFollowUp ?? dto.trackingFollowUpDate,
    ),
    dateOfLoss: dto.dateOfIncident ?? "",
    leadId: dto.leadId ?? "",
    clientFirstName: dto.clientFirstName,
    clientLastName: dto.clientLastName,
    externalReference: dto.externalReference ?? undefined,
    title: dto.title ?? undefined,
    clientDob: dto.clientDob ?? undefined,
    clientPhone: dto.clientPhone ?? undefined,
    clientEmail: dto.clientEmail ?? undefined,
    clientAddress: dto.clientAddress ?? undefined,
    dateOfIncident: dto.dateOfIncident ?? undefined,
    insuranceCarrier: dto.insuranceCarrier ?? undefined,
    policyNumber: dto.policyNumber ?? undefined,
    claimNumber: dto.claimNumber ?? undefined,
    description: dto.description ?? null,
    notes: dto.notes ?? null,
    status: dto.status,
    demandAmount: dto.demandAmount ?? null,
    settlementAmount: dto.settlementAmount ?? null,
  };
}

export function mapPagination<T>(
  result: PaginatedResultDto<T>,
): PaginationMeta {
  return {
    page: result.page,
    pageSize: result.pageSize,
    totalCount: result.totalCount,
    totalPages: Math.ceil(result.totalCount / Math.max(result.pageSize, 1)),
  };
}

export function mapMedicalInfo(
  result: CreateMedicalLiensResponse,
): CreateMedicalLiensResponse {
  return {
    id: result.id,
    caseId: result.caseId,
    status: result.status,
    purchaseDate: dateConvertertoIso(result.purchaseDate),
    initialServiceDate: dateConvertertoIso(result.initialServiceDate),
    endServiceDate: result.endServiceDate
      ? dateConvertertoIso(result.endServiceDate)
      : "",
    note: result.note,
    isBulk: result.isBulk == "Yes" ? "true" : "false",
    isServicing: result.isBulk == "Yes" ? "true" : "false",
    fundingCompany: result.fundingCompany,
    fundingCompanyId: result.fundingCompanyId,
  };
}

export function mapMedicalCodes(result: MedicalCodeLiensResponse[]): {
  codeRows: MedicalCodeLiensResponse[];
} {
  return {
    codeRows: result.map((r) => ({
      ...r,
      billingAmount: +r.billingAmount,
      medicareCost: +r.medicareCost,
      purchaseAmount: +r.purchaseAmount,
    })),
  };
}

function getDocumentTypeById(id: string, docs: DocumentTypeResponse[]) {
  const doc = docs.find((d) => d.id == id);
  return doc?.name ?? "";
}

export function mapDocuments(
  result: any,
  cat: DocumentTypeResponse[],
): CaseDocuments {
  let liens: CaseDocument[] = [];
  let cases: CaseDocument[] = [];

  (result.data || []).map((data: CaseDocument) => {
    if (data.liensId) {
      liens.push(data);
      data.documentType = getDocumentTypeById(data.typeId, cat);
    } else {
      data.documentType = getDocumentTypeById(data.typeId, cat);
      cases.push(data);
    }
  });
  return {
    caseDocuments: cases,
    liensDocuments: liens,
  };
}
