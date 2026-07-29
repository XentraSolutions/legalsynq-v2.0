import { apiClient } from '@/shared/api/client';

import type { UploadedDocument } from './types';

export const DocumentsApi = {
  async uploadDocument(formData: FormData): Promise<UploadedDocument> {
    const response = await apiClient.post<UploadedDocument>('/liens/api/documents', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return response.data;
  },

  async getDocumentDownloadUrl(id: string): Promise<string> {
    const response = await apiClient.get<string>(`/liens/api/documents/${id}/content`);
    return response.data;
  },
};
