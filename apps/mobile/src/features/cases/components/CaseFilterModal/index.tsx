import { ScrollView, Text, View } from 'react-native';

import type {
  CaseFilterKey,
  CaseFilterOptions,
  CaseFilters,
} from '@/features/cases/types/types';
import { Button } from '@/shared/components/Button';
import { Chip } from '@/shared/components/Chip';
import { Modal } from '@/shared/components/Modal';
import { cx, FIGMA_TEXT } from '@/shared/styles';

type FilterSection = {
  key: CaseFilterKey;
  label: string;
};

const FILTER_SECTIONS: FilterSection[] = [
  { key: 'lawFirmId', label: 'Law Firm' },
  { key: 'accidentTypeId', label: 'Accident Type' },
  { key: 'caseManagerId', label: 'Case Manager' },
  { key: 'statusId', label: 'Status' },
];

export function CaseFilterModal({
  draft,
  options,
  visible,
  onApply,
  onChange,
  onClose,
  onReset,
}: {
  draft: CaseFilters;
  options: CaseFilterOptions;
  visible: boolean;
  onApply: () => void;
  onChange: (key: CaseFilterKey, value: string) => void;
  onClose: () => void;
  onReset: () => void;
}) {
  return (
    <Modal
      footer={
        <View className="flex-row gap-3">
          <Button className="flex-1" label="Reset" variant="secondary" onPress={onReset} />
          <Button className="flex-1" label="Apply Filters" onPress={onApply} />
        </View>
      }
      title="Filter Cases"
      visible={visible}
      onClose={onClose}
    >
      <Text className={cx(FIGMA_TEXT.body, 'mb-4 text-[#6f737d] dark:text-[#a1a1aa]')}>
        Narrow down cases using one or more filters.
      </Text>
      <ScrollView className="max-h-[440px]" showsVerticalScrollIndicator={false}>
        <View className="gap-5">
          {FILTER_SECTIONS.map((section) => (
            <View key={section.key}>
              <Text className={cx(FIGMA_TEXT.formLabel, 'mb-2 text-[#6f737d] dark:text-[#a1a1aa]')}>
                {section.label}
              </Text>
              {options[section.key].length > 0 ? (
                <View className="flex-row flex-wrap gap-2">
                  {options[section.key].map((option) => (
                    <Chip
                      key={option.id}
                      label={option.label}
                      selected={draft[section.key] === option.id}
                      onPress={() =>
                        onChange(
                          section.key,
                          draft[section.key] === option.id ? '' : option.id
                        )
                      }
                    />
                  ))}
                </View>
              ) : (
                <Text className={cx(FIGMA_TEXT.body, 'text-[#9699a2] dark:text-[#777984]')}>
                  No options available
                </Text>
              )}
            </View>
          ))}
        </View>
      </ScrollView>
    </Modal>
  );
}
