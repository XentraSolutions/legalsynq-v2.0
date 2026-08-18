import type { NavigatorScreenParams } from '@react-navigation/native';

import type { DashboardDateRange, DashboardReportType } from '@/features/dashboard/types/types';
import type { LienEditSection } from '@/features/liens/types/types';

export type RootStackParamList = {
  Auth: NavigatorScreenParams<AuthStackParamList>;
  Main: NavigatorScreenParams<MainStackParamList>;
};

export type AuthStackParamList = {
  Login: undefined;
  ForgotPassword: undefined;
  TenantSelection: undefined;
};

export type MainTabParamList = {
  Dashboard: undefined;
  Marketplace: undefined;
  Offers: undefined;
  Cases: undefined;
  Profile: undefined;
};

export type MainStackParamList = {
  Tabs: NavigatorScreenParams<MainTabParamList>;
  Dashboard: undefined;
  Marketplace: undefined;
  Offers: undefined;
  Cases: undefined;
  Profile: undefined;
  LienDetail: { lienId: string };
  ManagementLienDetail: { lienId: string };
  CreateLien: { caseId?: string };
  EditLien: { lienId: string; section: LienEditSection };
  SellLien: undefined;
  MyLiens: undefined;
  Servicing: undefined;
  Contacts: undefined;
  ContactDetail: { contactId: string };
  ApplicationDetail: { applicationId: string };
  ContactForm: { contactId?: string; contactType?: string };
  ReassignContactCases: { contactId: string };
  FacilityDetail: { facilityId: string };
  FacilityForm: { facilityId?: string };
  OfferDetail: { offerId: string };
  CaseDetail: { caseId: string; initialTab?: 'servicing' };
  CaseTaskForm: { caseId: string; taskId?: string };
  EditCaseDetails: { caseId: string };
  EditCasePersonal: { caseId: string };
  PayoffQuote: { caseId: string };
  CreateCase: undefined;
  Settings: undefined;
  XeniaAI: undefined;
  DashboardReportDetail: { reportType: DashboardReportType; dateRange: DashboardDateRange };
  Placeholder: { title: string; subtitle?: string };
};
