import type { z } from 'zod';

import type { uploadedDocumentSchema } from './schemas';

export type UploadedDocument = z.infer<typeof uploadedDocumentSchema>;
