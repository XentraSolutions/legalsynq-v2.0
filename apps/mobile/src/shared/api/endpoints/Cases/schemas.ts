import { z } from 'zod';

import { paginationParamsSchema } from '@/shared/validation/commonSchemas';

import { lienCaseTypeSchema, lienSchema, lienStatusSchema } from '../Liens/schemas';

const optionalIsoDateSchema = z.string().refine(
  (value) => {
    if (!value) return true;
    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
    if (!match) return false;
    const year = Number(match[1]);
    const month = Number(match[2]);
    const day = Number(match[3]);
    const parsed = new Date(Date.UTC(year, month - 1, day));
    return (
      parsed.getUTCFullYear() === year &&
      parsed.getUTCMonth() === month - 1 &&
      parsed.getUTCDate() === day
    );
  },
  'Use a valid date in YYYY-MM-DD format'
);

export const caseStatusSchema = z.enum(['OPEN', 'PENDING', 'CLOSED', 'ARCHIVED']);

export const caseSchema = z.object({
  id: z.string(),
  caseReference: z.string(),
  patientName: z.string(),
  caseType: lienCaseTypeSchema,
  status: caseStatusSchema,
  jurisdiction: z.string(),
  incidentDate: z.string(),
  assignedAttorney: z.string().optional(),
  lienCount: z.number().int().nonnegative(),
  organizationId: z.string(),
  tenantId: z.string(),
  createdAt: z.string(),
  updatedAt: z.string(),
});

export const caseQueryParamsSchema = paginationParamsSchema.extend({
  search: z.string().optional(),
  status: caseStatusSchema.optional(),
});

export const noteSchema = z.object({
  id: z.string(),
  caseId: z.string(),
  authorId: z.string(),
  authorName: z.string(),
  content: z.string(),
  createdAt: z.string(),
});

export const addCaseNoteRequestSchema = z.object({
  content: z.string().trim().min(1, 'Note is required'),
  category: z.string().optional(),
  createdByName: z.string().optional(),
});

export const caseDetailResponseSchema = z.object({
  id: z.string(),
  caseNumber: z.string(),
  externalReference: z.string().nullish(),
  title: z.string().nullish(),
  clientFirstName: z.string(),
  clientLastName: z.string(),
  clientDisplayName: z.string(),
  status: z.string(),
  dateOfIncident: z.string().nullish(),
  clientDob: z.string().nullish(),
  clientPhone: z.string().nullish(),
  clientEmail: z.string().nullish(),
  clientAddress: z.string().nullish(),
  insuranceCarrier: z.string().nullish(),
  policyNumber: z.string().nullish(),
  claimNumber: z.string().nullish(),
  demandAmount: z.number().nullish(),
  settlementAmount: z.number().nullish(),
  description: z.string().nullish(),
  notes: z.string().nullish(),
  openedAtUtc: z.string().nullish(),
  closedAtUtc: z.string().nullish(),
  createdAtUtc: z.string(),
  updatedAtUtc: z.string(),
});

export const createCaseRequestSchema = z.object({
  caseNumber: z.string().trim().optional(),
  clientFirstName: z.string().trim().min(1, 'First name is required'),
  clientLastName: z.string().trim().min(1, 'Last name is required'),
  externalReference: z.string().trim().optional(),
  title: z.string().trim().optional(),
  clientDob: optionalIsoDateSchema.optional(),
  clientPhone: z.string().trim().optional(),
  clientEmail: z.string().trim().email('Enter a valid email').or(z.literal('')).optional(),
  clientAddress: z.string().trim().optional(),
  dateOfIncident: optionalIsoDateSchema.optional(),
  insuranceCarrier: z.string().trim().optional(),
  policyNumber: z.string().trim().optional(),
  claimNumber: z.string().trim().optional(),
  description: z.string().trim().optional(),
  notes: z.string().trim().optional(),
});

export const linkedLienSchema = lienSchema.pick({
  id: true,
  patientName: true,
  caseType: true,
  status: true,
  lienAmount: true,
  askingPrice: true,
  listedAt: true,
});

export const updateCaseStatusRequestSchema = z.object({
  status: caseStatusSchema.or(lienStatusSchema),
});

export const piechartStatusSchema = z.object({
  label: z.string(),
  value: z.number(),
});

export const dashboardPiechartSchema = z.object({
  totalCases: z.number(),
  totalActiveCases: z.number(),
  totalLiens: z.number(),
  totalLienValue: z.number(),
  caseStatus: z.array(piechartStatusSchema),
  lienStatus: z.array(piechartStatusSchema),
});

export const dashboardTaskSummarySchema = z.object({
  totalTasks: z.number(),
  overdue: z.number(),
  dueToday: z.number(),
});

export const reportFilterRequestSchema = z.object({
  page: z.number().int().positive().optional(),
  limit: z.number().int().positive().optional(),
  filterType: z.string().optional(),
  filterId: z.string().optional(),
  startDate: z.string().optional(),
  endDate: z.string().optional(),
});

export const dashboardStatRequestSchema = z.object({
  fromDate: z.string(),
  toDate: z.string(),
});

export const dashboardStatResponseSchema = z
  .object({
    totalAmount: z.number().optional(),
    totalCount: z.number().optional(),
    periodStart: z.string().optional(),
    periodEnd: z.string().optional(),
  })
  .passthrough();

const reportNumericSchema = z.union([z.number(), z.string()]);

export const dashboardTotalLienReportRowSchema = z
  .object({
    label: z.string().optional(),
    name: z.string().optional(),
    status: z.string().optional(),
    lienStatus: z.string().optional(),
    lienStatusName: z.string().optional(),
    statusName: z.string().optional(),
    count: reportNumericSchema.optional(),
    total: reportNumericSchema.optional(),
    value: reportNumericSchema.optional(),
    lienCount: reportNumericSchema.optional(),
    liensCount: reportNumericSchema.optional(),
    totalLiens: reportNumericSchema.optional(),
    purchase: reportNumericSchema.optional(),
    purchaseAmount: reportNumericSchema.optional(),
    totalPurchase: reportNumericSchema.optional(),
    totalPurchaseAmount: reportNumericSchema.optional(),
    billing: reportNumericSchema.optional(),
    billingAmount: reportNumericSchema.optional(),
    totalBilling: reportNumericSchema.optional(),
    totalBillingAmount: reportNumericSchema.optional(),
    percentage: reportNumericSchema.optional(),
    percent: reportNumericSchema.optional(),
  })
  .passthrough();

export const dashboardTotalCaseReportRowSchema = z
  .object({
    id: z.string().optional(),
    caseId: z.string().optional(),
    caseNumber: z.string().optional(),
    caseReference: z.string().optional(),
    externalReference: z.string().optional(),
    clientDisplayName: z.string().optional(),
    clientFirstName: z.string().optional(),
    clientLastName: z.string().optional(),
    patientName: z.string().optional(),
    label: z.string().optional(),
    name: z.string().optional(),
    status: z.string().optional(),
    caseStatus: z.string().optional(),
    currentStatus: z.string().optional(),
    statusName: z.string().optional(),
    dateOfIncident: z.string().optional(),
    dateOfLoss: z.string().optional(),
    incidentDate: z.string().optional(),
    lossDate: z.string().optional(),
    lawFirm: z.string().optional(),
    lawfirm: z.string().optional(),
    lawFirmName: z.string().optional(),
    firmName: z.string().optional(),
    assignedAttorney: z.string().optional(),
    organizationName: z.string().optional(),
    lawFirmId: z.string().optional(),
    accidentType: z.string().optional(),
    accidentTypeId: z.string().optional(),
    caseManager: z.string().optional(),
    caseManagerId: z.string().optional(),
    createdAtUtc: z.string().optional(),
    updatedAtUtc: z.string().optional(),
    count: reportNumericSchema.optional(),
    total: reportNumericSchema.optional(),
    value: reportNumericSchema.optional(),
    caseCount: reportNumericSchema.optional(),
    cases: reportNumericSchema.optional(),
    totalCases: reportNumericSchema.optional(),
    percentage: reportNumericSchema.optional(),
    percent: reportNumericSchema.optional(),
  })
  .passthrough();

export const caseExportFilterSchema = z.object({
  caseId: z.string().optional(),
  keyword: z.string().optional(),
  lawFirmId: z.string().optional(),
  accidentTypeId: z.string().optional(),
  statusId: z.string().optional(),
  caseManagerId: z.string().optional(),
});

export const caseExportFileSchema = z.object({
  base64: z.string(),
  filename: z.string(),
  export_format: z.string(),
});

export const dashboardLawFirmCaseReportRowSchema = z
  .object({
    lawFirmId: z.string().optional(),
    lawfirmId: z.string().optional(),
    lawFirmOrgId: z.string().optional(),
    organizationId: z.string().optional(),
    lawFirm: z.string().optional(),
    lawfirm: z.string().optional(),
    lawFirmName: z.string().optional(),
    firmName: z.string().optional(),
    name: z.string().optional(),
    label: z.string().optional(),
    caseId: z.string().optional(),
    caseNumber: z.string().optional(),
    caseReference: z.string().optional(),
    clientDisplayName: z.string().optional(),
    clientFirstName: z.string().optional(),
    clientLastName: z.string().optional(),
    patientName: z.string().optional(),
    dateOfIncident: z.string().optional(),
    dateOfLoss: z.string().optional(),
    incidentDate: z.string().optional(),
    lossDate: z.string().optional(),
    totalCases: reportNumericSchema.optional(),
    totalCase: reportNumericSchema.optional(),
    caseCount: reportNumericSchema.optional(),
    cases: reportNumericSchema.optional(),
    count: reportNumericSchema.optional(),
    total: reportNumericSchema.optional(),
    value: reportNumericSchema.optional(),
    percentage: reportNumericSchema.optional(),
    percent: z.union([z.number(), z.string()]).optional(),
  })
  .passthrough();

export const dashboardMedicalProviderReportRowSchema = z
  .object({
    medicalProvider: z.string().optional(),
    medicalprovider: z.string().optional(),
    medicalProviderName: z.string().optional(),
    facilityName: z.string().optional(),
    providerName: z.string().optional(),
    name: z.string().optional(),
    label: z.string().optional(),
    caseId: z.string().optional(),
    caseNumber: z.string().optional(),
    caseReference: z.string().optional(),
    clientDisplayName: z.string().optional(),
    clientFirstName: z.string().optional(),
    clientLastName: z.string().optional(),
    patientName: z.string().optional(),
    dateOfIncident: z.string().optional(),
    dateOfLoss: z.string().optional(),
    incidentDate: z.string().optional(),
    lossDate: z.string().optional(),
    totalCases: reportNumericSchema.optional(),
    totalCase: reportNumericSchema.optional(),
    caseCount: reportNumericSchema.optional(),
    cases: reportNumericSchema.optional(),
    count: reportNumericSchema.optional(),
    total: reportNumericSchema.optional(),
    value: reportNumericSchema.optional(),
    percentage: reportNumericSchema.optional(),
    percent: z.union([z.number(), z.string()]).optional(),
  })
  .passthrough();
