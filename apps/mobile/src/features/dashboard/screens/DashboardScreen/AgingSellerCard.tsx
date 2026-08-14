import { View } from 'react-native';
import { SELLING_SELLERS } from './index';
import { CardShell } from './CardShell';
import { SectionTitle } from './SectionTitle';
import { SellerRiskRow } from './SellerRiskRow';

export function AgingSellerCard({ isDark }: { isDark: boolean }) {
  return (
    <CardShell isDark={isDark}>
      <SectionTitle
        icon="time-outline"
        subtitle="Track overdue timelines and risk statuses by provider."
        title="Aging By Lien Seller"
      />
      <View className="mt-5">
        {SELLING_SELLERS.map((seller, index) => (
          <SellerRiskRow
            expanded={index === 0}
            isLast={index === SELLING_SELLERS.length - 1}
            key={seller.name}
            seller={seller}
          />
        ))}
      </View>
    </CardShell>
  );
}
