import { z } from 'zod';

export const apiErrorSchema = z.object({
  code: z.string().default('API_ERROR'),
  message: z.string(),
  statusCode: z.number().optional(),
  correlationId: z.string().optional(),
  details: z.unknown().optional(),
});

export const pagedResultSchema = <TItem extends z.ZodTypeAny>(itemSchema: TItem) =>
  z.object({
    items: z.array(itemSchema),
    page: z.number(),
    pageSize: z.number(),
    totalCount: z.number(),
    totalPages: z.number(),
  });
