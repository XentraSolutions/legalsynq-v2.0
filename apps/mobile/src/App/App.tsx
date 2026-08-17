import { useEffect } from 'react';
import { View } from 'react-native';
import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { StatusBar } from 'expo-status-bar';
import { useFonts } from 'expo-font';
import { useAtomValue, useSetAtom } from 'jotai';
import { useColorScheme as useNativeWindColorScheme } from 'nativewind';

import { RootNavigator } from '@/navigation/RootNavigator';
import { BiometricEnrollmentModal } from '@/features/authentication/components';
import { registerUnauthorizedHandler } from '@/shared/api/client';
import { PrivacyOverlay } from '@/shared/components/PrivacyOverlay';
import { ApiModeService } from '@/shared/services/ApiMode';
import {
  AuthenticationService,
  BiometricAuthenticationService,
  biometricSessionClient,
} from '@/shared/services/Authentication';
import { apiModeAtom, apiModeHydratedAtom } from '@/shared/state/atoms/apiModeAtom';
import { authAtom } from '@/shared/state/atoms/authAtom';

import { AppProvider } from './AppProvider';
import { APP_FONTS } from './bootstrap/loadFonts';
import { DeepLinkAuthIntegration } from './DeepLinkAuthIntegration';

// Register before the first render. Child biometric hooks may run before App's
// effects, so effect-time registration can leave them using the unavailable fallback.
BiometricAuthenticationService.configureSessionClient(biometricSessionClient);

export default function App() {
  const [fontsLoaded] = useFonts(APP_FONTS);
  const setApiMode = useSetAtom(apiModeAtom);
  const setApiModeHydrated = useSetAtom(apiModeHydratedAtom);

  useEffect(() => {
    registerUnauthorizedHandler(AuthenticationService.clearAccessSession);
  }, []);

  useEffect(() => {
    void ApiModeService.getMode()
      .then(setApiMode)
      .catch(() => undefined)
      .then(AuthenticationService.hydrateSession)
      .finally(() => setApiModeHydrated(true));
  }, [setApiMode, setApiModeHydrated]);

  if (!fontsLoaded) {
    return <View className="flex-1 bg-[#f97332]" />;
  }

  return (
    <GestureHandlerRootView style={{ flex: 1 }}>
      <AppProvider>
        <AppContent />
      </AppProvider>
    </GestureHandlerRootView>
  );
}

function AppContent() {
  const { colorScheme } = useNativeWindColorScheme();
  const auth = useAtomValue(authAtom);

  return (
    <>
      <StatusBar style={colorScheme === 'dark' ? 'light' : 'dark'} />
      <DeepLinkAuthIntegration />
      {auth.status === 'hydrating' ? <View className="flex-1 bg-[#f97332]" /> : <RootNavigator />}
      <BiometricEnrollmentModal />
      <PrivacyOverlay />
    </>
  );
}
