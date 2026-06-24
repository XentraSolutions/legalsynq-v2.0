import { apiClient } from '@/shared/api/client';

import type {
  ChangePasswordRequest,
  ForgotPasswordRequest,
  LoginRequest,
  LoginResponse,
  ResetPasswordRequest,
  UserSession,
} from './types';

export const AuthenticationApi = {
  async login(body: LoginRequest): Promise<LoginResponse> {
    const response = await apiClient.post<LoginResponse>('/auth/login', body);
    return response.data;
  },

  async logout(): Promise<void> {
    await apiClient.post('/auth/logout');
  },

  async forgotPassword(body: ForgotPasswordRequest): Promise<void> {
    await apiClient.post('/auth/forgot-password', body);
  },

  async resetPassword(body: ResetPasswordRequest): Promise<void> {
    await apiClient.post('/auth/password-reset/confirm', body);
  },

  async getMe(): Promise<UserSession> {
    const response = await apiClient.get<UserSession>('/auth/me');
    return response.data;
  },

  async changePassword(body: ChangePasswordRequest): Promise<void> {
    await apiClient.post('/auth/change-password', body);
  },
};
