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
