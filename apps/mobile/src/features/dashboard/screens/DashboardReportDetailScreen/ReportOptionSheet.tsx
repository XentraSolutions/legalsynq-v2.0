import type { ReactNode } from 'react';
import { Modal, Pressable, ScrollView, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { cx, FIGMA_COLORS, FIGMA_TEXT as TYPE } from '@/shared/styles';

export function ReportOptionSheet({
  children,
  description,
  isDark,
  title,
  visible,
  onClose,
}: {
  children: ReactNode;
  description: string;
  isDark: boolean;
  title: string;
  visible: boolean;
  onClose: () => void;
}) {
  return (
    <Modal animationType="fade" transparent visible={visible} onRequestClose={onClose}>
      <View className="flex-1 justify-end bg-black/35 px-4 pb-6 dark:bg-black/70">
        <View
          className="max-h-[80%] rounded-[24px] bg-white p-4 dark:bg-[#191a1f]"
          style={{
            shadowColor: isDark ? FIGMA_COLORS.shadowDark : FIGMA_COLORS.shadowLight,
            shadowOpacity: isDark ? 0.28 : 0.45,
            shadowRadius: 12,
            shadowOffset: { height: 6, width: 0 },
            elevation: 4,
          }}
        >
          <View className="mx-auto mb-4 h-1 w-10 rounded-full bg-[#d7d9de] dark:bg-[#3a3b42]" />
          <View className="mb-4 flex-row items-start justify-between gap-4">
            <View className="flex-1">
              <Text className={cx(TYPE.cardTitle, 'text-[#18181b] dark:text-white')}>{title}</Text>
              <Text className={cx(TYPE.cardDescription, 'mt-1 text-[#71717a] dark:text-[#a1a1aa]')}>
                {description}
              </Text>
            </View>
            <Pressable accessibilityRole="button" hitSlop={12} onPress={onClose}>
              <Ionicons color={isDark ? '#a1a1aa' : '#71717a'} name="close-outline" size={22} />
            </Pressable>
          </View>
          <ScrollView showsVerticalScrollIndicator={false}>
            <View className="pb-2">{children}</View>
          </ScrollView>
        </View>
      </View>
    </Modal>
  );
}
