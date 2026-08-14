import { useEffect, useState } from 'react';
import { AppState, Text, View, type AppStateStatus } from 'react-native';

export function PrivacyOverlay() {
  const [appState, setAppState] = useState<AppStateStatus>(AppState.currentState);
  const locked = appState === 'background' || appState === 'inactive';

  useEffect(() => {
    const subscription = AppState.addEventListener('change', setAppState);
    return () => subscription.remove();
  }, []);

  if (!locked) {
    return null;
  }

  return (
    <View className="absolute inset-0 z-[200] flex-1 items-center justify-center bg-[#050506]">
      <View className="mb-4 h-16 w-16 items-center justify-center rounded-2xl bg-white">
        <Text className="font-jakarta-bold text-[24px] leading-[30px] text-[#f97332]">LS</Text>
      </View>
      <Text className="font-jakarta-semibold text-[18px] leading-[24px] text-white">App is locked</Text>
    </View>
  );
}
