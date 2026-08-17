import { Text, View } from 'react-native';
import { cx, FIGMA_TEXT as TYPE } from '@/shared/styles';
import { SELLING_TOP_BALANCES } from './sellingDashboardData';
import { CardShell } from '../CardShell';
import { SectionTitle } from '../SectionTitle';
import { BrandMark } from './BrandMark';

export function TopBalanceCard({
  isDark,
  items = SELLING_TOP_BALANCES,
}: {
  isDark: boolean;
  items?: typeof SELLING_TOP_BALANCES;
}) {
  return (
    <CardShell isDark={isDark}>
      <SectionTitle
        icon="bar-chart-outline"
        subtitle="Highest outstanding lien balances ranked by total value and share."
        title="Top 5 Liens By Balance"
      />
      <View className="mt-5 gap-4">
        {items.map((item) => (
          <View className="flex-row items-center" key={item.name}>
            <BrandMark variant={item.mark} />
            <View className="ml-3 flex-1">
              <Text className={cx(TYPE.rowLabel, 'text-[#2e3138] dark:text-[#f5f5f5]')}>
                {item.name}
              </Text>
              <Text className={cx(TYPE.microMeta, 'mt-0.5 text-[#8d9098] dark:text-[#8f929b]')}>
                {item.subtitle}
              </Text>
            </View>
            <View className="items-end">
              <Text className={cx(TYPE.rowLabel, 'text-[#2e3138] dark:text-[#f5f5f5]')}>
                {item.balance}
              </Text>
              <Text className={cx(TYPE.microMeta, 'mt-0.5 text-[#8d9098] dark:text-[#8f929b]')}>
                {item.share}
              </Text>
            </View>
          </View>
        ))}
      </View>
    </CardShell>
  );
}
