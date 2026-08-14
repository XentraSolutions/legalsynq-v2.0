import { useMemo, useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { cx, FIGMA_TEXT as TYPE } from '@/shared/styles';
import { DonutSlice, LEGEND_PAGE_SIZE } from './index';
import { CardShell } from './CardShell';
import { SectionTitle } from './SectionTitle';
import { DonutChart } from './DonutChart';
import { LegendRow } from './LegendRow';
import { LegendPagination } from './LegendPagination';

export function sortDonutSlicesDescending(slices: DonutSlice[]): DonutSlice[] {
  return [...slices].sort((left, right) => right.value - left.value);
}

export function DonutCard({
  centerCaption,
  centerValue,
  icon,
  isDark,
  slices,
  subtitle,
  summaryRows,
  onViewDetails,
  title,
}: {
  centerCaption: string;
  centerValue: string;
  icon: keyof typeof Ionicons.glyphMap;
  isDark: boolean;
  slices: DonutSlice[];
  subtitle: string;
  summaryRows?: Array<{ label: string; value: string }>;
  onViewDetails?: () => void;
  title: string;
}) {
  const [legendPage, setLegendPage] = useState(1);
  const sortedSlices = useMemo(() => sortDonutSlicesDescending(slices), [slices]);
  const totalLegendPages = Math.max(1, Math.ceil(sortedSlices.length / LEGEND_PAGE_SIZE));
  const currentLegendPage = Math.min(legendPage, totalLegendPages);
  const pagedSlices = sortedSlices.slice(
    (currentLegendPage - 1) * LEGEND_PAGE_SIZE,
    currentLegendPage * LEGEND_PAGE_SIZE
  );

  return (
    <CardShell isDark={isDark}>
      <SectionTitle icon={icon} subtitle={subtitle} title={title} />
      <DonutChart centerCaption={centerCaption} centerValue={centerValue} slices={sortedSlices} />
      {sortedSlices.length > 0 ? (
        <>
          <View className="mt-4">
            {pagedSlices.map((slice, index) => (
              <LegendRow
                key={slice.label}
                isLast={index === pagedSlices.length - 1}
                slice={slice}
              />
            ))}
          </View>
          {sortedSlices.length > LEGEND_PAGE_SIZE ? (
            <LegendPagination
              page={currentLegendPage}
              totalPages={totalLegendPages}
              onNext={() => setLegendPage((page) => Math.min(totalLegendPages, page + 1))}
              onPrevious={() => setLegendPage((page) => Math.max(1, page - 1))}
            />
          ) : null}
        </>
      ) : (
        <Text className={cx(TYPE.rowMuted, 'mt-5 text-center text-[#8d9098] dark:text-[#8f929b]')}>
          No report data available for the selected date range.
        </Text>
      )}
      {summaryRows ? (
        <View className="mt-3 gap-4 border-t border-[#ececf0] pt-4 dark:border-[#292a2f]">
          {summaryRows.map((row) => (
            <View className="flex-row items-center justify-between" key={row.label}>
              <Text className={cx(TYPE.rowLabel, 'text-[#535762] dark:text-[#c7c8cc]')}>
                {row.label}
              </Text>
              <Text className={cx(TYPE.rowLabel, 'text-[#22252b] dark:text-[#f4f4f5]')}>
                {row.value}
              </Text>
            </View>
          ))}
        </View>
      ) : null}
      {onViewDetails ? (
        <Pressable
          accessibilityRole="button"
          className="mt-5 h-9 items-center justify-center rounded-full bg-[#ececee] dark:bg-[#2a2b30]"
          onPress={onViewDetails}
        >
          <Text className={cx(TYPE.cta, 'text-[#555964] dark:text-[#e7e7e9]')}>View Details</Text>
        </Pressable>
      ) : (
        <View className="mt-5 h-9 items-center justify-center rounded-full bg-[#ececee] dark:bg-[#2a2b30]">
          <Text className={cx(TYPE.cta, 'text-[#555964] dark:text-[#e7e7e9]')}>View Details</Text>
        </View>
      )}
    </CardShell>
  );
}
