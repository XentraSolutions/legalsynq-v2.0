import { Pressable, Text, View } from 'react-native';
import { FontAwesome6, Ionicons } from '@expo/vector-icons';

import type { CaseListItem } from '@/features/cases/types/types';
import { Badge, type BadgeVariant } from '@/shared/components/Badge';
import { Card } from '@/shared/components/Card';
import { cx, FIGMA_TEXT } from '@/shared/styles';
import { formatDisplayDate } from '@/shared/utils';

export interface CaseCardProps {
  caseItem: CaseListItem;
  onPress?: () => void;
}

function displayDate(value: string): string {
  try {
    return formatDisplayDate(value, 'MM/dd/yyyy');
  } catch {
    return value;
  }
}

function statusVariant(status: string): BadgeVariant {
  const normalized = status.trim().toLowerCase();
  if (normalized.includes('closed')) return 'error';
  if (normalized.includes('negotiat')) return 'warning';
  if (
    normalized.includes('settled') ||
    normalized.includes('demand') ||
    normalized.includes('open') ||
    normalized.includes('active')
  ) {
    return 'success';
  }
  return 'neutral';
}

function DetailRow({
  icon,
  label,
  value,
}: {
  icon: 'accidentType' | 'dateOfLoss' | 'lawFirm';
  label: string;
  value: string;
}) {
  return (
    <View className="flex-row items-center gap-2.5">
      <DetailIcon icon={icon} />
      <Text className={cx(FIGMA_TEXT.body, 'flex-1 text-[#858892] dark:text-[#a1a1aa]')}>
        {label}
      </Text>
      <Text
        className={cx(FIGMA_TEXT.bodyStrong, 'max-w-[52%] text-right text-[#292b31] dark:text-white')}
        numberOfLines={1}
      >
        {value || '—'}
      </Text>
    </View>
  );
}

function DetailIcon({ icon }: { icon: 'accidentType' | 'dateOfLoss' | 'lawFirm' }) {
  if (icon === 'lawFirm') {
    return <FontAwesome6 color="#8f929b" name="scale-unbalanced" size={17} />;
  }

  if (icon === 'accidentType') {
    return (
      <View className="h-[15px] w-[18px] items-center justify-center rounded-[3px] border border-[#8f929b]">
        <Ionicons color="#8f929b" name="pulse" size={12} />
      </View>
    );
  }

  return <Ionicons color="#8f929b" name="calendar-outline" size={18} />;
}

export function CaseCard({ caseItem, onPress }: CaseCardProps) {
  return (
    <Card className="rounded-[22px] px-6 py-7">
      <View className="flex-row items-start justify-between gap-3">
        <View className="flex-1">
          <Text className={cx(FIGMA_TEXT.cardTitle, 'text-[#202228] dark:text-white')}>
            {caseItem.clientName}
          </Text>
          <Text className={cx(FIGMA_TEXT.body, 'mt-1 text-[#6f737d] dark:text-[#a1a1aa]')}>
            Case ID: {caseItem.caseNumber}
          </Text>
        </View>
        <Badge label={caseItem.status} variant={statusVariant(caseItem.status)} />
      </View>
      <View className="mt-6 gap-3">
        <DetailRow icon="accidentType" label="Accident Type" value={caseItem.accidentType} />
        <DetailRow icon="lawFirm" label="Law Firm" value={caseItem.lawFirm} />
        <DetailRow
          icon="dateOfLoss"
          label="Date of Loss"
          value={caseItem.dateOfLoss ? displayDate(caseItem.dateOfLoss) : ''}
        />
      </View>
      <Pressable
        accessibilityLabel={`View case ${caseItem.caseNumber}`}
        accessibilityRole="button"
        className="mt-6 h-12 items-center justify-center rounded-full bg-[#ededee] dark:bg-[#2a2b30]"
        onPress={onPress}
      >
        <Text className={cx(FIGMA_TEXT.bodyStrong, 'text-[#292b31] dark:text-white')}>
          View Case
        </Text>
      </Pressable>
    </Card>
  );
}
