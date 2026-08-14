import { View } from 'react-native';

import { Checkbox } from './Checkbox';

export default { title: 'Shared/Checkbox', component: Checkbox };

export function States() {
  return (
    <View className="gap-3 bg-white p-4">
      <Checkbox checked label="Selected" onChange={() => undefined} />
      <Checkbox checked={false} label="Unselected" onChange={() => undefined} />
      <Checkbox checked disabled label="Disabled" onChange={() => undefined} />
    </View>
  );
}
