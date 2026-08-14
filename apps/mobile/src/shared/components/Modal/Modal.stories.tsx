import { Text, View } from 'react-native';

import { Button } from '@/shared/components/Button';

import { Modal } from './Modal';

export default { title: 'Shared/Modal', component: Modal };

export function Open() {
  return (
    <View className="h-80 bg-background">
      <Modal
        footer={<Button label="Confirm" />}
        title="Make an Offer"
        visible
        onClose={() => undefined}
      >
        <Text className="text-base text-content-secondary">Modal body content</Text>
      </Modal>
    </View>
  );
}
