export const ANALYTICS_EVENTS = {
  AUTH_LOGIN_SUBMITTED: 'auth.login_submitted',
  AUTH_LOGIN_SUCCEEDED: 'auth.login_succeeded',
  AUTH_LOGIN_FAILED: 'auth.login_failed',
  AUTH_LOGOUT: 'auth.logout',
  BIOMETRIC_ENROLLMENT_OFFERED: 'auth.biometric_enrollment_offered',
  BIOMETRIC_ENROLLMENT_ACCEPTED: 'auth.biometric_enrollment_accepted',
  BIOMETRIC_ENROLLMENT_CANCELLED: 'auth.biometric_enrollment_cancelled',
  BIOMETRIC_ENROLLMENT_COMPLETED: 'auth.biometric_enrollment_completed',
  BIOMETRIC_ENROLLMENT_FAILED: 'auth.biometric_enrollment_failed',
  BIOMETRIC_LOGIN_INITIATED: 'auth.biometric_login_initiated',
  BIOMETRIC_LOGIN_SUCCEEDED: 'auth.biometric_login_succeeded',
  BIOMETRIC_LOGIN_CANCELLED: 'auth.biometric_login_cancelled',
  BIOMETRIC_LOGIN_FAILED: 'auth.biometric_login_failed',
  BIOMETRIC_CREDENTIAL_INVALIDATED: 'auth.biometric_credential_invalidated',
  BIOMETRIC_LOGIN_DISABLED: 'auth.biometric_login_disabled',
  LIEN_MARKETPLACE_OPENED: 'lien.marketplace_opened',
  LIEN_DETAIL_OPENED: 'lien.detail_opened',
  OFFER_SUBMITTED: 'offer.submitted',
  SETTINGS_THEME_CHANGED: 'settings.theme_changed',
} as const;

export type AnalyticsEventName = (typeof ANALYTICS_EVENTS)[keyof typeof ANALYTICS_EVENTS];
