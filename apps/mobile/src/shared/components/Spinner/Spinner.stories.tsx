import { View } from 'react-native';

import { Spinner } from './Spinner';

export default { title: 'Shared/Spinner', component: Spinner };

export function Sizes() {
  return (
    <View className="flex-row items-center gap-4 bg-white p-4">
      <Spinner size="sm" />
      <Spinner />
      <Spinner size="lg" />
    </View>
  );
}
