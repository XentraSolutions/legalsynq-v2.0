import { formatLegacyDateOnly } from "../format-date";
import { GenericPaginatedResult } from "../lookup/lookup.types";
import { ServicingListResult } from "./servicing.service";
import type {
  ServicingItemResponseDto,
  ServicingListItem,
  ServicingDetail,
  PaginatedResultDto,
  PaginationMeta,
  ServicingListItemResponseDto,
} from "./servicing.types";

type ServicingListLikeDto = {
  caseId?: string | null;
  caseCode?: string | null;
  plaintiffName?: string | null;
  lawfirm?: string | null;
  status?: string | null;
  settlementStatus?: string | null;
  settlementDate?: string | null;
  settlementAmount?: number | null;
  billingAmount?: number | null;
  purchaseAmount?: number | null;
  description?: string | null;
  taskNumber?: string | null;
  assignedTo?: string | null;
  assignedToUserId?: string | null;
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

export function mapServicingToListItem(
  dto: ServicingListLikeDto,
): ServicingListItem {
  const formattedSettlementDate = formatDateField(dto.settlementDate);

  return {
    id: dto.caseId ?? undefined,
    caseId: safeString(dto.caseId),
    caseCode: safeString(dto.caseCode),
    name: safeString(dto.plaintiffName ?? dto.description ?? dto.taskNumber),
    lawfirm: safeString(dto.lawfirm),
    currentStatus: safeString(dto.status),
    settlementStatus: dto.settlementStatus
      ? safeString(dto.settlementStatus)
      : "---",
    settlementDate: dto.settlementDate ? safeString(dto.settlementDate) : "---",
    settlementAmount: dto.settlementAmount ?? 0,
    billingAmount: Number(dto.billingAmount) ?? 0, //dto.billingAmount ?? 0,
    purchaseAmount: dto.purchaseAmount ?? 0,
  };
}

export function mapServicingToDetail(
  dto: ServicingItemResponseDto,
): ServicingDetail {
  return {
    ...mapServicingToListItem(dto),
    assignedToUserId: safeString(dto.assignedToUserId),
    taskNumber: safeString(dto.taskNumber),
    taskType: safeString(dto.taskType),
    description: safeString(dto.description),
    status: safeString(dto.status),
    priority: safeString(dto.priority),
    assignedTo: safeString(dto.assignedTo),
    dueDate: dto.dueDate ?? null,
    notes: dto.notes ?? null,
    resolution: dto.resolution ?? null,
    startedAt: dto.startedAtUtc ?? null,
    completedAt: dto.completedAtUtc ?? null,
    escalatedAt: dto.escalatedAtUtc ?? null,
    createdAt: safeString(dto.createdAtUtc),
    updatedAt: safeString(dto.updatedAtUtc),
    lienId: dto.lienId ?? null,
  };
}

export function mapServicingPagination<T>(
  result: GenericPaginatedResult<T>,
): PaginationMeta {
  return {
    page: result.page,
    pageSize: result.limit,
    totalCount: result.totalCount,
    totalPages: Math.ceil(result.totalCount / Math.max(result.limit, 1)),
  };
}
