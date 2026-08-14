import { View } from 'react-native';

import { Divider } from './Divider';

export default { title: 'Shared/Divider', component: Divider };

export function Variants() {
  return (
    <View className="bg-white p-4">
      <Divider />
      <Divider label="or continue with" />
    </View>
  );
}
