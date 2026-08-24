import { Text, View } from 'react-native';

import { BottomSheet } from './BottomSheet';

export default { title: 'Shared/BottomSheet', component: BottomSheet };

export function Open() {
  return (
    <View className="h-96 bg-background">
      <BottomSheet index={0} title="Add Note">
        <Text className="text-content-primary">Bottom sheet content</Text>
      </BottomSheet>
    </View>
  );
}
