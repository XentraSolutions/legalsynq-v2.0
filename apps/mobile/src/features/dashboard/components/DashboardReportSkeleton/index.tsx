import { View } from 'react-native';

import { Skeleton } from '@/shared/components/Skeleton';
import { FIGMA_COLORS } from '@/shared/styles';

export interface DashboardSkeletonProps {
  isDark: boolean;
}

interface DashboardReportSkeletonProps extends DashboardSkeletonProps {
  hasSummaryRows?: boolean;
  legendDetailRows?: number;
  legendRows?: number;
}

export function DashboardReportSkeleton({
  hasSummaryRows = false,
  isDark,
  legendDetailRows = 0,
  legendRows = 4,
}: DashboardReportSkeletonProps) {
  return (
    <View
      className="mt-5 rounded-[16px] bg-white p-5 dark:bg-[#191a1f]"
      style={{
        shadowColor: isDark ? FIGMA_COLORS.shadowDark : FIGMA_COLORS.shadowLight,
        shadowOpacity: isDark ? 0.18 : 0.45,
        shadowRadius: 10,
        shadowOffset: { height: 4, width: 0 },
        elevation: 2,
      }}
      testID="dashboard-report-skeleton"
    >
      <View className="flex-row items-center gap-2">
        <Skeleton height={17} variant="circle" width={17} />
        <Skeleton height={19} variant="text" width="48%" />
      </View>
      <View className="mt-2 gap-1">
        <Skeleton height={15} variant="text" width="100%" />
        <Skeleton height={15} variant="text" width="76%" />
      </View>

      <View className="mt-7 items-center">
        <Skeleton height={156} variant="circle" width={156} />
      </View>

      <View className="mt-4">
        {Array.from({ length: legendRows }, (_, index) => (
          <View
            className="border-b border-dashed border-[#e8e8ec] py-3 dark:border-[#292a2f]"
            key={index}
          >
            <View className="flex-row items-center justify-between">
              <View className="flex-row items-center gap-3">
                <Skeleton height={16} width={6} borderRadius={9999} />
                <Skeleton height={16} variant="text" width={88} />
              </View>
              <Skeleton height={16} variant="text" width={74} />
            </View>
            {Array.from({ length: legendDetailRows }, (_, detailIndex) => (
              <View className="mt-3 flex-row items-center justify-between pl-8" key={detailIndex}>
                <Skeleton height={16} variant="text" width={72} />
                <Skeleton height={16} variant="text" width={82} />
              </View>
            ))}
          </View>
        ))}
      </View>

      {hasSummaryRows ? (
        <View className="mt-3 gap-4 border-t border-[#ececf0] pt-4 dark:border-[#292a2f]">
          {[0, 1].map((row) => (
            <View className="flex-row items-center justify-between" key={row}>
              <Skeleton height={16} variant="text" width="52%" />
              <Skeleton height={16} variant="text" width="30%" />
            </View>
          ))}
        </View>
      ) : null}

      <View className="mt-5">
        <Skeleton borderRadius={9999} height={36} width="100%" />
      </View>
    </View>
  );
}

export { DashboardStatCardSkeleton } from './DashboardStatCardSkeleton';
