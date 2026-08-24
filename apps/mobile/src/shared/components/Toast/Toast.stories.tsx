import { View } from 'react-native';

import { Toast } from './Toast';

export default { title: 'Shared/Toast', component: Toast };

export function Types() {
  return (
    <View className="h-32 bg-background">
      <Toast message="Offer submitted" type="success" />
    </View>
  );
}
