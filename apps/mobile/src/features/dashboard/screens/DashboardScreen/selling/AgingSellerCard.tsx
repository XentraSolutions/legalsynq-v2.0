import { View } from 'react-native';
import { SELLING_SELLERS, type SellerRisk } from './sellingDashboardData';
import { CardShell } from '../CardShell';
import { SectionTitle } from '../SectionTitle';
import { SellerRiskRow } from './SellerRiskRow';

export function AgingSellerCard({
  isDark,
  sellers = SELLING_SELLERS,
  subtitle = 'Track overdue timelines and risk statuses by buyer.',
  title = 'Aging By Lien Buyer',
}: {
  isDark: boolean;
  sellers?: SellerRisk[];
  subtitle?: string;
  title?: string;
}) {
  return (
    <CardShell isDark={isDark}>
      <SectionTitle icon="time-outline" subtitle={subtitle} title={title} />
      <View className="mt-5">
        {sellers.map((seller, index) => (
          <SellerRiskRow
            expanded={index === 0}
            isLast={index === sellers.length - 1}
            key={seller.name}
            seller={seller}
          />
        ))}
      </View>
    </CardShell>
  );
}
