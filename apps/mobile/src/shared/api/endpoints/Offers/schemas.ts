import { z } from 'zod';

import { paginationParamsSchema } from '@/shared/validation/commonSchemas';

import { offerSchema, offerStatusSchema } from '../Liens/schemas';

export const offerQueryParamsSchema = paginationParamsSchema.extend({
  status: offerStatusSchema.optional(),
  direction: z.enum(['received', 'sent']).optional(),
});

export const offerActionRequestSchema = z.object({
  notes: z.string().optional(),
});

export { offerSchema, offerStatusSchema };
