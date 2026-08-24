import { Ionicons } from '@expo/vector-icons';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';

import { CasesListScreen } from '@/features/cases';
import { DashboardScreen } from '@/features/dashboard';
import { LienMarketplaceScreen } from '@/features/liens';
import { OffersListScreen } from '@/features/offers';
import { ProfileScreen } from '@/features/profile';
import type { MainStackParamList } from '@/navigation/types/navigation';

const Tab = createBottomTabNavigator<MainStackParamList>();

const ICONS = {
  Dashboard: 'home-outline',
  Marketplace: 'storefront-outline',
  Offers: 'pricetag-outline',
  Cases: 'folder-open-outline',
  Profile: 'person-outline',
} as const;

export function BottomTabNavigator() {
  return (
    <Tab.Navigator
      screenOptions={({ route }) => ({
        headerShown: false,
        tabBarActiveTintColor: '#f97332',
        tabBarInactiveTintColor: '#64748b',
        tabBarStyle: {
          display: 'none',
        },
        tabBarIcon: ({ color, size }) => (
          <Ionicons
            color={color}
            name={ICONS[route.name as keyof typeof ICONS] ?? 'ellipse-outline'}
            size={size}
          />
        ),
      })}
    >
      <Tab.Screen component={DashboardScreen} name="Dashboard" options={{ title: 'Home' }} />
      <Tab.Screen component={LienMarketplaceScreen} name="Marketplace" options={{ title: 'Market' }} />
      <Tab.Screen component={OffersListScreen} name="Offers" />
      <Tab.Screen component={CasesListScreen} name="Cases" />
      <Tab.Screen component={ProfileScreen} name="Profile" />
    </Tab.Navigator>
  );
}
