import type { ReactNode } from 'react';
import { Modal as RNModal, Pressable, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

import { cx, FIGMA_TEXT } from '@/shared/styles';

export interface ModalProps {
  visible: boolean;
  onClose: () => void;
  title?: string;
  children: ReactNode;
  footer?: ReactNode;
}

export function Modal({ visible, onClose, title, children, footer }: ModalProps) {
  return (
    <RNModal animationType="fade" transparent visible={visible} onRequestClose={onClose}>
      <View className="flex-1 items-center justify-center bg-black/50">
        <Pressable className="absolute inset-0" onPress={onClose} />
        <View className="mx-5 w-[90%] rounded-[20px] bg-white p-6 shadow-lg dark:bg-[#191a1f]">
          {title ? (
            <View className="mb-4 flex-row items-center justify-between">
              <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-[#202228] dark:text-white')}>{title}</Text>
              <Pressable accessibilityRole="button" onPress={onClose} hitSlop={12}>
                <Ionicons color="#f97332" name="close" size={24} />
              </Pressable>
            </View>
          ) : null}
          {children}
          {footer ? <View className="mt-5">{footer}</View> : null}
        </View>
      </View>
    </RNModal>
  );
}
