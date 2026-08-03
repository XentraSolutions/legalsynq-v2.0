import { ScrollView, Text, useColorScheme, View } from 'react-native';

import type {
  LienManagementFilterKey,
  LienManagementFilterOptions,
  LienManagementFilters,
} from '@/features/liens/types/types';
import { Button } from '@/shared/components/Button';
import { Chip } from '@/shared/components/Chip';
import { DateRangePicker } from '@/shared/components/DateRangePicker';
import { Modal } from '@/shared/components/Modal';
import { cx, FIGMA_TEXT } from '@/shared/styles';

const SECTIONS: Array<{ key: LienManagementFilterKey; label: string }> = [
  { key: 'lawFirmId', label: 'Law Firm' },
  { key: 'medicalFacilityId', label: 'Medical Facility' },
  { key: 'caseManagerId', label: 'Case Manager' },
  { key: 'statusId', label: 'Lien Status' },
];

function pickerDate(value: string): string {
  if (!value) return '';
  const parts = value.slice(0, 10).split('-');
  return parts.length === 3 ? `${parts[1]}/${parts[2]}/${parts[0]}` : value;
}

function isoDate(value: string): string {
  if (!value) return '';
  const parts = value.split('/');
  return parts.length === 3 ? `${parts[2]}-${parts[0]}-${parts[1]}` : value;
}

export function LienManagementFilterModal({
  draft,
  options,
  visible,
  onApply,
  onChange,
  onClose,
  onReset,
}: {
  draft: LienManagementFilters;
  options: LienManagementFilterOptions;
  visible: boolean;
  onApply: () => void;
  onChange: (filters: LienManagementFilters) => void;
  onClose: () => void;
  onReset: () => void;
}) {
  const isDark = useColorScheme() === 'dark';
  return (
    <Modal
      footer={
        <View className="flex-row gap-3">
          <Button className="flex-1" label="Clear Filter" variant="secondary" onPress={onReset} />
          <Button className="flex-1" label="Apply Filters" onPress={onApply} />
        </View>
      }
      title="Filter Liens"
      visible={visible}
      onClose={onClose}
    >
      <Text className={cx(FIGMA_TEXT.body, 'mb-4 text-[#6f737d] dark:text-[#a1a1aa]')}>
        Apply one or more filters to narrow down the available lien records.
      </Text>
      <ScrollView className="max-h-[500px]" showsVerticalScrollIndicator={false}>
        <View className="gap-5 pb-2">
          <DateRangePicker
            fieldLabel="Purchase Start - End Date"
            isDark={isDark}
            value={{
              startDate: pickerDate(draft.purchaseStartDate),
              endDate: pickerDate(draft.purchaseEndDate),
            }}
            onChange={(range) =>
              onChange({
                ...draft,
                purchaseStartDate: isoDate(range.startDate),
                purchaseEndDate: isoDate(range.endDate),
              })
            }
          />
          <DateRangePicker
            fieldLabel="Closed Start - End Date"
            isDark={isDark}
            value={{
              startDate: pickerDate(draft.closedStartDate),
              endDate: pickerDate(draft.closedEndDate),
            }}
            onChange={(range) =>
              onChange({
                ...draft,
                closedStartDate: isoDate(range.startDate),
                closedEndDate: isoDate(range.endDate),
              })
            }
          />
          {SECTIONS.map((section) => (
            <View key={section.key}>
              <Text className={cx(FIGMA_TEXT.formLabel, 'mb-2 text-[#202228] dark:text-white')}>
                {section.label}
              </Text>
              <View className="flex-row flex-wrap gap-2">
                {options[section.key].map((option) => (
                  <Chip
                    key={option.id}
                    label={option.label}
                    selected={draft[section.key] === option.id}
                    onPress={() =>
                      onChange({
                        ...draft,
                        [section.key]: draft[section.key] === option.id ? '' : option.id,
                      })
                    }
                  />
                ))}
              </View>
            </View>
          ))}
        </View>
      </ScrollView>
    </Modal>
  );
}
