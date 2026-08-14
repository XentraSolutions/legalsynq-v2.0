import { useEffect, useMemo, useState } from 'react';
import { Modal, Pressable, ScrollView, Text, useWindowDimensions, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

import { Input } from '@/shared/components/Input';
import { cx, FIGMA_TEXT } from '@/shared/styles';

export type SelectOptionItem = {
  label: string;
  value: string;
};

export interface SelectOptionModalProps {
  options: SelectOptionItem[];
  selectedLabel?: string;
  selectedValue?: string;
  title: string;
  visible: boolean;
  emptyMessage?: string;
  maxHeightRatio?: number;
  searchThreshold?: number;
  onClose: () => void;
  onSelect: (option: SelectOptionItem) => void;
}

export function SelectOptionModal({
  options,
  selectedLabel,
  selectedValue = '',
  title,
  visible,
  emptyMessage = 'No options are currently available.',
  maxHeightRatio = 0.6,
  searchThreshold = 20,
  onClose,
  onSelect,
}: SelectOptionModalProps) {
  const { height: windowHeight } = useWindowDimensions();
  const [search, setSearch] = useState('');

  useEffect(() => {
    if (!visible) setSearch('');
  }, [visible]);

  const orderedOptions = useMemo(() => {
    const allOptions = [...options];
    if (selectedValue && !allOptions.some((option) => option.value === selectedValue)) {
      allOptions.push({
        label: selectedLabel || selectedValue,
        value: selectedValue,
      });
    }

    const normalizedSearch = search.trim().toLowerCase();
    return allOptions
      .filter(
        (option) =>
          !normalizedSearch ||
          option.label.toLowerCase().includes(normalizedSearch) ||
          option.value.toLowerCase().includes(normalizedSearch)
      )
      .sort((left, right) => {
        if (left.value === selectedValue) return -1;
        if (right.value === selectedValue) return 1;
        return left.label.localeCompare(right.label);
      });
  }, [options, search, selectedLabel, selectedValue]);

  const showSearch = options.length > searchThreshold;

  return (
    <Modal animationType="fade" transparent visible={visible} onRequestClose={onClose}>
      <View className="flex-1 items-center justify-center bg-black/40 px-6">
        <Pressable
          accessibilityLabel={`Close ${title}`}
          className="absolute inset-0"
          onPress={onClose}
        />
        <View className="w-full rounded-[20px] bg-white p-6 dark:bg-[#191a1f]">
          <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-[#202228] dark:text-white')}>
            {title}
          </Text>
          {showSearch ? (
            <Input
              autoCapitalize="none"
              className="mt-4"
              placeholder={`Search ${title.toLowerCase()}...`}
              rightIcon={
                <View className="flex-row items-center gap-2">
                  {search ? (
                    <Pressable
                      accessibilityLabel="Clear search"
                      accessibilityRole="button"
                      hitSlop={10}
                      onPress={() => setSearch('')}
                    >
                      <Ionicons color="#71717a" name="close" size={18} />
                    </Pressable>
                  ) : null}
                  <Ionicons color="#71717a" name="search-outline" size={18} />
                </View>
              }
              value={search}
              onChangeText={setSearch}
            />
          ) : null}
          <ScrollView
            className={showSearch ? 'mt-3' : 'mt-4'}
            contentContainerClassName="grow"
            nestedScrollEnabled
            showsVerticalScrollIndicator
            style={{ maxHeight: windowHeight * maxHeightRatio }}
          >
            {orderedOptions.length > 0 ? (
              orderedOptions.map((option) => {
                const selected = option.value === selectedValue;
                return (
                  <Pressable
                    key={option.value}
                    accessibilityState={{ selected }}
                    accessibilityRole="button"
                    className="flex-row items-center border-b border-[#e4e4e7] py-4 dark:border-[#303138]"
                    testID={`select-option-${option.value}`}
                    onPress={() => onSelect(option)}
                  >
                    <Text className={cx(FIGMA_TEXT.body, 'flex-1 text-[#202228] dark:text-white')}>
                      {option.label}
                    </Text>
                    {selected ? (
                      <Ionicons color="#ee7132" name="checkmark" size={20} />
                    ) : null}
                  </Pressable>
                );
              })
            ) : (
              <Text className={cx(FIGMA_TEXT.body, 'py-4 text-[#777a84] dark:text-[#a1a1aa]')}>
                {emptyMessage}
              </Text>
            )}
          </ScrollView>
        </View>
      </View>
    </Modal>
  );
}
