import { z } from 'zod';

import { moneySchema, paginationParamsSchema } from '@/shared/validation/commonSchemas';

export const lienCaseTypeSchema = z.enum([
  'AUTO_ACCIDENT',
  'WORKERS_COMP',
  'PERSONAL_INJURY',
  'MEDICAL_MALPRACTICE',
]);

export const lienStatusSchema = z.enum([
  'DRAFT',
  'AVAILABLE',
  'PENDING',
  'SOLD',
  'SETTLED',
  'DISPUTED',
]);

export const offerStatusSchema = z.enum(['PENDING', 'ACCEPTED', 'DECLINED', 'WITHDRAWN', 'EXPIRED']);

export const lienSchema = z.object({
  id: z.string(),
  caseReference: z.string(),
  patientName: z.string(),
  caseType: lienCaseTypeSchema,
  lienAmount: moneySchema,
  askingPrice: moneySchema.optional(),
  status: lienStatusSchema,
  jurisdiction: z.string(),
  incidentDate: z.string(),
  listedAt: z.string().optional(),
  sellerId: z.string(),
  buyerId: z.string().optional(),
  organizationId: z.string(),
  tenantId: z.string(),
  createdAt: z.string(),
  updatedAt: z.string(),
});

export const offerSchema = z.object({
  id: z.string(),
  lienId: z.string(),
  buyerId: z.string(),
  buyerOrgName: z.string(),
  offerAmount: moneySchema,
  status: offerStatusSchema,
  expiresAt: z.string(),
  notes: z.string().optional(),
  createdAt: z.string(),
});

export const lienQueryParamsSchema = paginationParamsSchema.extend({
  status: lienStatusSchema.optional(),
  caseType: lienCaseTypeSchema.optional(),
  sortBy: z.enum(['amount', 'listedAt', 'offers']).optional(),
  search: z.string().optional(),
});

export const createLienRequestSchema = lienSchema.omit({
  id: true,
  sellerId: true,
  buyerId: true,
  organizationId: true,
  tenantId: true,
  createdAt: true,
  updatedAt: true,
});

export const updateLienRequestSchema = createLienRequestSchema.partial();

export const makeOfferRequestSchema = z.object({
  offerAmount: moneySchema,
  expiresAt: z.string(),
  notes: z.string().optional(),
});

export const updateOfferRequestSchema = z.object({
  status: offerStatusSchema.optional(),
  offerAmount: moneySchema.optional(),
  notes: z.string().optional(),
});

export const statusHistoryEntrySchema = z.object({
  id: z.string(),
  status: lienStatusSchema,
  changedAt: z.string(),
  changedBy: z.string().optional(),
  notes: z.string().optional(),
});
