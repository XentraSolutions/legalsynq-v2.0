import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { useAtomValue } from 'jotai';

import { AuthStack } from '@/navigation/AuthStack';
import { DeepLinkNavigationService } from '@/navigation/DeepLinkNavigation';
import { MainStack } from '@/navigation/MainStack';
import type { RootStackParamList } from '@/navigation/types/navigation';
import { ErrorTrackingService } from '@/shared/services/ErrorTracking';
import { sentryNavigationIntegration } from '@/shared/services/ErrorTracking/SentryNavigationIntegration';
import { authAtom } from '@/shared/state/atoms/authAtom';

import { rootNavigationRef } from './navigationRef';

const Stack = createNativeStackNavigator<RootStackParamList>();
function recordCurrentScreen(): void {
  const route = rootNavigationRef.getCurrentRoute();
  if (route) {
    const params =
      route.params && typeof route.params === 'object'
        ? (route.params as Record<string, unknown>)
        : undefined;
    ErrorTrackingService.setCurrentScreen(route.name, params);
  }
}

export function RootNavigator() {
  const { isAuthenticated } = useAtomValue(authAtom);

  return (
    <NavigationContainer
      ref={rootNavigationRef}
      onReady={() => {
        sentryNavigationIntegration.registerNavigationContainer(rootNavigationRef);
        recordCurrentScreen();
        DeepLinkNavigationService.onNavigationReady();
      }}
      onStateChange={recordCurrentScreen}
    >
      <Stack.Navigator screenOptions={{ headerShown: false }}>
        {isAuthenticated ? (
          <Stack.Screen component={MainStack} name="Main" />
        ) : (
          <Stack.Screen component={AuthStack} name="Auth" />
        )}
      </Stack.Navigator>
    </NavigationContainer>
  );
}
