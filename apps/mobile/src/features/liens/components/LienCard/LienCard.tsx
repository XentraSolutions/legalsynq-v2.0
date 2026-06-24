import { Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

import { CASE_TYPE_LABELS } from '@/features/mockData';
import { LienStatusBadge } from '@/features/liens/components/LienStatusBadge';
import type { LienView } from '@/features/liens/types/types';
import { Card } from '@/shared/components/Card';
import { Chip } from '@/shared/components/Chip';
import { cx, FIGMA_TEXT } from '@/shared/styles';
import { formatCurrency, formatRelativeDate } from '@/shared/utils';

export interface LienCardProps {
  lien: LienView;
  actionLabel?: string;
  onPress?: () => void;
}

export function LienCard({ lien, actionLabel = 'View Details', onPress }: LienCardProps) {
  return (
    <Card onPress={onPress}>
      <View className="flex-row items-center justify-between gap-3">
        <LienStatusBadge status={lien.status} />
        <Chip label={CASE_TYPE_LABELS[lien.caseType]} />
      </View>
      <View className="mt-4 flex-row items-start justify-between gap-3">
        <View className="flex-1">
          <Text className={cx(FIGMA_TEXT.bodyStrong, 'text-[#202228] dark:text-white')}>Patient: {lien.patientName}</Text>
          <Text className={cx(FIGMA_TEXT.body, 'mt-1 text-[#6f737d] dark:text-[#a1a1aa]')}>Jurisdiction: {lien.jurisdiction}</Text>
        </View>
        <Text className="font-jakarta-bold text-[20px] leading-[26px] text-[#f97332]">
          {formatCurrency(lien.askingPrice ?? lien.lienAmount)}
        </Text>
      </View>
      <Text className={cx(FIGMA_TEXT.body, 'mt-2 text-[#6f737d] dark:text-[#a1a1aa]')}>
        Lien Amount: {formatCurrency(lien.lienAmount)}
      </Text>
      <View className="mt-3 flex-row items-center justify-between">
        <Text className={cx(FIGMA_TEXT.microMeta, 'text-content-tertiary dark:text-[#8f929b]')}>
          Listed: {lien.listedAt ? formatRelativeDate(lien.listedAt) : 'Draft'} | {lien.offerCount} offers
        </Text>
        <View className="flex-row items-center gap-1">
          <Text className={cx(FIGMA_TEXT.rowValue, 'text-[#f97332]')}>{actionLabel}</Text>
          <Ionicons color="#f97332" name="arrow-forward" size={16} />
        </View>
      </View>
    </Card>
  );
}
