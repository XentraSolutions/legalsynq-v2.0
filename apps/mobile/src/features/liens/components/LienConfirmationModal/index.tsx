import { Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

import { Button } from '@/shared/components/Button';
import { Modal } from '@/shared/components/Modal';
import { cx, FIGMA_TEXT } from '@/shared/styles';

export function LienConfirmationModal({
  confirmLabel,
  description,
  loading,
  title,
  visible,
  onCancel,
  onConfirm,
}: {
  confirmLabel: string;
  description: string;
  loading?: boolean;
  title: string;
  visible: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <Modal
      footer={
        <View className="gap-3">
          <Button label={confirmLabel} loading={loading} onPress={onConfirm} />
          <Button disabled={loading} label="Cancel" variant="secondary" onPress={onCancel} />
        </View>
      }
      visible={visible}
      onClose={onCancel}
    >
      <View className="items-center">
        <View className="h-12 w-12 items-center justify-center rounded-full bg-[#fff1e9] dark:bg-[#3a241a]">
          <Ionicons color="#ee7132" name="share-outline" size={24} />
        </View>
        <Text className={cx(FIGMA_TEXT.sectionTitle, 'mt-4 text-center text-[#202228] dark:text-white')}>
          {title}
        </Text>
        <Text className={cx(FIGMA_TEXT.body, 'mt-2 text-center text-[#71717a] dark:text-[#a1a1aa]')}>
          {description}
        </Text>
      </View>
    </Modal>
  );
}
