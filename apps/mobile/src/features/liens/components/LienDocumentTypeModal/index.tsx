import { useEffect, useMemo, useState } from 'react';
import {
  Modal,
  Pressable,
  ScrollView,
  Text,
  TextInput,
  useWindowDimensions,
  View,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';

import type { LienDocumentType } from '@/shared/api/endpoints/Liens';
import { Spinner } from '@/shared/components/Spinner';
import { cx, FIGMA_TEXT } from '@/shared/styles';

export function LienDocumentTypeModal({
  error,
  isLoading = false,
  options,
  visible,
  onAddNew,
  onClose,
  onSelect,
}: {
  error?: boolean;
  isLoading?: boolean;
  options: LienDocumentType[];
  visible: boolean;
  onAddNew: () => void;
  onClose: () => void;
  onSelect: (documentType: LienDocumentType) => void;
}) {
  const { height } = useWindowDimensions();
  const [search, setSearch] = useState('');
  const [selectedId, setSelectedId] = useState('');

  useEffect(() => {
    if (!visible) {
      setSearch('');
      setSelectedId('');
    }
  }, [visible]);

  const filteredOptions = useMemo(() => {
    const normalized = search.trim().toLowerCase();
    if (!normalized) return options;
    return options.filter(
      (option) =>
        option.name.toLowerCase().includes(normalized) ||
        option.code.toLowerCase().includes(normalized)
    );
  }, [options, search]);

  function select(documentType: LienDocumentType) {
    setSelectedId(documentType.id);
    onSelect(documentType);
  }

  return (
    <Modal animationType="fade" transparent visible={visible} onRequestClose={onClose}>
      <View className="flex-1 justify-end bg-black/25 px-4 pb-4">
        <Pressable
          accessibilityLabel="Close Select Document Type"
          className="absolute inset-0"
          onPress={onClose}
        />
        <View
          className="w-full rounded-[24px] bg-white p-6 shadow-lg dark:bg-[#191a1f]"
          style={{ maxHeight: height * 0.78 }}
        >
          <View className="flex-row items-start gap-4">
            <View className="flex-1">
              <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-[16px] leading-6 text-[#18181b] dark:text-white')}>
                Select Document Type
              </Text>
              <Text className={cx(FIGMA_TEXT.body, 'mt-2 text-[#71717a] dark:text-[#a1a1aa]')}>
                Select the document type before uploading your file.
              </Text>
            </View>
            <Pressable
              accessibilityLabel="Close document type selector"
              accessibilityRole="button"
              className="h-7 w-7 items-center justify-center rounded-full bg-[#ebebec] dark:bg-[#2a2b30]"
              hitSlop={10}
              onPress={onClose}
            >
              <Ionicons color="#777984" name="close" size={20} />
            </Pressable>
          </View>

          <View className="mt-5 h-10 flex-row items-center rounded-[12px] border border-[#e4e4e7] px-3 dark:border-[#303138]">
            <Ionicons color="#777984" name="search-outline" size={18} />
            <TextInput
              accessibilityLabel="Search document types"
              className={cx(FIGMA_TEXT.input, 'ml-2 flex-1 text-[#18181b] dark:text-white')}
              placeholder="Search..."
              placeholderTextColor="#858892"
              value={search}
              onChangeText={setSearch}
            />
          </View>

          <ScrollView
            className="mt-4"
            contentContainerClassName="grow"
            nestedScrollEnabled
            showsVerticalScrollIndicator
            style={{ maxHeight: height * 0.38 }}
          >
            {isLoading ? (
              <View className="items-center py-8"><Spinner /></View>
            ) : filteredOptions.length ? (
              filteredOptions.map((option) => {
                const selected = option.id === selectedId;
                return (
                  <Pressable
                    accessibilityRole="button"
                    accessibilityState={{ selected }}
                    className="min-h-[44px] flex-row items-center border-b border-[#e4e4e7] py-3 dark:border-[#303138]"
                    key={option.id}
                    testID={`document-type-${option.id}`}
                    onPress={() => select(option)}
                  >
                    <Text className={cx(FIGMA_TEXT.body, 'flex-1 text-[#18181b] dark:text-white')}>
                      {option.name}
                    </Text>
                    {selected ? (
                      <View className="h-5 w-5 items-center justify-center rounded-md bg-[#f97332]">
                        <Ionicons color="white" name="checkmark" size={15} />
                      </View>
                    ) : null}
                  </Pressable>
                );
              })
            ) : (
              <Text className={cx(FIGMA_TEXT.body, 'py-8 text-center text-[#71717a] dark:text-[#a1a1aa]')}>
                {error ? 'Document types could not be loaded.' : 'No document types found.'}
              </Text>
            )}
          </ScrollView>

          <View className="mt-5 gap-3">
            <Pressable
              accessibilityRole="button"
              className="h-11 flex-row items-center justify-center gap-2 rounded-full border border-[#dedee0] dark:border-[#34353b]"
              onPress={onAddNew}
            >
              <Ionicons color="#18181b" name="add" size={22} />
              <Text className={cx(FIGMA_TEXT.cta, 'text-[#18181b] dark:text-white')}>
                Add New Document Type
              </Text>
            </Pressable>
            <Pressable
              accessibilityRole="button"
              className="h-11 items-center justify-center rounded-full bg-[#ebebec] dark:bg-[#2a2b30]"
              onPress={onClose}
            >
              <Text className={cx(FIGMA_TEXT.cta, 'text-[#b4531a] dark:text-[#f97332]')}>
                Cancel
              </Text>
            </Pressable>
          </View>
        </View>
      </View>
    </Modal>
  );
}
