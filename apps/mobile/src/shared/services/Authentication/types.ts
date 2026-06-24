import type { SessionEnvelope, UserSession } from '@/shared/types/auth';

export interface LoginCredentials {
  email: string;
  password: string;
  tenantCode?: string;
}

export interface LoginResponse {
  accessToken: string;
  sessionEnvelope: SessionEnvelope;
}

export type { SessionEnvelope, UserSession };
