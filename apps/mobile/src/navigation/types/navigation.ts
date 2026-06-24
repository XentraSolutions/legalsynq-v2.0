import type { NavigatorScreenParams } from '@react-navigation/native';

export type RootStackParamList = {
  Auth: NavigatorScreenParams<AuthStackParamList>;
  Main: NavigatorScreenParams<MainStackParamList>;
};

export type AuthStackParamList = {
  Login: undefined;
  ForgotPassword: undefined;
};

export type MainStackParamList = {
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
  Settings: undefined;
};
