import { Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { cx, FIGMA_COLORS, FIGMA_TEXT as TYPE } from '@/shared/styles';
import type { BreakdownItem, StatusTone } from './types';
import { StatusChip } from './StatusChip';

export function BreakdownCard({ isDark, item }: { isDark: boolean; item: BreakdownItem }) {
  const statusTone: StatusTone =
    item.statusTone ??
    (item.status === 'Open' ? 'warning' : item.status === 'Active' ? 'info' : 'success');

  return (
    <View
      className="w-full rounded-[20px] bg-white p-6 dark:bg-[#191a1f]"
      style={{
        shadowColor: isDark ? FIGMA_COLORS.shadowDark : FIGMA_COLORS.shadowLight,
        shadowOpacity: isDark ? 0.18 : 0.4,
        shadowRadius: 8,
        shadowOffset: { height: 3, width: 0 },
        elevation: 2,
      }}
    >
      <View className="flex-row items-start justify-between gap-3">
        <Text className={cx(TYPE.cardTitle, 'flex-1 text-[#18181b] dark:text-white')}>
          {item.id}
        </Text>
        {item.showStatus === false ? null : (
          <StatusChip color={item.statusColor} status={item.status} tone={statusTone} />
        )}
      </View>
      <View className="mt-3 gap-3">
        {item.fields.map((field, fieldIndex) => (
          <View
            className="flex-row items-center justify-between gap-3"
            key={`${field.label}-${fieldIndex}`}
          >
            <View className="flex-row items-center gap-2">
              <Ionicons color="#8f929b" name={field.icon} size={14} />
              <Text className={cx(TYPE.rowMuted, 'text-[#71717a] dark:text-[#a1a1aa]')}>
                {field.label}
              </Text>
            </View>
            <Text
              className={cx(TYPE.rowValue, 'max-w-[55%] text-right text-[#18181b] dark:text-white')}
            >
              {field.value}
            </Text>
          </View>
        ))}
      </View>
    </View>
  );
}
