import { View } from 'react-native';

import { Button } from './Button';

export default { title: 'Shared/Button', component: Button };

export function Variants() {
  return (
    <View className="gap-3 bg-white p-4">
      <Button label="Primary" />
      <Button label="Secondary" variant="secondary" />
      <Button label="Ghost" variant="ghost" />
      <Button label="Danger" variant="danger" />
      <Button label="Loading" loading />
      <Button disabled label="Disabled" />
      <Button label="Small" size="sm" />
    </View>
  );
}
