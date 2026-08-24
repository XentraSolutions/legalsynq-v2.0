import { View } from 'react-native';

import { Avatar } from './Avatar';

export default { title: 'Shared/Avatar', component: Avatar };

export function Sizes() {
  return (
    <View className="flex-row items-center gap-3 bg-white p-4">
      <Avatar name="Smith Law Firm" size="sm" />
      <Avatar name="Smith Law Firm" />
      <Avatar name="Smith Law Firm" size="lg" />
      <Avatar name="Smith Law Firm" size="xl" />
    </View>
  );
}
