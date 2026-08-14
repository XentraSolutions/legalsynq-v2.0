import { Text, View } from 'react-native';

import { Card } from './Card';

export default { title: 'Shared/Card', component: Card };

export function Default() {
  return (
    <View className="bg-background p-4">
      <Card>
        <Text className="text-base text-content-primary">Card content</Text>
      </Card>
    </View>
  );
}
