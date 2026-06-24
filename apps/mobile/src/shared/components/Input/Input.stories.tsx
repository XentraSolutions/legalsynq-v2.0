import { View } from 'react-native';

import { Input } from './Input';

export default { title: 'Shared/Input', component: Input };

export function States() {
  return (
    <View className="gap-4 bg-white p-4">
      <Input label="Email address" placeholder="name@example.com" value="" />
      <Input errorMessage="Email is required" label="Email address" value="" />
      <Input hint="Use your work email" label="Work email" value="demo@legalsynq.com" />
      <Input label="Notes" multiline value="Longer case notes" />
    </View>
  );
}
