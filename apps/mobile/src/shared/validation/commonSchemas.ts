import { z } from 'zod';

export const paginationParamsSchema = z.object({
  page: z.number().int().positive().optional(),
  pageSize: z.number().int().positive().max(100).optional(),
});

export const nonEmptyStringSchema = z.string().trim().min(1, 'Required');

export const moneySchema = z
  .number()
  .positive('Amount must be greater than zero')
  .finite('Amount must be a valid number');
