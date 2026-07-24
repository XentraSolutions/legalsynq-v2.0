export interface CaseResponseDto {
  id: string;
  caseNumber: string;
  externalReference?: string | null;
  title?: string | null;
  clientFirstName: string;
  clientLastName: string;
  clientDisplayName: string;
  trackingFollowUp?: string | null;
  status: string;
  statusLabel?: string | null;
  dateOfIncident?: string | null;
  clientDob?: string | null;
  clientPhone?: string | null;
  clientEmail?: string | null;
  clientAddress?: string | null;
  clientStreetAddress: string;
  clientCity: string;
  clientState: string;
  clientZipcode: string;
  sex: string;
  caseType: string;
  currentMedicalStatus: string;
  stateOfIncident: string;
  trackingFollowUpDate: string;
  leadId: string;
  lawFirmId?: string | null;
  lawFirm?: string | null;
  caseManagerId?: string | null;
  caseManager?: string | null;
  accidentTypeId?: string | null;
  accidentType?: string | null;
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
  shareCase?: string;
  minorComp?: string;
  caseDropped?: string;
  childSupportLiens?: string;
  isUccFiled?: string;
}

export interface PaginatedResultDto<T> {
  items?: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CaseDocuments {
  caseDocuments: CaseDocument[];
  liensDocuments: CaseDocument[];
}

export interface CaseDocument {
  id: string;
  liensId: string;
  caseId: string;
  url: string;
  created: string;
  mimeType: string;
  documentType: string;
  [key: string]: any;
}

export interface CreateCaseRequestDto {
  caseNumber?: string;
  firstname: string;
  lastname: string;
  externalReference?: string;
  title?: string;
  dob?: string;
  phone?: string;
  email?: string;
  address?: string;
  city?: string;
  state?: string;
  zipcode?: string;
  dateOfLoss?: string;
  insuranceCarrier?: string;
  policyNumber?: string;
  claimNumber?: string;
  description?: string;
  notes?: string;
  caseStatusId: string | null;
  caseManagerId?: string | null;
  lawfirmId?: string | null;
  stateId?: string | null;
  accidentTypeId?: string | null;
  accidentStateId?: string | null;
  isServicing?: boolean;
  dateOfIncident?: string;
  caseType?: string;
  stateOfIncident?: string;
  minorComp: string;
}

export interface UpdateCaseRequestDto {
  caseId: string;
  currentStatus: string;
  currentMedicalStatus: string;
  caseType: string;
  stateOfIncident: string;
  trackingFollowUp: string;
  dateOfLoss: string;
  leadId: string;
  shareCase: string;
  minorComp: string;
  caseDropped: string;
  childSupportLiens: string;
  description?: string | null;
  notes: string | null;
  demandAmount: number | string | null;
  settlementAmount: number | string | null;
  clientFirstName?: string;
  clientLastName?: string;
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
  status?: string;
  isUccFiled?: string;
}

export interface UpdateCaseDetailsRequestDto {
  firstname: string;
  lastname: string;

  externalReference?: string;
  title?: string;
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

export interface UpdateCasePersonalRequestDto {
  caseId: string;
  firstName: string;
  lastName: string;
  sex: string;
  dob?: string;
  phone?: string;
  email?: string;
  address?: string;
  city?: string;
  state?: string;
  zipcode?: string;
}

export interface CasesQuery {
  search?: string | null;
  pageSize?: number;
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
  facility?: string | null;
  facilityName?: string | null;
  serviceDate?: string | null;
  purchaseDate?: string | null;
  purchaseAmount?: number | null;
  originalAmount: number;
  currentBalance?: number | null;
  offerPrice?: number | null;
  purchasePrice?: number | null;
  totalPurchase?: number | null;
  totalBilling?: number | null;
  isServicing?: string | boolean | null;
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
  clientDisplayName?: string;
}

export interface CaseDetail {
  id: string;
  caseType: string;
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
  clientStreetAddress: string;
  clientCity: string;
  clientState: string;
  clientZipcode: string;
  sex: string;
  currentMedicalStatus: string;
  stateOfIncident: string;
  trackingFollowUpDate: string;
  leadId: string;
  insuranceCarrier: string;
  policyNumber: string;
  claimNumber: string;
  demandAmount: number | null;
  settlementAmount: number | null;
  description: string | null;
  notes: string | null;
  trackingFollowUp: string;
  openedAt: string;
  closedAt: string;
  createdAt: string;
  updatedAt: string;
  lawFirm: string;
  lawFirmId: string;
  caseManager: string;
  caseManagerId: string;
  accidentType: string;
  shareCase: string;
  minorComp: string;
  caseDropped: string;
  childSupportLiens: string;
  isUccFiled: string;
}

export interface CaseFlagTypes {
  shareCase: string;
  minorComp: string;
  caseDropped: string;
  childSupportLiens: string;
  isUccFiled: string;
}

export interface CaseLienItem {
  id: string;
  lienNumber: string;
  lienType: string;
  status: string;
  originalAmount: number;
  facility: string;
  facilityName: string;
  serviceDate: string;
  purchaseDate: string;
  purchaseAmount: number;
  isServicing: boolean;
}

export interface CaseLienItemMetadata {
  closedAtUtc: string | null;
  reductionAmount: number | null;
  paymentAmount: number | null;
  balance: number;
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

// Actual shape returned by the per-contact-type case lookups
// (cases/law/v3, cases/leads/v3, cases/medical/v3, cases/medical/facility/v3,
// cases/funding/v3). `data` holds only the requested page; `totalCount`
// reflects the full keyword-filtered result set.
export interface CaseListApiResponse {
  isSuccess: boolean;
  message: string;
  data: CaseResponseDto[];
  totalCount: number;
  totalCases: number;
  totalActiveCases: number;
  totalValue: number;
}

// Shared request shape for the per-contact-type case lookups.
export interface ContactCaseLookupParams {
  keyword?: string;
  page: number;
  limit: number;
}

export interface CaseListResult {
  items: CaseListItem[];
  pagination: PaginationMeta;
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

export interface CaseLiensFilters {
  caseId: string;
  liensId: null;
  lawFirmId: null;
  medicalFacilityId: null;
  purchaseDate: null;
  caseManagerId: null;
  lienStatusId: null;
}

export interface ExportResponse {
  data: Array<{ base64: string; export_format: string; filename: string }>;
}

export interface CaseAllocationReportRequest {
  page: number;
  limit: number;
  startDate?: string;
  endDate?: string;
}

export interface CaseReportItem {
  id: string;
  caseNumber?: string;
  clientFirstName?: string;
  clientLastName?: string;
  clientName?: string;
  status?: string;
  dateOfIncident?: string;
  clientDob?: string;
  lawFirmId?: string;
  lawFirm?: string;
  caseManagerId?: string;
  caseManager?: string;
  accidentTypeId?: string;
  accidentType?: string;
  medicalFacility?: string;
  totalLienAmount?: number;
  lienCount?: number;
  createdAtUtc?: string;
  updatedAtUtc?: string;
  [key: string]: unknown;
}

export interface LienReportItem {
  id: string;
  lienNumber?: string;
  status?: string;
  lienType?: string;
  caseId?: string;
  caseNumber?: string;
  clientName?: string;
  lawFirmId?: string;
  lawFirm?: string;
  caseManagerId?: string;
  caseManager?: string;
  facilityId?: string;
  facilityName?: string;
  medicalProviderId?: string;
  medicalProvider?: string;
  fundingCompanyId?: string;
  fundingCompany?: string;
  incidentDate?: string;
  initialServiceDate?: string;
  endServiceDate?: string;
  originalAmount?: number;
  currentBalance?: number;
  purchasePrice?: number;
  totalPurchaseAmount?: number;
  totalBillingAmount?: number;
  createdAtUtc?: string;
  updatedAtUtc?: string;
  [key: string]: unknown;
}

export interface ReportPaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CashMetricResponse {
  periodStart: string;
  periodEnd: string;
  totalAmount: string;
  totalCount: number;
}

export interface CashMetricApiResponse {
  isSuccess: boolean;
  message: string;
  data: CashMetricResponse;
}

export interface AllocationSegment {
  label: string;
  value: number;
}

export interface CreateMedicalLiensDto {
  id: null | string;
  caseId: string;
  status: string;
  purchaseDate: string;
  initialServiceDate: string;
  endServiceDate: string;
  note: string;
  isBulk: boolean | string;
  isServicing: boolean | string;
  fundingCompanyId: string;
}

export interface CreateMedicalLiensResponse {
  id: null | string;
  caseId: string;
  status: string;
  purchaseDate: string;
  initialServiceDate: string;
  endServiceDate: null | string;
  note: string;
  isBulk: boolean | string;
  isServicing: boolean | string;
  fundingCompany: string;
  fundingCompanyId: string;
  created?: string;
  createdBy?: string;
  updated?: string;
  updatedBy?: string;
}

export interface CreateMedicalFacilityDto {
  liensId: string;
  facilityId: string;
  facility: string;
  facilityContactId: string;
  facilityContact: string;
  email: string;
  medicalProviderId: string;
  medicalProvider: string;
}

export interface CreateMedicalCodeLiensDto {
  id: string | null;
  liensId: string;
  code: string;
  medicareCost: string;
  billingAmount: string;
  purchaseAmount: string;
  payee: string;
  outboundCheckNumber: string;
}

export interface CreateMedicalPaymentDto {
  id: null;
  liensId: string;
  payee: string;
  outboundCheckNumber: string;
}

// export interface CreateMedicalCodeDto {
//   liensId: string;
//   payee: string;
//   outboundCheckNumber: string;
// }

export interface CreateMedicalCodeDto {
  code: string;
  description: string;
}

export interface MedicalCodeLiensResponse {
  billingAmount: string | number;
  code: string;
  created: string;
  createdBy: string;
  id: string;
  liensId: string;
  medicareCost: string | number;
  outboundCheckNumber: string;
  payee: string;
  purchaseAmount: string | number;
  updated: string;
  updatedBy: string;
}

export interface BatchReassignCasesRequestDto {
  contactType: string;
  oldId: string;
  newId: string;
}

export interface ReassignLeadRequestDto {
  caseId: string;
  leadId: string;
}

export interface ReassignLawFirmRequestDto {
  caseId: string;
  lawfirm: string;
}

export interface ReassignCaseManagerRequestDto {
  caseId: string;
  caseManager: string;
}

export interface CaseTrackingNote {
  id: string;
  caseId: string;
  note: string;
  isDeleted: "Y" | "N";
  /** ISO UTC instant — normalized by the service from the API's raw "MM/DD/YYYY hh:mm AM/PM" string. */
  created: string;
  createdAtUtc: string;
  createdBy: string;
  userId: string;
  canDelete: boolean;
}

export interface LegacyEnvelope<T> {
  isSuccess: boolean;
  message: string;
  data: T;
}

export type GetCaseNotesResponse = LegacyEnvelope<CaseTrackingNote[]>;
export type MutateCaseNoteResponse = LegacyEnvelope<null>;
