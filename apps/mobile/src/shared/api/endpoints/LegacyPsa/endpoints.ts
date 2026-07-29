import { apiClient } from '@/shared/api/client';

export interface LegacyServiceRequestParams {
  iv: string;
  value: string;
}

// Fixed credentials for this app's PSA integration on the legacy backend.
const PSA_CLIENT_ID = 'PLeQe0dmm3gc9gnYlvftYy14H5aaCpr3';
const PSA_CLIENT_SECRET = 'OwXJMnlafWMGpT13';
const PSA_GRANT_TYPE = 'IVPlusTokenId';

// Some legacy responses come back with a Content-Type axios/RN doesn't
// recognize as JSON, leaving response.data as a raw string. Defend against that.
function parseJsonResponse<T>(raw: unknown): T {
  if (typeof raw === 'string') {
    try {
      return JSON.parse(raw) as T;
    } catch {
      return raw as T;
    }
  }
  return raw as T;
}

export const LegacyPsaApi = {
  // Response shape is undocumented in the Postman collection (no example body).
  async generateToken(): Promise<unknown> {
    const response = await apiClient.get<unknown>('/Authentication/IV', {
      headers: {
        client_id: PSA_CLIENT_ID,
        client_secret: PSA_CLIENT_SECRET,
        grant_type: PSA_GRANT_TYPE,
      },
    });
    return parseJsonResponse(response.data);
  },

  // `iv`/`value` come from a prior generateToken() + encryption step.
  // Response shape is undocumented in the Postman collection (no example body).
  async serviceRequest({ iv, value }: LegacyServiceRequestParams): Promise<unknown> {
    const response = await apiClient.get<unknown>('/Service/Request', {
      headers: {
        IV: iv,
        value,
      },
    });
    return parseJsonResponse(response.data);
  },
};
