import { View } from 'react-native';

import { Switch } from './Switch';

export default { title: 'Shared/Switch', component: Switch };

export function States() {
  return (
    <View className="gap-3 bg-white p-4">
      <Switch value onValueChange={() => undefined} />
      <Switch value={false} onValueChange={() => undefined} />
      <Switch disabled value onValueChange={() => undefined} />
    </View>
  );
}
