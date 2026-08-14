import type { z } from 'zod';

import type {
  documentListSchema,
  documentSchema,
  issuedDocumentTokenSchema,
  uploadedDocumentSchema,
} from './schemas';

export type UploadedDocument = z.infer<typeof uploadedDocumentSchema>;
export type Document = z.infer<typeof documentSchema>;
export type DocumentList = z.infer<typeof documentListSchema>;
export type IssuedDocumentToken = z.infer<typeof issuedDocumentTokenSchema>;

export interface DocumentQueryParams {
  productId?: string;
  referenceId?: string;
  referenceType?: string;
  status?: string;
  limit?: number;
  offset?: number;
}

export interface UpdateDocumentRequest {
  title?: string;
  description?: string;
  documentTypeId?: string;
  status?: 'DRAFT' | 'ACTIVE' | 'ARCHIVED' | 'LEGAL_HOLD';
  retainUntil?: string;
}
