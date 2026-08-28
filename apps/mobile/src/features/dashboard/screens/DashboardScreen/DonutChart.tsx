import { Text, View } from 'react-native';
import Svg, { Circle } from 'react-native-svg';
import { cx, FIGMA_TEXT as TYPE } from '@/shared/styles';
import type { DonutSlice } from './dashboardShared';

export function getDonutCenterValueTextStyle(value: string) {
  if (value.length > 13) return { fontSize: 13, lineHeight: 16 };
  if (value.length > 9) return { fontSize: 16, lineHeight: 20 };
  if (value.length > 7) return { fontSize: 19, lineHeight: 23 };
  return undefined;
}

export function DonutChart({
  centerCaption,
  centerValue,
  slices,
}: {
  centerCaption: string;
  centerValue: string;
  slices: DonutSlice[];
}) {
  const size = 156;
  const strokeWidth = 28;
  const radius = (size - strokeWidth) / 2;
  const circumference = 2 * Math.PI * radius;
  const total = slices.reduce((sum, slice) => sum + slice.value, 0) || 1;
  let accumulated = 0;

  return (
    <View className="mt-7 items-center justify-center">
      <View className="h-[156px] w-[156px] items-center justify-center">
        <Svg height={size} width={size}>
          {slices.map((slice) => {
            const length = (slice.value / total) * circumference;
            const dashOffset = -accumulated;
            accumulated += length;
            return (
              <Circle
                cx={size / 2}
                cy={size / 2}
                fill="transparent"
                key={slice.label}
                r={radius}
                stroke={slice.color}
                strokeDasharray={`${length} ${circumference - length}`}
                strokeDashoffset={dashOffset}
                strokeLinecap="butt"
                strokeWidth={strokeWidth}
                transform={`rotate(-90 ${size / 2} ${size / 2})`}
              />
            );
          })}
        </Svg>
        <View className="absolute h-[96px] w-[96px] items-center justify-center rounded-full bg-white dark:bg-[#191a1f]">
          <Text
            className={cx(TYPE.donutValue, 'w-full text-center text-[#25282e] dark:text-white')}
            numberOfLines={1}
            style={getDonutCenterValueTextStyle(centerValue)}
          >
            {centerValue}
          </Text>
          <Text
            className={cx(
              TYPE.donutCaption,
              'mt-0.5 text-center text-[#767a84] dark:text-[#a1a1aa]'
            )}
          >
            {centerCaption}
          </Text>
        </View>
      </View>
    </View>
  );
}
