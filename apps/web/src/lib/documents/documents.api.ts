import { apiClient } from '@/lib/api-client';
import { normalizeDocumentTimestamps } from './documents-timestamps';
import type {
  DocumentResponseDto,
  DocumentListResponseDto,
  DocumentVersionResponseDto,
  IssuedTokenResponseDto,
  UpdateDocumentRequestDto,
  DocumentsQuery,
  UploadDocumentParams,
} from './documents.types';

const BASE = '/lien/documents/documents';

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

export const documentsApi = {
  async list(query: DocumentsQuery = {}) {
    const response = await apiClient.get<DocumentListResponseDto>(
      `${BASE}${toQs(query as Record<string, unknown>)}`,
    );
    return {
      ...response,
      data: normalizeDocumentTimestamps(response.data),
    };
  },

  async getById(id: string) {
    const response = await apiClient.get<{ data: DocumentResponseDto }>(`${BASE}/${id}`);
    return {
      ...response,
      data: normalizeDocumentTimestamps(response.data),
    };
  },

  async update(id: string, request: UpdateDocumentRequestDto) {
    const response = await apiClient.patch<{ data: DocumentResponseDto }>(`${BASE}/${id}`, request);
    return {
      ...response,
      data: normalizeDocumentTimestamps(response.data),
    };
  },

  delete(id: string) {
    return apiClient.delete<void>(`${BASE}/${id}`);
  },

  async upload(params: UploadDocumentParams): Promise<{ data: DocumentResponseDto; correlationId: string }> {
    const formData = new FormData();
    formData.append('file', params.file);
    formData.append('tenantId', params.tenantId);
    formData.append('productId', params.productId);
    formData.append('referenceId', params.referenceId);
    formData.append('referenceType', params.referenceType);
    formData.append('documentTypeId', params.documentTypeId);
    formData.append('title', params.title);
    if (params.description) {
      formData.append('description', params.description);
    }

    const res = await fetch(`/api${BASE}`, {
      method: 'POST',
      credentials: 'include',
      body: formData,
    });

    const correlationId = res.headers.get('X-Correlation-Id') ?? 'unknown';

    if (!res.ok) {
      let message = `HTTP ${res.status}`;
      try {
        const errBody = await res.json();
        message = errBody.message ?? errBody.title ?? message;
      } catch { /* non-JSON error body */ }
      throw new Error(message);
    }

    const body: { data: DocumentResponseDto } = normalizeDocumentTimestamps(await res.json());
    return { data: body.data, correlationId };
  },

  requestViewUrl(id: string) {
    return apiClient.post<{ data: IssuedTokenResponseDto }>(`${BASE}/${id}/view-url`, {});
  },

  requestDownloadUrl(id: string) {
    return apiClient.post<{ data: IssuedTokenResponseDto }>(`${BASE}/${id}/download-url`, {});
  },

  async listVersions(id: string) {
    const response = await apiClient.get<{ data: DocumentVersionResponseDto[] }>(`${BASE}/${id}/versions`);
    return {
      ...response,
      data: normalizeDocumentTimestamps(response.data),
    };
  },
};
