import { Text, View } from 'react-native';
import { cx, FIGMA_COLORS, FIGMA_TEXT as TYPE } from '@/shared/styles';
import type { StatCardData } from './dashboardShared';

export function StatCard({ isDark, stat }: { isDark: boolean; stat: StatCardData }) {
  return (
    <View
      className="w-[48%] rounded-[14px] bg-white p-4 dark:bg-[#191a1f]"
      style={{
        shadowColor: isDark ? FIGMA_COLORS.shadowDark : FIGMA_COLORS.shadowLight,
        shadowOpacity: isDark ? 0.16 : 0.45,
        shadowRadius: 9,
        shadowOffset: { height: 4, width: 0 },
        elevation: 2,
      }}
    >
      <Text className={cx(TYPE.statLabel, 'text-[#8d9098] dark:text-[#8f929b]')}>{stat.label}</Text>
      <Text className={cx(TYPE.statValue, 'mt-4 text-[#22252b] dark:text-[#f4f4f5]')}>
        {stat.value}
      </Text>
    </View>
  );
}
