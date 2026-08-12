import { Text } from 'react-native';
import { cx, FIGMA_TEXT as TYPE } from '@/shared/styles';
import type { BreakdownSortDirection, BreakdownSortField, BreakdownSortOption } from './types';
import { OptionRow } from './OptionRow';
import { ReportOptionSheet } from './ReportOptionSheet';

export function SortBySheet({
  isDark,
  options,
  selectedDirection,
  selectedField,
  visible,
  onClose,
  onDirectionChange,
  onFieldChange,
}: {
  isDark: boolean;
  options: BreakdownSortOption[];
  selectedDirection: BreakdownSortDirection;
  selectedField: BreakdownSortField;
  visible: boolean;
  onClose: () => void;
  onDirectionChange: (direction: BreakdownSortDirection) => void;
  onFieldChange: (field: BreakdownSortField) => void;
}) {
  return (
    <ReportOptionSheet
      description="Choose the field and direction for the detailed breakdown."
      isDark={isDark}
      title="Sort by"
      visible={visible}
      onClose={onClose}
    >
      <Text className={cx(TYPE.formLabel, 'mb-2 text-[#71717a] dark:text-[#a1a1aa]')}>Field</Text>
      {options.map((option) => (
        <OptionRow
          key={option.field}
          label={option.label}
          selected={selectedField === option.field}
          onPress={() => onFieldChange(option.field)}
        />
      ))}
      <Text className={cx(TYPE.formLabel, 'mb-2 mt-4 text-[#71717a] dark:text-[#a1a1aa]')}>
        Direction
      </Text>
      <OptionRow
        label="Ascending"
        selected={selectedDirection === 'asc'}
        onPress={() => onDirectionChange('asc')}
      />
      <OptionRow
        label="Descending"
        selected={selectedDirection === 'desc'}
        onPress={() => onDirectionChange('desc')}
      />
    </ReportOptionSheet>
  );
}
