import type { BreakdownFilterOption } from './types';
import { OptionRow } from './OptionRow';
import { ReportOptionSheet } from './ReportOptionSheet';

export function FilterBySheet({
  filterLabel,
  isDark,
  options,
  selectedFilterIds,
  visible,
  onClose,
  onSelect,
}: {
  filterLabel: string;
  isDark: boolean;
  options: BreakdownFilterOption[];
  selectedFilterIds: string[];
  visible: boolean;
  onClose: () => void;
  onSelect: (filterId: string) => void;
}) {
  return (
    <ReportOptionSheet
      description={`Choose one or more ${filterLabel.toLowerCase()} values to narrow the detailed breakdown.`}
      isDark={isDark}
      title={`Filter by ${filterLabel}`}
      visible={visible}
      onClose={onClose}
    >
      {options.map((option) => (
        <OptionRow
          key={option.id}
          label={option.label}
          selected={selectedFilterIds.includes(option.id)}
          onPress={() => onSelect(option.id)}
        />
      ))}
    </ReportOptionSheet>
  );
}
