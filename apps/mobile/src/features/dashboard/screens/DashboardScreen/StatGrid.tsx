import { View } from 'react-native';
import type { StatCardData } from './dashboardShared';
import { StatCard } from './StatCard';

export function StatGrid({ isDark, stats }: { isDark: boolean; stats: StatCardData[] }) {
  return (
    <View className="mt-4 flex-row flex-wrap justify-between gap-y-3">
      {stats.map((stat) => (
        <StatCard isDark={isDark} key={stat.label} stat={stat} />
      ))}
    </View>
  );
}
