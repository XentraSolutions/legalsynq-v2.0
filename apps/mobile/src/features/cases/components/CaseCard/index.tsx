import { Text, View } from 'react-native';

import { CASE_TYPE_LABELS } from '@/features/mockData';
import type { CaseView } from '@/features/cases/types/types';
import { Badge } from '@/shared/components/Badge';
import { Card } from '@/shared/components/Card';
import { cx, FIGMA_TEXT } from '@/shared/styles';
import { formatDisplayDate } from '@/shared/utils';

export interface CaseCardProps {
  caseItem: CaseView;
  onPress?: () => void;
}

export function CaseCard({ caseItem, onPress }: CaseCardProps) {
  return (
    <Card onPress={onPress}>
      <View className="flex-row items-start justify-between gap-3">
        <View className="flex-1">
          <Text className={cx(FIGMA_TEXT.cardTitle, 'text-[#202228] dark:text-white')}>{caseItem.patientName}</Text>
          <Text className={cx(FIGMA_TEXT.body, 'mt-1 text-[#6f737d] dark:text-[#a1a1aa]')}>
            {CASE_TYPE_LABELS[caseItem.caseType]}
          </Text>
        </View>
        <Badge label={caseItem.status} variant={caseItem.status === 'OPEN' ? 'success' : 'warning'} />
      </View>
      <View className="mt-4 flex-row flex-wrap gap-x-3 gap-y-1">
        <Text className={cx(FIGMA_TEXT.microMeta, 'text-content-tertiary dark:text-[#8f929b]')}>Ref: {caseItem.caseReference}</Text>
        <Text className={cx(FIGMA_TEXT.microMeta, 'text-content-tertiary dark:text-[#8f929b]')}>{caseItem.lienCount} liens</Text>
        <Text className={cx(FIGMA_TEXT.microMeta, 'text-content-tertiary dark:text-[#8f929b]')}>
          {formatDisplayDate(caseItem.updatedAt, 'MMM d, yyyy')}
        </Text>
      </View>
    </Card>
  );
}
