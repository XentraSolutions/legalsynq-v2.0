export interface CaseResponseDto {
  id: string;
  caseNumber: string;
  externalReference?: string | null;
  title?: string | null;
  clientFirstName: string;
  clientLastName: string;
  clientDisplayName: string;
  status: string;
  dateOfIncident?: string | null;
  clientDob?: string | null;
  clientPhone?: string | null;
  clientEmail?: string | null;
  clientAddress?: string | null;
  insuranceCarrier?: string | null;
  policyNumber?: string | null;
  claimNumber?: string | null;
  demandAmount?: number | null;
  settlementAmount?: number | null;
  description?: string | null;
  notes?: string | null;
  openedAtUtc?: string | null;
  closedAtUtc?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface PaginatedResultDto<T> {
  items?: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CreateCaseRequestDto {
  caseNumber: string;
  firstname: string;
  lastname: string;
  externalReference?: string;
  title?: string;
  clientDob?: string;
  clientPhone?: string;
  clientEmail?: string;
  clientAddress?: string;
  dateOfIncident?: string;
  insuranceCarrier?: string;
  policyNumber?: string;
  claimNumber?: string;
  description?: string;
  notes?: string;
}

export interface UpdateCaseRequestDto {
  clientFirstName: string;
  clientLastName: string;
  externalReference?: string;
  title?: string;
  clientDob?: string;
  clientPhone?: string;
  clientEmail?: string;
  clientAddress?: string;
  dateOfIncident?: string;
  insuranceCarrier?: string;
  policyNumber?: string;
  claimNumber?: string;
  description?: string;
  notes?: string;
  status?: string;
  demandAmount?: number;
  settlementAmount?: number;
}

export interface CasesQuery {
  page?: number;
  limit?: number;
  lawFirmId?: string | null;
  accidentTypeId?: string | null;
  statusId?: number | string;
  caseManagerId?: string | null;
  keyword?: string | null;
  sortBy?: string | null;
  sortDirection?: string | null;
}

export interface LienResponseDto {
  id: string;
  lienNumber: string;
  lienType: string;
  status: string;
  caseId?: string | null;
  originalAmount: number;
  currentBalance?: number | null;
  offerPrice?: number | null;
  purchasePrice?: number | null;
  jurisdiction?: string | null;
  isConfidential: boolean;
  subjectDisplayName?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CaseListItem {
  id: string;
  caseId: string;
  caseNumber: string;
  clientName: string;
  title: string;
  status: string;
  statusLabel: string;
  lawFirm: string;
  caseManager: string;
  accidentType: string;
  dateOfIncident: string;
  clientDob: string;
  insuranceCarrier: string;
  demandAmount: number | null;
  settlementAmount: number | null;
  createdAt: string;
  updatedAt: string;
}

export interface CaseDetail {
  id: string;
  caseNumber: string;
  externalReference: string;
  title: string;
  clientName: string;
  clientFirstName: string;
  clientLastName: string;
  status: string;
  statusLabel: string;
  dateOfIncident: string;
  clientDob: string;
  clientPhone: string;
  clientEmail: string;
  clientAddress: string;
  insuranceCarrier: string;
  policyNumber: string;
  claimNumber: string;
  demandAmount: number | null;
  settlementAmount: number | null;
  description: string;
  notes: string;
  openedAt: string;
  closedAt: string;
  createdAt: string;
  updatedAt: string;
}

export interface CaseLienItem {
  id: string;
  lienNumber: string;
  lienType: string;
  status: string;
  originalAmount: number;
}

export interface CaseUpdatesItem {
  id: string;
  timestamp: string;
  action: string;
  description: string;
  updatedBy: string;
}

export interface CaseUpdatesDto {
  id: string;
  timestamp: string;
  action: string;
  description: string;
  updatedBy: string;
}

export interface CaseStatusResponse {
  category: string;
  code: string;
  description: string;
  id: string;
  isActive: boolean;
  isSystem: boolean;
  name: string;
  sortOrder: number;
}
export interface SearchMeta {
  page: number;
  limit: number;
  lawFirmId: string | null;
  accidentTypeId: string | null;
  statusId: number;
  caseManagerId: string | null;
  keyword: string | null;
  sortBy: string | null;
  sortDirection: string | null;
}

export interface PaginationMeta {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface DashboardStats {
  totalActiveCases: number;
  totalCases: number;
  totalLienValue: number;
  totalLiens: number;
  caseStatus: StatusData[];
  lienStatus: StatusData[];
}

interface StatusData {
  label: string;
  value: number;
}
export interface CasePaginatedParams {
  CaseId: string;
  page: number;
  limit: number;
}

export interface CasePaginatedResult {
  data: CaseResponseDto[];
  pagination?: PaginationMeta;
  limit: number;
  page: number;
  totalCount: number;
}

export interface CaseListResult {
  items: CaseListItem[];
  pagination: PaginationMeta;
}

export interface CaseLiensResult {
  items: CaseLienItem[];
  pagination?: PaginationMeta;
}

export interface PaginatedWithLimitResultDto<T> {
  items: T[];
  page: number;
  limit: number;
  totalCount: number;
}

export type CaseLiensApiResponse = PaginatedWithLimitResultDto<LienResponseDto>;

export interface CasesFilters {
  caseId: string | null;
  keyword: string;
  lawFirmId: string | null;
  accidentTypeId: string | null;
  statusId: string | null;
  caseManagerId: string | null;
}

export interface ExportResponse {
  data: Array<{ base64: string; export_format: string; filename: string }>;
}
