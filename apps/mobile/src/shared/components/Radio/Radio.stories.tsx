import { View } from 'react-native';

import { Radio } from './Radio';

export default { title: 'Shared/Radio', component: Radio };

export function States() {
  return (
    <View className="gap-3 bg-white p-4">
      <Radio selected label="Selected" onChange={() => undefined} />
      <Radio selected={false} label="Unselected" onChange={() => undefined} />
    </View>
  );
}
