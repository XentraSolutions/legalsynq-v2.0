import { LienDetailsResult } from "@/types/lien-selling";
import { formatLegacyDateOnly } from "../format-date";
import type {
  LienResponseDto,
  LienOfferResponseDto,
  LienListItem,
  LienDetail,
  LienOfferItem,
  PaginatedResultDto,
  PaginationMeta,
  UpdateLienRequestDto,
} from "./liens.types";

const LIEN_TYPE_LABELS: Record<string, string> = {
  MedicalLien: "Medical Lien",
  AttorneyLien: "Attorney Lien",
  SettlementAdvance: "Settlement Advance",
  WorkersCompLien: "Workers' Comp Lien",
  PropertyLien: "Property Lien",
  Other: "Other",
};

function safeString(val: string | null | undefined): string {
  return val ?? "";
}

function formatDateField(val: string | null | undefined): string {
  if (!val) return "";
  try {
    return formatLegacyDateOnly(val);
  } catch {
    return val;
  }
}

export function mapOfferToItem(dto: LienOfferResponseDto): LienOfferItem {
  return {
    id: dto.id,
    lienId: dto.lienId,
    offerAmount: dto.offerAmount,
    status: dto.status,
    buyerOrgId: dto.buyerOrgId,
    sellerOrgId: dto.sellerOrgId,
    notes: safeString(dto.notes),
    responseNotes: safeString(dto.responseNotes),
    offeredAt: formatDateField(dto.offeredAtUtc),
    expiresAt: formatDateField(dto.expiresAtUtc),
    respondedAt: formatDateField(dto.respondedAtUtc),
    isExpired: dto.isExpired,
    createdAt: formatDateField(dto.createdAtUtc),
  };
}

export function mapDtoToUpdateRequest(
  dto: LienResponseDto,
): UpdateLienRequestDto {
  return {
    externalReference: dto.externalReference ?? undefined,
    lienType: dto.lienType,
    caseId: dto.caseId ?? undefined,
    facilityId: dto.facilityId ?? undefined,
    originalAmount: dto.originalAmount,
    jurisdiction: dto.jurisdiction ?? undefined,
    isConfidential: dto.isConfidential,
    subjectFirstName: dto.subjectFirstName ?? undefined,
    subjectLastName: dto.subjectLastName ?? undefined,
    incidentDate: dto.incidentDate ?? undefined,
    description: dto.description ?? undefined,
  };
}

export function mapLienItem(dto: LienListItem): LienListItem {
  return {
    ...dto,
    askAmount:
      typeof dto.askAmount == null || typeof dto.askAmount == "object"
        ? 0
        : dto.askAmount,
    billingAmount:
      typeof dto.billingAmount == null || typeof dto.billingAmount == "object"
        ? 0
        : dto.billingAmount,
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
