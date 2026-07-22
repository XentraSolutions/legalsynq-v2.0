import type { NavigatorScreenParams } from '@react-navigation/native';

import type { DashboardDateRange, DashboardReportType } from '@/features/dashboard/types/types';

export type RootStackParamList = {
  Auth: NavigatorScreenParams<AuthStackParamList>;
  Main: NavigatorScreenParams<MainStackParamList>;
};

export type AuthStackParamList = {
  Login: undefined;
  ForgotPassword: undefined;
  TenantSelection: undefined;
};

export type MainStackParamList = {
  Tabs: undefined;
  Dashboard: undefined;
  Marketplace: undefined;
  Offers: undefined;
  Cases: undefined;
  Profile: undefined;
  LienDetail: { lienId: string };
  SellLien: undefined;
  MyLiens: undefined;
  OfferDetail: { offerId: string };
  CaseDetail: { caseId: string };
  CreateCase: undefined;
  Settings: undefined;
  XeniaAI: undefined;
  DashboardReportDetail: { reportType: DashboardReportType; dateRange: DashboardDateRange };
  Placeholder: { title: string; subtitle?: string };
};
