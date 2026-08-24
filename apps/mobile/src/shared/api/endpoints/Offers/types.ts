import type { z } from 'zod';

import type {
  offerActionRequestSchema,
  offerQueryParamsSchema,
  offerSchema,
  offerStatusSchema,
} from './schemas';

export type Offer = z.infer<typeof offerSchema>;
export type OfferStatus = z.infer<typeof offerStatusSchema>;
export type OfferQueryParams = z.infer<typeof offerQueryParamsSchema>;
export type OfferActionRequest = z.infer<typeof offerActionRequestSchema>;
