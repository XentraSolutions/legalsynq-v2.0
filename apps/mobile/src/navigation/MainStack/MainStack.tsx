import { createNativeStackNavigator } from '@react-navigation/native-stack';

import {
  CaseDetailScreen,
  CreateCaseScreen,
  EditCaseDetailsScreen,
  EditCasePersonalScreen,
  PayoffQuoteScreen,
} from '@/features/cases';
import { DashboardReportDetailScreen } from '@/features/dashboard';
import {
  CreateLienScreen,
  EditLienScreen,
  LienDetailScreen,
  ManagementLienDetailScreen,
  MyLiensScreen,
  SellLienScreen,
} from '@/features/liens';
import { OfferDetailScreen } from '@/features/offers';
import { PlaceholderScreen } from '@/features/placeholders';
import { SettingsScreen } from '@/features/profile';
import { ServicingListScreen } from '@/features/servicing';
import { XeniaChatScreen } from '@/features/xenia';
import type { MainStackParamList } from '@/navigation/types/navigation';

import { BottomTabNavigator } from './BottomTabNavigator';

const Stack = createNativeStackNavigator<MainStackParamList>();

export function MainStack() {
  return (
    <Stack.Navigator screenOptions={{ headerShown: false }}>
      <Stack.Screen component={BottomTabNavigator} name="Tabs" />
      <Stack.Screen component={LienDetailScreen} name="LienDetail" />
      <Stack.Screen component={ManagementLienDetailScreen} name="ManagementLienDetail" />
      <Stack.Screen component={CreateLienScreen} name="CreateLien" />
      <Stack.Screen component={EditLienScreen} name="EditLien" />
      <Stack.Screen component={SellLienScreen} name="SellLien" />
      <Stack.Screen component={MyLiensScreen} name="MyLiens" />
      <Stack.Screen component={ServicingListScreen} name="Servicing" />
      <Stack.Screen component={OfferDetailScreen} name="OfferDetail" />
      <Stack.Screen component={CaseDetailScreen} name="CaseDetail" />
      <Stack.Screen component={EditCaseDetailsScreen} name="EditCaseDetails" />
      <Stack.Screen component={EditCasePersonalScreen} name="EditCasePersonal" />
      <Stack.Screen component={PayoffQuoteScreen} name="PayoffQuote" />
      <Stack.Screen component={CreateCaseScreen} name="CreateCase" />
      <Stack.Screen component={SettingsScreen} name="Settings" />
      <Stack.Screen component={XeniaChatScreen} name="XeniaAI" />
      <Stack.Screen component={DashboardReportDetailScreen} name="DashboardReportDetail" />
      <Stack.Screen component={PlaceholderScreen} name="Placeholder" />
    </Stack.Navigator>
  );
}
