import { View } from 'react-native';

import { Chip } from './Chip';

export default { title: 'Shared/Chip', component: Chip };

export function States() {
  return (
    <View className="flex-row gap-2 bg-white p-4">
      <Chip label="All" />
      <Chip label="Auto Accident" selected />
      <Chip label="Workers Comp" onRemove={() => undefined} />
    </View>
  );
}
