export const STORAGE_KEYS = {
  ACCESS_TOKEN: 'legalsynq.access_token',
  USER_SESSION: 'legalsynq.user_session',
  REMEMBERED_TENANTS: 'legalsynq.remembered_tenants',
  ACTIVE_TENANT_ID: 'legalsynq.active_tenant_id',
  THEME_PREFERENCE: 'legalsynq.theme',
  BIOMETRICS_ENABLED: 'legalsynq.biometrics_enabled',
  ONBOARDING_COMPLETE: 'legalsynq.onboarding_complete',
} as const;

export type StorageKey = (typeof STORAGE_KEYS)[keyof typeof STORAGE_KEYS];
