import { Text, View } from 'react-native';

export default { title: 'Shared/PrivacyOverlay' };

export function LockedState() {
  return (
    <View className="flex-1 items-center justify-center bg-primary-900 p-10">
      <View className="mb-4 h-16 w-16 items-center justify-center rounded-2xl bg-white">
        <Text className="text-2xl font-bold text-primary-600">LS</Text>
      </View>
      <Text className="text-lg font-semibold text-white">App is locked</Text>
    </View>
  );
}
