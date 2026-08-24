import { useMemo } from 'react';
import { Text, View } from 'react-native';
import Svg, { Circle, Defs, LinearGradient, Path, Polyline, Stop } from 'react-native-svg';
import { FIGMA_TEXT as TYPE } from '@/shared/styles';
import { BLUE, LINE_POINTS, buildLineChart } from './index';
import { CardShell } from './CardShell';
import { SectionTitle } from './SectionTitle';

export function LineChartCard({ isDark }: { isDark: boolean }) {
  const chart = useMemo(() => buildLineChart(220, 132, LINE_POINTS), []);
  const gridColor = isDark ? '#2a2b30' : '#e7e8ec';
  const labelColor = isDark ? '#8f929b' : '#8a8e98';

  return (
    <CardShell isDark={isDark}>
      <SectionTitle
        icon="analytics-outline"
        subtitle="Track fluctuations and growth in lien totals over time."
        title="Liens Over Time"
      />
      <View className="mt-7 flex-row">
        <View className="w-9 justify-between pb-6">
          {['$4M', '$3M', '$2M', '$1M', '$0'].map((label) => (
            <Text className={TYPE.microMeta} key={label} style={{ color: labelColor }}>
              {label}
            </Text>
          ))}
        </View>
        <View className="flex-1">
          <Svg height={150} width="100%" viewBox="0 0 220 150">
            <Defs>
              <LinearGradient id="lineFill" x1="0" x2="0" y1="0" y2="1">
                <Stop offset="0" stopColor={BLUE} stopOpacity={isDark ? 0.45 : 0.28} />
                <Stop offset="1" stopColor={BLUE} stopOpacity="0.03" />
              </LinearGradient>
            </Defs>
            {[0, 1, 2, 3, 4].map((index) => (
              <Path
                d={`M0 ${index * 27 + 8} H220`}
                key={index}
                stroke={gridColor}
                strokeWidth="1"
              />
            ))}
            <Path d={chart.areaPath} fill="url(#lineFill)" />
            <Polyline
              fill="none"
              points={chart.pointsString}
              stroke={BLUE}
              strokeLinecap="round"
              strokeWidth="3"
            />
            {chart.points.map((point) => (
              <Circle cx={point.x} cy={point.y} fill={BLUE} key={`${point.x}-${point.y}`} r="3" />
            ))}
          </Svg>
          <View className="mt-1 flex-row justify-between px-1">
            {['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun'].map((label) => (
              <Text className={TYPE.microMeta} key={label} style={{ color: labelColor }}>
                {label}
              </Text>
            ))}
          </View>
        </View>
      </View>
    </CardShell>
  );
}
