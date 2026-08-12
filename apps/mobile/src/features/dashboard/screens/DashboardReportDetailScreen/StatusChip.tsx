import { Text, View } from 'react-native';
import { cx, FIGMA_TEXT as TYPE } from '@/shared/styles';
import type { BreakdownItem, StatusTone } from './types';

export function StatusChip({
  color,
  status,
  tone,
}: {
  color?: string;
  status: BreakdownItem['status'];
  tone: StatusTone;
}) {
  if (color) {
    return (
      <View
        style={{
          backgroundColor: `${color}22`,
          borderRadius: 999,
          paddingHorizontal: 12,
          paddingVertical: 4,
        }}
      >
        <Text className={TYPE.microStrong} style={{ color }}>
          {status}
        </Text>
      </View>
    );
  }

  const classes = {
    info: {
      container: 'bg-[#dbeafe] dark:bg-[#172554]',
      text: 'text-[#1d4ed8] dark:text-[#93c5fd]',
    },
    success: {
      container: 'bg-[#dcfce7] dark:bg-[#133225]',
      text: 'text-[#2b7744] dark:text-[#86efac]',
    },
    warning: {
      container: 'bg-[#fef3c7] dark:bg-[#3f2f14]',
      text: 'text-[#855f2c] dark:text-[#facc15]',
    },
    danger: {
      container: 'bg-[#fee2e2] dark:bg-[#3f1d1d]',
      text: 'text-[#a43532] dark:text-[#fca5a5]',
    },
  }[tone];

  return (
    <View className={cx('rounded-full px-3 py-1', classes.container)}>
      <Text className={cx(TYPE.microStrong, classes.text)}>{status}</Text>
    </View>
  );
}
