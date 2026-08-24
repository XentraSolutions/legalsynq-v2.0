import type { z } from 'zod';

import type {
  createLienRequestSchema,
  lienCaseTypeSchema,
  lienQueryParamsSchema,
  lienSchema,
  lienStatusSchema,
  makeOfferRequestSchema,
  offerSchema,
  offerStatusSchema,
  statusHistoryEntrySchema,
  updateLienRequestSchema,
  updateOfferRequestSchema,
} from './schemas';

export type LienCaseType = z.infer<typeof lienCaseTypeSchema>;
export type LienStatus = z.infer<typeof lienStatusSchema>;
export type OfferStatus = z.infer<typeof offerStatusSchema>;
export type Lien = z.infer<typeof lienSchema>;
export type Offer = z.infer<typeof offerSchema>;
export type LienQueryParams = z.infer<typeof lienQueryParamsSchema>;
export type CreateLienRequest = z.infer<typeof createLienRequestSchema>;
export type UpdateLienRequest = z.infer<typeof updateLienRequestSchema>;
export type MakeOfferRequest = z.infer<typeof makeOfferRequestSchema>;
export type UpdateOfferRequest = z.infer<typeof updateOfferRequestSchema>;
export type StatusHistoryEntry = z.infer<typeof statusHistoryEntrySchema>;

/** Canonical lien contract returned by the Liens service management endpoints. */
export interface ManagementLien {
  id: string;
  lienNumber: string;
  externalReference?: string | null;
  lienType: string;
  status: string;
  caseId?: string | null;
  facilityId?: string | null;
  originalAmount: number;
  currentBalance?: number | null;
  offerPrice?: number | null;
  purchasePrice?: number | null;
  payoffAmount?: number | null;
  jurisdiction?: string | null;
  isConfidential: boolean;
  subjectFirstName?: string | null;
  subjectLastName?: string | null;
  subjectDisplayName?: string | null;
  orgId: string;
  sellingOrgId?: string | null;
  buyingOrgId?: string | null;
  holdingOrgId?: string | null;
  incidentDate?: string | null;
  description?: string | null;
  openedAtUtc?: string | null;
  closedAtUtc?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface ManagementLienQueryParams {
  search?: string;
  status?: string;
  lienType?: string;
  caseId?: string;
  facilityId?: string;
  page?: number;
  pageSize?: number;
}

export interface CreateManagementLienRequest {
  lienNumber: string;
  externalReference?: string;
  lienType: string;
  caseId?: string;
  facilityId?: string;
  originalAmount: number;
  jurisdiction?: string;
  isConfidential: boolean;
  subjectFirstName?: string;
  subjectLastName?: string;
  incidentDate?: string;
  description?: string;
}

export type UpdateManagementLienRequest = Omit<CreateManagementLienRequest, 'lienNumber'>;

export interface LegacyLienMedicalInfo {
  id: string;
  caseId: string;
  status: string;
  purchaseDate: string;
  initialServiceDate: string;
  endServiceDate: string;
  note: string;
  fundingCompanyId: string;
  fundingCompany: string;
  isBulk: string;
  isServicing: string;
}

export interface LegacyLienFacilityInfo {
  id: string;
  liensId: string;
  facilityId: string;
  facilityContactId: string;
  email: string;
  phone: string;
  medicalProviderId: string;
}

export interface LegacyLienMedicalCode {
  id: string;
  liensId: string;
  code: string;
  medicareCost: string;
  billingAmount: string;
  purchaseAmount: string;
}

export interface LegacyLienDocument {
  id: string;
  liensId: string;
  filename: string;
  typeId: string;
  url: string;
  status: string;
}

export interface ManagementLienDetails {
  medicalList: LegacyLienMedicalInfo[];
  facilityList: LegacyLienFacilityInfo[];
  codeList: LegacyLienMedicalCode[];
  documentList: LegacyLienDocument[];
}

export interface LienMedicalRequest {
  id?: string;
  caseId?: string;
  status?: string;
  purchaseDate?: string;
  initialServiceDate?: string;
  endServiceDate?: string;
  note?: string;
  isBulk?: string;
  isServicing?: string;
  fundingCompanyId?: string;
}

export interface LienFacilityRequest {
  id?: string;
  liensId: string;
  facilityId: string;
  facilityContactId?: string;
  email?: string;
  phone?: string;
  medicalProviderId?: string;
}

export interface LienMedicalCodeRequest {
  id?: string;
  liensId: string;
  code: string;
  medicareCost?: string;
  billingAmount?: string;
  purchaseAmount?: string;
  payee?: string;
  outboundCheckNumber?: string;
}

export interface LienExportFilter {
  caseId?: string;
  liensId?: string;
  lawFirmId?: string;
  medicalFacilityId?: string;
  purchaseDate?: string;
  caseManagerId?: string;
  lienStatusId?: string;
}

export interface LienExportFile {
  base64: string;
  filename: string;
  export_format: string;
}

export interface LienFacility {
  id: string;
  name: string;
  code?: string | null;
  phone?: string | null;
  email?: string | null;
  isActive: boolean;
}

export interface LienFacilityContact {
  id: string;
  facilityId: string;
  firstName: string;
  lastName: string;
  position?: string | null;
  email?: string | null;
  phone?: string | null;
  isActive: boolean;
}

export interface LienDocumentType {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}
