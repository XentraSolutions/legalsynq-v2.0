import type { LoginResponse as ApiLoginResponse } from '@/shared/api/endpoints/Authentication';
import type { UserSession } from '@/shared/types/auth';

export interface LoginCredentials {
  email: string;
  password: string;
  tenantCode: string;
}

export type LoginResponse = ApiLoginResponse;

export type { UserSession };
