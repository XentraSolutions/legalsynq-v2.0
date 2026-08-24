import { Pressable, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

import type { ManagementLienListItem } from '@/features/liens/types/types';
import { Badge, type BadgeVariant } from '@/shared/components/Badge';
import { Card } from '@/shared/components/Card';
import { cx, FIGMA_TEXT } from '@/shared/styles';
import { formatCurrency } from '@/shared/utils';

function statusVariant(status: string): BadgeVariant {
  const value = status.toLowerCase();
  if (value.includes('closed') || value.includes('rejected')) return 'error';
  if (value.includes('pending')) return 'warning';
  if (value.includes('open') || value.includes('active')) return 'success';
  return 'neutral';
}

function DetailRow({
  icon,
  label,
  value,
}: {
  icon: keyof typeof Ionicons.glyphMap;
  label: string;
  value: string;
}) {
  return (
    <View className="flex-row items-center gap-2.5">
      <Ionicons color="#8f929b" name={icon} size={18} />
      <Text className={cx(FIGMA_TEXT.body, 'flex-1 text-[#858892] dark:text-[#a1a1aa]')}>
        {label}
      </Text>
      <Text
        className={cx(FIGMA_TEXT.bodyStrong, 'max-w-[52%] text-right text-[#292b31] dark:text-white')}
        numberOfLines={2}
      >
        {value || '—'}
      </Text>
    </View>
  );
}

export function ManagementLienCard({
  lien,
  onPress,
}: {
  lien: ManagementLienListItem;
  onPress: () => void;
}) {
  return (
    <Card className="rounded-[22px] px-6 py-7">
      <View className="flex-row items-start justify-between gap-3">
        <View className="flex-1">
          <Text className={cx(FIGMA_TEXT.cardTitle, 'text-[#202228] dark:text-white')}>
            {lien.patientName}
          </Text>
          <Text className={cx(FIGMA_TEXT.body, 'mt-1 text-[#6f737d] dark:text-[#a1a1aa]')}>
            Lien ID: {lien.lienNumber}
          </Text>
        </View>
        <Badge label={lien.status} variant={statusVariant(lien.status)} />
      </View>
      <View className="mt-6 gap-3">
        <DetailRow icon="cash-outline" label="Purchase Amount" value={formatCurrency(lien.purchaseAmount)} />
        <DetailRow icon="medkit-outline" label="Medical Facility" value={lien.medicalFacility} />
        <DetailRow icon="scale-outline" label="Law Firm" value={lien.lawFirm} />
      </View>
      <Pressable
        accessibilityLabel={`View lien ${lien.lienNumber}`}
        accessibilityRole="button"
        className="mt-6 h-12 items-center justify-center rounded-full bg-[#ededee] dark:bg-[#2a2b30]"
        onPress={onPress}
      >
        <Text className={cx(FIGMA_TEXT.bodyStrong, 'text-[#292b31] dark:text-white')}>
          View Lien
        </Text>
      </Pressable>
    </Card>
  );
}
