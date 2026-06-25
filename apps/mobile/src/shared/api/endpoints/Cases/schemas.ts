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
