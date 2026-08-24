import { View } from 'react-native';

import { Tabs } from './Tabs';

export default { title: 'Shared/Tabs', component: Tabs };

export function Default() {
  return (
    <View className="bg-white">
      <Tabs
        activeTab="received"
        tabs={[
          { id: 'received', label: 'Received' },
          { id: 'sent', label: 'Sent' },
        ]}
        onTabChange={() => undefined}
      />
    </View>
  );
}
