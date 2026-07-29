import { z } from 'zod';

export const updatePhoneRequestSchema = z.object({
  phone: z.string().trim().min(7, 'Enter a valid phone number'),
});
