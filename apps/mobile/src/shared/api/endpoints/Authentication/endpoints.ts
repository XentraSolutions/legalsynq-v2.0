import { apiClient } from '@/shared/api/client';

import type {
  ChangePasswordRequest,
  ForgotPasswordRequest,
  LoginRequest,
  LoginResponse,
  ResetPasswordRequest,
  UserSession,
} from './types';

const BASE = '/identity/api/auth';

export const AuthenticationApi = {
  async login(body: LoginRequest): Promise<LoginResponse> {
    const response = await apiClient.post<LoginResponse>(`${BASE}/login`, body);
    return response.data;
  },

  async logout(): Promise<void> {
    await apiClient.post(`${BASE}/logout`);
  },

  async forgotPassword(body: ForgotPasswordRequest): Promise<void> {
    await apiClient.post(`${BASE}/forgot-password`, body);
  },

  async resetPassword(body: ResetPasswordRequest): Promise<void> {
    await apiClient.post(`${BASE}/password-reset/confirm`, body);
  },

  async getMe(): Promise<UserSession> {
    const response = await apiClient.get<UserSession>(`${BASE}/me`);
    return response.data;
  },

  async changePassword(body: ChangePasswordRequest): Promise<void> {
    await apiClient.post(`${BASE}/change-password`, body);
  },
};
