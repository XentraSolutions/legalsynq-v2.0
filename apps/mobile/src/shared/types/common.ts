export type Environment = 'development' | 'qa' | 'production';

export type ThemePreference = 'light' | 'dark' | 'system';

export interface FeatureFlags {
  enableBiometrics: boolean;
  enableMarketplace: boolean;
  enableOffers: boolean;
}

export interface DashboardSettings {
  useDummyData: boolean;
}

export interface TimestampedEntity {
  id: string;
  createdAt: string;
  updatedAt: string;
}

export interface SelectOption<TValue extends string = string> {
  label: string;
  value: TValue;
}
