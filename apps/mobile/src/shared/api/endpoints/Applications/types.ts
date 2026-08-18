export interface FundingApplicationDetail {
  id: string;
  tenantId: string;
  applicationNumber: string;
  applicantFirstName: string;
  applicantLastName: string;
  email: string;
  phone: string;
  requestedAmount: number | null;
  approvedAmount: number | null;
  caseType: string | null;
  incidentDate: string | null;
  attorneyNotes: string | null;
  approvalTerms: string | null;
  denialReason: string | null;
  funderId: string | null;
  status: string;
  createdByUserId: string | null;
  updatedByUserId: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}
