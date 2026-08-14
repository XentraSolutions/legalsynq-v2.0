import type { ReactNode } from 'react';
import { Pressable, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

import { cx, FIGMA_TEXT } from '@/shared/styles';

export interface ChipProps {
  label: string;
  selected?: boolean;
  onPress?: () => void;
  onRemove?: () => void;
  leftIcon?: ReactNode;
}

export function Chip({ label, selected = false, onPress, onRemove, leftIcon }: ChipProps) {
  return (
    <Pressable
      accessibilityRole={onPress ? 'button' : undefined}
      onPress={onPress}
      className={cx(
        'self-start rounded-full border px-3 py-1',
        selected ? 'border-[#f97332] bg-[#f97332]' : 'border-border bg-white dark:border-[#303138] dark:bg-[#222329]'
      )}
    >
      <View className="flex-row items-center gap-1">
        {leftIcon}
        <Text className={cx(FIGMA_TEXT.rowValue, selected ? 'text-[#111111]' : 'text-content-secondary dark:text-[#c7c8cc]')}>
          {label}
        </Text>
        {onRemove ? (
          <Pressable accessibilityRole="button" onPress={onRemove} hitSlop={8}>
            <Ionicons color={selected ? '#1d4ed8' : '#64748b'} name="close" size={14} />
          </Pressable>
        ) : null}
      </View>
    </Pressable>
  );
}
