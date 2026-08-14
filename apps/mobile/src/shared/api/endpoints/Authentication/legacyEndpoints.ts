import { apiClient } from '@/shared/api/client';

export interface LegacyLoginRequest {
  username: string;
  password: string;
}

export interface LegacyLoginResponse {
  isSuccess: boolean;
  message: string;
  sessionId?: string;
  data?: {
    firstName?: string;
    lastName?: string;
    email?: string;
    phone?: string;
    roleId?: number;
    programId?: number;
    portalId?: number;
    userType?: string;
    permission?: string;
  };
}

const LEGACY_LOGIN_PATH = '/authentication/login';

// Fixed identifiers for this app's tenant/program on the legacy ("SynqLiens"/"GuardianLiens") backend.
const LEGACY_LOGIN_URL = 'https://guardianliens.legalsynq.com';
const LEGACY_PROGRAM_ID = 1;

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

export const LegacyAuthenticationApi = {
  async login(body: LegacyLoginRequest): Promise<LegacyLoginResponse> {
    const response = await apiClient.post<LegacyLoginResponse>(LEGACY_LOGIN_PATH, {
      username: body.username,
      password: body.password,
      url: LEGACY_LOGIN_URL,
      programId: LEGACY_PROGRAM_ID,
    });
    return parseJsonResponse<LegacyLoginResponse>(response.data);
  },

  async logout(): Promise<void> {
    // No logout endpoint is documented for the legacy backend; local session
    // clearing (in AuthenticationService) is sufficient regardless.
    throw new Error('LegacyAuthenticationApi.logout is not documented.');
  },

  async forgotPassword(): Promise<void> {
    // No forgot-password endpoint is documented for the legacy backend.
    throw new Error('LegacyAuthenticationApi.forgotPassword is not documented.');
  },
};
