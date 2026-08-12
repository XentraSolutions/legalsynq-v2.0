import { Text, View } from 'react-native';
import { cx, FIGMA_TEXT as TYPE } from '@/shared/styles';
import { DonutSlice } from './index';

export function LegendRow({ isLast, slice }: { isLast: boolean; slice: DonutSlice }) {
  return (
    <View
      className={`${isLast ? '' : 'border-b border-dashed border-[#e8e8ec] dark:border-[#292a2f]'} py-3`}
    >
      <View className="flex-row items-center justify-between gap-3">
        <View className="flex-row items-center gap-3">
          <View className="h-4 w-1.5 rounded-full" style={{ backgroundColor: slice.color }} />
          <Text className={cx(TYPE.rowLabel, 'text-[#4d515c] dark:text-[#e1e1e4]')}>
            {slice.label}
          </Text>
        </View>
        <Text className={cx(TYPE.rowValue, 'text-[#6e727c] dark:text-[#a3a4ab]')}>
          {slice.amount} {slice.percent}
        </Text>
      </View>
      {slice.details?.map((detail) => (
        <View className="mt-3 flex-row items-center justify-between pl-8" key={detail.label}>
          <Text className={cx(TYPE.rowMuted, 'text-[#8b8f99] dark:text-[#8f929b]')}>
            {detail.label}
          </Text>
          <Text className={cx(TYPE.rowValue, 'text-[#8b8f99] dark:text-[#a3a4ab]')}>
            {detail.value}
          </Text>
        </View>
      ))}
    </View>
  );
}
