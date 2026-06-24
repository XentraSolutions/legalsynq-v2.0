import { z } from 'zod';

export const uploadedDocumentSchema = z.object({
  id: z.string(),
  filename: z.string(),
  url: z.string(),
});
