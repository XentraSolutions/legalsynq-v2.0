import { z } from 'zod';

export const documentSchema = z.object({
  id: z.string(),
  tenantId: z.string(),
  productId: z.string(),
  referenceId: z.string(),
  referenceType: z.string(),
  documentTypeId: z.string(),
  title: z.string(),
  description: z.string().nullable().optional(),
  status: z.string(),
  mimeType: z.string(),
  fileSizeBytes: z.number(),
  currentVersionId: z.string().nullable().optional(),
  versionCount: z.number(),
  scanStatus: z.string(),
  scanCompletedAt: z.string().nullable().optional(),
  scanThreats: z.array(z.string()),
  isDeleted: z.boolean(),
  retainUntil: z.string().nullable().optional(),
  legalHoldAt: z.string().nullable().optional(),
  createdAt: z.string(),
  createdBy: z.string(),
  updatedAt: z.string(),
  updatedBy: z.string(),
});

export const documentEnvelopeSchema = z.object({ data: documentSchema });

export const documentListSchema = z.object({
  data: z.array(documentSchema),
  total: z.number(),
  limit: z.number(),
  offset: z.number(),
});

export const issuedDocumentTokenSchema = z.object({
  accessToken: z.string(),
  redeemUrl: z.string(),
  expiresInSeconds: z.number(),
  type: z.string(),
});

export const issuedDocumentTokenEnvelopeSchema = z.object({
  data: issuedDocumentTokenSchema,
});

export const uploadedDocumentSchema = documentSchema;
