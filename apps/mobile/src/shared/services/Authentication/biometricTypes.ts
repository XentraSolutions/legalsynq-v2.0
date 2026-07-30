import type { UserSession } from '@/shared/types/auth';
import type { BiometricCapability } from '@/shared/services/DeviceSecurity';

export interface BiometricPreference {
  enabled: true;
  accountLabel: string;
  deviceSessionId: string;
  tenantId: string;
  userId: string;
}

export interface BiometricEnrollmentCredentials {
  accountLabel: string;
  deviceSessionId: string;
  refreshToken: string;
  tenantId: string;
  user: UserSession;
}

export interface BiometricRefreshInput {
  deviceSessionId: string;
  refreshToken: string;
}

export interface BiometricRefreshResult {
  accessToken: string;
  refreshToken: string;
  user: UserSession;
}

export interface BiometricSessionClient {
  isAvailable(): boolean;
  refreshSession(input: BiometricRefreshInput): Promise<BiometricRefreshResult>;
  enableBiometrics(deviceSessionId: string): Promise<void>;
  disableBiometrics(deviceSessionId: string): Promise<void>;
}

export interface BiometricStatus {
  available: boolean;
  backendAvailable: boolean;
  capability: BiometricCapability;
  enabled: boolean;
  preference: BiometricPreference | null;
  reason?: 'backend_unavailable' | 'no_hardware' | 'not_enrolled' | 'not_enabled';
}

export class BiometricSessionUnavailableError extends Error {
  constructor() {
    super('Biometric session endpoints are not available yet.');
    this.name = 'BiometricSessionUnavailableError';
  }
}

export class BiometricCredentialInvalidError extends Error {
  constructor(message = 'Your saved session is no longer available. Please sign in again.') {
    super(message);
    this.name = 'BiometricCredentialInvalidError';
  }
}

export class BiometricAuthenticationCancelledError extends Error {
  constructor() {
    super('Biometric authentication was cancelled.');
    this.name = 'BiometricAuthenticationCancelledError';
  }
}
