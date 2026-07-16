import { useEffect } from 'react';
import { View } from 'react-native';
import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { StatusBar } from 'expo-status-bar';
import { useFonts } from 'expo-font';
import { useSetAtom } from 'jotai';
import { useColorScheme as useNativeWindColorScheme } from 'nativewind';

import { RootNavigator } from '@/navigation/RootNavigator';
import { registerUnauthorizedHandler } from '@/shared/api/client';
import { PrivacyOverlay } from '@/shared/components/PrivacyOverlay';
import { ApiModeService } from '@/shared/services/ApiMode';
import { AuthenticationService } from '@/shared/services/Authentication';
import { apiModeAtom, apiModeHydratedAtom } from '@/shared/state/atoms/apiModeAtom';

import { AppProvider } from './AppProvider';
import { APP_FONTS } from './bootstrap/loadFonts';

export default function App() {
  const [fontsLoaded] = useFonts(APP_FONTS);
  const setApiMode = useSetAtom(apiModeAtom);
  const setApiModeHydrated = useSetAtom(apiModeHydratedAtom);

  useEffect(() => {
    registerUnauthorizedHandler(AuthenticationService.clearSession);
  }, []);

  useEffect(() => {
    void ApiModeService.getMode()
      .then(setApiMode)
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

  return (
    <>
      <StatusBar style={colorScheme === 'dark' ? 'light' : 'dark'} />
      <RootNavigator />
      <PrivacyOverlay />
    </>
  );
}
