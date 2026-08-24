import { Pressable, Text, View } from 'react-native';
import { DashboardStatCardSkeleton } from '@/features/dashboard/components';
import { cx, FIGMA_COLORS, FIGMA_TEXT as TYPE } from '@/shared/styles';
import { StatCardData } from './index';
import { StatCard } from './StatCard';

export function DashboardStatState({
  isDark,
  isError,
  isLoading,
  label,
  onRetry,
  stat,
}: {
  isDark: boolean;
  isError: boolean;
  isLoading: boolean;
  label: string;
  onRetry: () => void;
  stat: StatCardData;
}) {
  if (isLoading) {
    return <DashboardStatCardSkeleton isDark={isDark} />;
  }

  if (isError) {
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
        <Text className={cx(TYPE.statLabel, 'text-[#8d9098] dark:text-[#8f929b]')}>{label}</Text>
        <Pressable accessibilityRole="button" className="mt-3 self-start" onPress={onRetry}>
          <Text className={cx(TYPE.microStrong, 'text-[#d94f16] dark:text-[#fb8b5c]')}>
            Unable to load · Retry
          </Text>
        </Pressable>
      </View>
    );
  }

  return <StatCard isDark={isDark} stat={stat} />;
}
