import { z } from 'zod';

import { paginationParamsSchema } from '@/shared/validation/commonSchemas';

import { lienCaseTypeSchema, lienSchema, lienStatusSchema } from '../Liens/schemas';

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
    label: z.string().optional(),
    name: z.string().optional(),
    status: z.string().optional(),
    caseStatus: z.string().optional(),
    currentStatus: z.string().optional(),
    statusName: z.string().optional(),
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
