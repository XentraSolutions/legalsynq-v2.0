import { createNativeStackNavigator } from '@react-navigation/native-stack';

import { CaseDetailScreen } from '@/features/cases';
import { LienDetailScreen, MyLiensScreen, SellLienScreen } from '@/features/liens';
import { OfferDetailScreen } from '@/features/offers';
import { SettingsScreen } from '@/features/profile';
import type { MainStackParamList } from '@/navigation/types/navigation';

import { BottomTabNavigator } from './BottomTabNavigator';

const Stack = createNativeStackNavigator<MainStackParamList>();

export function MainStack() {
  return (
    <Stack.Navigator screenOptions={{ headerShown: false }}>
      <Stack.Screen component={BottomTabNavigator} name="Tabs" />
      <Stack.Screen component={LienDetailScreen} name="LienDetail" />
      <Stack.Screen component={SellLienScreen} name="SellLien" />
      <Stack.Screen component={MyLiensScreen} name="MyLiens" />
      <Stack.Screen component={OfferDetailScreen} name="OfferDetail" />
      <Stack.Screen component={CaseDetailScreen} name="CaseDetail" />
      <Stack.Screen component={SettingsScreen} name="Settings" />
    </Stack.Navigator>
  );
}
