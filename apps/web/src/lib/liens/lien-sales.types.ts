export interface PaginatedResultDto<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface SellingPortfolioDto {
  id: string;
  tenantId: string;
  sellerOrgId: string;
  portfolioNumber: string;
  name: string;
  description?: string | null;
  internalNotes?: string | null;
  targetGrouping?: string | null;
  status: string;
  lienCount: number;
  originalAmountTotal: number;
  currentBalanceTotal?: number | null;
  offerPriceTotal?: number | null;
  publishedAtUtc?: string | null;
  closedAtUtc?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  liens: SellingPortfolioLienDto[];
  buyers: SellingPortfolioBuyerDto[];
}

export interface SellingPortfolioLienDto {
  id: string;
  portfolioId: string;
  lienId: string;
  lienNumber: string;
  lienExternalId?: string | null;
  caseId?: string | null;
  caseExternalId?: string | null;
  facilityId?: string | null;
  lienType: string;
  lienLifecycleStatus: string;
  originalAmount: number;
  currentBalance?: number | null;
  offerPrice?: number | null;
  purchasePrice?: number | null;
  payoffAmount?: number | null;
  subjectFirstName?: string | null;
  subjectLastName?: string | null;
  jurisdiction?: string | null;
  incidentDate?: string | null;
  description?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface SellingPortfolioBuyerDto {
  id: string;
  portfolioId: string;
  buyerOrgId: string;
  createdAtUtc: string;
}

export interface SellingPortfolioActivityDto {
  id: string;
  portfolioId: string;
  action: string;
  entityType: string;
  entityId?: string | null;
  actorUserId: string;
  occurredAtUtc: string;
  summary: string;
  metadataJson?: string | null;
}

export interface SellingPortfolioStatusHistoryDto {
  id: string;
  portfolioId: string;
  fromStatus?: string | null;
  toStatus: string;
  changedByUserId: string;
  changedAtUtc: string;
  notes?: string | null;
}

export interface SellingPortfolioAnalyticsDto {
  portfolioId: string;
  financial: {
    totalReceivables: number;
    totalOutstandingBalance: number;
    settlementExposure: number;
    paymentTotal: number;
    averageLienBalance: number;
  };
  agingBuckets: Array<{
    bucket: string;
    lienCount: number;
    outstandingBalance: number;
  }>;
  operational: {
    lienCount: number;
    status: string;
    publishedAtUtc?: string | null;
    closedAtUtc?: string | null;
    activityCount: number;
  };
  concentrations: Array<{
    dimension: string;
    value: string;
    lienCount: number;
    outstandingBalance: number;
  }>;
}

export interface CreateSellingPortfolioRequestDto {
  portfolioNumber: string;
  name: string;
  description?: string;
  internalNotes?: string;
  targetGrouping?: string;
  lienIds?: string[];
  buyerOrgIds?: string[];
}

export interface UpdateSellingPortfolioRequestDto {
  name: string;
  description?: string;
  internalNotes?: string;
  targetGrouping?: string;
}

export interface AddSellingPortfolioLiensRequestDto {
  lienIds?: string[];
  lienCodes?: string[];
  liens?: string[];
}

export interface AddSellingPortfolioBuyersRequestDto {
  buyerOrgIds: string[];
}

export interface TransitionSellingPortfolioStatusRequestDto {
  status: string;
  notes?: string;
}

export interface SendLienBuyerEmailRequestDto {
  buyerContactId: string;
  detailsUrl: string;
}

export interface SendLienBuyerEmailResponseDto {
  success: boolean;
  notificationId?: string | null;
  notificationStatus: string;
  lienId: string;
  lienCode: string;
  buyerContactId: string;
  buyerOrgId: string;
  buyerName: string;
  buyerEmail: string;
  subject: string;
  body: string;
}

export interface SellingPortfolioQuery {
  search?: string;
  status?: string;
  buyerOrgId?: string;
  page?: number;
  pageSize?: number;
}
