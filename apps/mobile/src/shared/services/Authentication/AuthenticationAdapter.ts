import type { AuthState, SessionEnvelope } from '@/shared/types/auth';

export const AuthenticationAdapter = {
  toAuthState(accessToken: string, sessionEnvelope: SessionEnvelope): AuthState {
    return {
      user: sessionEnvelope.user,
      token: accessToken,
      isAuthenticated: true,
    };
  },
};
