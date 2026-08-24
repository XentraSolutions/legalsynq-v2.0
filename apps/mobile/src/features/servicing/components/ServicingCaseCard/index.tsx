import { Pressable, Text, View } from 'react-native';
import { FontAwesome6, Ionicons } from '@expo/vector-icons';

import type { ServicingCaseListItem } from '@/features/servicing/types/types';
import { Badge, type BadgeVariant } from '@/shared/components/Badge';
import { Card } from '@/shared/components/Card';
import { cx, FIGMA_TEXT } from '@/shared/styles';
import { formatCurrency } from '@/shared/utils';

function displayStatus(status: string): string {
  const labels: Record<string, string> = {
    PreDemand: 'Pre-demand',
    DemandSent: 'Demand Sent',
    InNegotiation: 'Negotiations',
    CaseSettled: 'Case Settled',
  };
  return labels[status] ?? status.replace(/([a-z])([A-Z])/g, '$1 $2');
}

function statusVariant(status: string): BadgeVariant {
  const normalized = status.toLowerCase();
  if (normalized.includes('closed')) return 'error';
  if (normalized.includes('negotiat')) return 'warning';
  return 'success';
}

function DetailRow({
  icon,
  label,
  value,
}: {
  icon: 'billing' | 'lawFirm' | 'purchase';
  label: string;
  value: string;
}) {
  return (
    <View className="flex-row items-center gap-2 py-1.5">
      {icon === 'lawFirm' ? (
        <FontAwesome6 color="#8f929b" name="scale-unbalanced" size={14} />
      ) : (
        <Ionicons
          color="#8f929b"
          name={icon === 'purchase' ? 'cash-outline' : 'receipt-outline'}
          size={16}
        />
      )}
      <Text className={cx(FIGMA_TEXT.formLabel, 'flex-1 text-[#858892] dark:text-[#a1a1aa]')}>
        {label}
      </Text>
      <Text
        className={cx(
          FIGMA_TEXT.formLabel,
          'max-w-[55%] text-right text-[#202228] dark:text-white'
        )}
        numberOfLines={2}
      >
        {value || '—'}
      </Text>
    </View>
  );
}

export function ServicingCaseCard({
  caseItem,
  onPress,
}: {
  caseItem: ServicingCaseListItem;
  onPress: () => void;
}) {
  const status = displayStatus(caseItem.status);

  return (
    <Card className="rounded-[20px] px-6 py-5">
      <View className="flex-row items-start justify-between gap-3">
        <View className="flex-1">
          <Text className="font-jakarta-semibold text-[16px] leading-6 text-[#202228] dark:text-white">
            {caseItem.clientName}
          </Text>
          <Text className={cx(FIGMA_TEXT.body, 'mt-0.5 text-[#858892] dark:text-[#a1a1aa]')}>
            Case ID: {caseItem.caseNumber}
          </Text>
        </View>
        <Badge label={status} variant={statusVariant(caseItem.status)} />
      </View>

      <View className="mt-3">
        <DetailRow icon="lawFirm" label="Law Firm" value={caseItem.lawFirm} />
        <DetailRow
          icon="purchase"
          label="Purchase Amount"
          value={formatCurrency(caseItem.purchaseAmount)}
        />
        <DetailRow
          icon="billing"
          label="Billing Amount"
          value={formatCurrency(caseItem.billingAmount)}
        />
      </View>

      <Pressable
        accessibilityLabel={`View servicing for ${caseItem.caseNumber}`}
        accessibilityRole="button"
        className="mt-4 h-10 items-center justify-center rounded-full bg-[#ebebec] dark:bg-[#2a2b30]"
        onPress={onPress}
      >
        <Text className={cx(FIGMA_TEXT.bodyStrong, 'text-[#202228] dark:text-white')}>
          View Servicing
        </Text>
      </Pressable>
    </Card>
  );
}
