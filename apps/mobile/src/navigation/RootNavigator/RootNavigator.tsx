import * as Sentry from '@sentry/react-native';
import { createNavigationContainerRef } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { useAtomValue } from 'jotai';

import { AuthStack } from '@/navigation/AuthStack';
import { MainStack } from '@/navigation/MainStack';
import type { RootStackParamList } from '@/navigation/types/navigation';
import { ErrorTrackingService } from '@/shared/services/ErrorTracking';
import { authAtom } from '@/shared/state/atoms/authAtom';

const Stack = createNativeStackNavigator<RootStackParamList>();
const navigationRef = createNavigationContainerRef<RootStackParamList>();

function recordCurrentScreen(): void {
  const route = navigationRef.getCurrentRoute();
  if (route) {
    const params = route.params && typeof route.params === 'object'
      ? route.params as Record<string, unknown>
      : undefined;
    ErrorTrackingService.setCurrentScreen(route.name, params);
  }
}

export function RootNavigator() {
  const { isAuthenticated } = useAtomValue(authAtom);

  return (
    <Sentry.NavigationContainer
      ref={navigationRef}
      onReady={recordCurrentScreen}
      onStateChange={recordCurrentScreen}
    >
      <Stack.Navigator screenOptions={{ headerShown: false }}>
        {isAuthenticated ? (
          <Stack.Screen component={MainStack} name="Main" />
        ) : (
          <Stack.Screen component={AuthStack} name="Auth" />
        )}
      </Stack.Navigator>
    </Sentry.NavigationContainer>
  );
}
