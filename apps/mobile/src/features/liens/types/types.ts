import type { LienView } from '@/features/mockData';
import type { LienCaseType, LienStatus } from '@/shared/api/endpoints/Liens';

export interface LienFilter {
  id: string;
  label: string;
  caseType?: LienCaseType;
  status?: LienStatus;
  maxAmount?: number;
  minAmount?: number;
}

export type { LienView };

export type LienManagementFilterKey =
  | 'lawFirmId'
  | 'medicalFacilityId'
  | 'caseManagerId'
  | 'statusId';

export interface LienManagementFilters {
  purchaseStartDate: string;
  purchaseEndDate: string;
  closedStartDate: string;
  closedEndDate: string;
  lawFirmId: string;
  medicalFacilityId: string;
  caseManagerId: string;
  statusId: string;
}

export const EMPTY_LIEN_MANAGEMENT_FILTERS: LienManagementFilters = {
  purchaseStartDate: '',
  purchaseEndDate: '',
  closedStartDate: '',
  closedEndDate: '',
  lawFirmId: '',
  medicalFacilityId: '',
  caseManagerId: '',
  statusId: '',
};

export interface LienFilterOption {
  id: string;
  label: string;
}

export type LienManagementFilterOptions = Record<
  LienManagementFilterKey,
  LienFilterOption[]
>;

export interface ManagementLienListItem {
  id: string;
  lienNumber: string;
  patientName: string;
  status: string;
  purchaseAmount: number;
  medicalFacility: string;
  medicalFacilityId: string;
  lawFirm: string;
  lawFirmId: string;
  caseManager: string;
  caseManagerId: string;
  caseId: string;
  purchaseDate: string;
  closedDate: string;
  initialServiceDate: string;
  billingAmount: number;
}

export interface LienMedicalCodeFormValue {
  id?: string;
  code: string;
  medicalCost: string;
  billingAmount: string;
  purchaseAmount: string;
  payee: string;
  outboundCheckNumber: string;
}

export type LienEditSection = 'company' | 'provider' | 'medicalCodes';

export interface LienFormValues {
  lienNumber: string;
  caseId: string;
  status: string;
  purchaseDate: string;
  initialServiceDate: string;
  endServiceDate: string;
  notes: string;
  isBulk: boolean;
  isServicing: boolean;
  fundingCompanyId: string;
  facilityId: string;
  facilityContactId: string;
  facilityEmail: string;
  facilityPhone: string;
  medicalProviderId: string;
  originalAmount: string;
  jurisdiction: string;
  subjectFirstName: string;
  subjectLastName: string;
  medicalCodes: LienMedicalCodeFormValue[];
  deletedMedicalCodeIds: string[];
  payee: string;
  outboundCheckNumber: string;
}
