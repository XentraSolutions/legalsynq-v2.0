import { Text, View } from 'react-native';

import type { BadgeVariant } from '@/shared/components/Badge/Badge';
import { Badge } from '@/shared/components/Badge';
import { cx, FIGMA_TEXT } from '@/shared/styles';

interface CaseSummaryRowProps {
  label: string;
  value?: string | null;
  badgeVariant?: BadgeVariant;
  showDivider?: boolean;
}

export function CaseSummaryRow({
  label,
  value,
  badgeVariant,
  showDivider = true,
}: CaseSummaryRowProps) {
  const displayValue = value?.trim() || '—';

  return (
    <View
      className={cx(
        'min-h-[44px] flex-row items-center justify-between gap-4 py-3',
        showDivider && 'border-b border-[#dedfe2] dark:border-[#33343a]'
      )}
    >
      <Text className={cx(FIGMA_TEXT.body, 'text-[#777a84] dark:text-[#a1a1aa]')}>{label}</Text>
      {badgeVariant && displayValue !== '—' ? (
        <Badge label={displayValue} variant={badgeVariant} />
      ) : (
        <Text
          className={cx(FIGMA_TEXT.bodyStrong, 'max-w-[58%] text-right text-[#202228] dark:text-white')}
        >
          {displayValue}
        </Text>
      )}
    </View>
  );
}
