import { apiClient } from '@/shared/api/client';

import {
  documentEnvelopeSchema,
  documentListSchema,
  issuedDocumentTokenEnvelopeSchema,
} from './schemas';
import type {
  Document,
  DocumentList,
  DocumentQueryParams,
  IssuedDocumentToken,
  UpdateDocumentRequest,
  UploadedDocument,
} from './types';

const BASE_PATH = '/documents/documents';

export const DocumentsApi = {
  async uploadDocument(formData: FormData): Promise<UploadedDocument> {
    const response = await apiClient.post(BASE_PATH, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return documentEnvelopeSchema.parse(response.data).data;
  },

  async listDocuments(params: DocumentQueryParams = {}): Promise<DocumentList> {
    const response = await apiClient.get(BASE_PATH, { params });
    return documentListSchema.parse(response.data);
  },

  async getDocument(id: string): Promise<Document> {
    const response = await apiClient.get(`${BASE_PATH}/${id}`);
    return documentEnvelopeSchema.parse(response.data).data;
  },

  async updateDocument(id: string, body: UpdateDocumentRequest): Promise<Document> {
    const response = await apiClient.patch(`${BASE_PATH}/${id}`, body);
    return documentEnvelopeSchema.parse(response.data).data;
  },

  async deleteDocument(id: string): Promise<void> {
    await apiClient.delete(`${BASE_PATH}/${id}`);
  },

  async requestViewUrl(id: string): Promise<IssuedDocumentToken> {
    const response = await apiClient.post(`${BASE_PATH}/${id}/view-url`);
    return issuedDocumentTokenEnvelopeSchema.parse(response.data).data;
  },

  async getDocumentDownloadUrl(id: string): Promise<string> {
    const response = await apiClient.post(`${BASE_PATH}/${id}/download-url`);
    return issuedDocumentTokenEnvelopeSchema.parse(response.data).data.redeemUrl;
  },
};
