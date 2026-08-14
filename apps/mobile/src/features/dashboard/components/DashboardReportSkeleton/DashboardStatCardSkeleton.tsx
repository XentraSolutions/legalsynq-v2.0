import { View } from 'react-native';

import { Skeleton } from '@/shared/components/Skeleton';
import { FIGMA_COLORS } from '@/shared/styles';
import type { DashboardSkeletonProps } from './index';

export function DashboardStatCardSkeleton({ isDark }: DashboardSkeletonProps) {
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
      testID="dashboard-stat-skeleton"
    >
      <Skeleton height={13} variant="text" width="72%" />
      <View className="mt-4">
        <Skeleton height={19} variant="text" width="82%" />
      </View>
    </View>
  );
}
