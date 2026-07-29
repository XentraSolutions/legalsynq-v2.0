export const ANALYTICS_EVENTS = {
  AUTH_LOGIN_SUBMITTED: 'auth.login_submitted',
  AUTH_LOGIN_SUCCEEDED: 'auth.login_succeeded',
  AUTH_LOGIN_FAILED: 'auth.login_failed',
  AUTH_LOGOUT: 'auth.logout',
  LIEN_MARKETPLACE_OPENED: 'lien.marketplace_opened',
  LIEN_DETAIL_OPENED: 'lien.detail_opened',
  OFFER_SUBMITTED: 'offer.submitted',
  SETTINGS_THEME_CHANGED: 'settings.theme_changed',
} as const;

export type AnalyticsEventName = (typeof ANALYTICS_EVENTS)[keyof typeof ANALYTICS_EVENTS];
