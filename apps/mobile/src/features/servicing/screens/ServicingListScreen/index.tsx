import { useEffect, useRef, useState } from 'react';
import { FlatList, Pressable, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation } from '@react-navigation/native';
import type { NavigationProp } from '@react-navigation/native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { ServicingCaseCard } from '@/features/servicing/components';
import { useExportServicingCases, useServicingCases } from '@/features/servicing/hooks';
import type { ServicingCaseListItem } from '@/features/servicing/types/types';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { AppMenu } from '@/shared/components/AppMenu';
import { EmptyState } from '@/shared/components/EmptyState';
import { SearchBar } from '@/shared/components/SearchBar';
import { Spinner } from '@/shared/components/Spinner';
import { useToast } from '@/shared/hooks';
import { cx, FIGMA_TEXT } from '@/shared/styles';

const PAGE_SIZE = 5;

export function ServicingListScreen() {
  const listRef = useRef<FlatList<ServicingCaseListItem>>(null);
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const toast = useToast();
  const [menuVisible, setMenuVisible] = useState(false);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const servicingQuery = useServicingCases(search);
  const exportServicing = useExportServicingCases();
  const pageCount = Math.max(1, Math.ceil(servicingQuery.cases.length / PAGE_SIZE));
  const pageCases = servicingQuery.cases.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  useEffect(() => {
    if (page > pageCount) setPage(pageCount);
  }, [page, pageCount]);

  useEffect(() => {
    listRef.current?.scrollToOffset({ animated: page > 1, offset: 0 });
  }, [page]);

  async function handleExport() {
    try {
      await exportServicing.mutateAsync(servicingQuery.cases);
      toast.showSuccess('Servicing cases exported successfully');
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to export servicing cases');
    }
  }

  const isEmpty = !servicingQuery.isLoading && servicingQuery.totalCount === 0;
  const noMatches =
    !servicingQuery.isLoading && servicingQuery.totalCount > 0 && servicingQuery.cases.length === 0;

  return (
    <View className="flex-1 bg-[#fafafa] dark:bg-[#050506]">
      <SafeAreaView edges={['top']}>
        <View className="h-16 flex-row items-center justify-between px-6">
          <IconButton
            accessibilityLabel="Open menu"
            icon="menu-outline"
            onPress={() => setMenuVisible(true)}
          />
          <IconButton
            accessibilityLabel="Export servicing cases"
            disabled={servicingQuery.cases.length === 0 || exportServicing.isPending}
            icon="share-outline"
            onPress={() => void handleExport()}
          />
        </View>
      </SafeAreaView>

      <View className="px-6 pb-3 pt-1">
        <Text className="font-jakarta-bold text-[24px] leading-8 text-[#202228] dark:text-white">
          Servicing
        </Text>
        <Text className={cx(FIGMA_TEXT.body, 'mt-1 text-[#858892] dark:text-[#a1a1aa]')}>
          {servicingQuery.isLoading
            ? 'Loading servicing cases…'
            : `You have ${servicingQuery.totalCount} servicing cases. Stay on top of their progress and updates.`}
        </Text>
        <View className="mt-5">
          <SearchBar
            placeholder="Search..."
            value={search}
            onChangeText={(value) => {
              setSearch(value);
              setPage(1);
            }}
          />
        </View>
      </View>

      {servicingQuery.isLoading ? (
        <View className="flex-1 items-center justify-center">
          <Spinner />
          <Text className={cx(FIGMA_TEXT.body, 'mt-3 text-[#858892] dark:text-[#a1a1aa]')}>
            Loading servicing cases…
          </Text>
        </View>
      ) : servicingQuery.isError ? (
        <EmptyState
          actionLabel="Try Again"
          description={
            servicingQuery.error instanceof Error
              ? servicingQuery.error.message
              : 'The servicing list could not be loaded.'
          }
          icon={<Ionicons color="#f97332" name="alert-circle-outline" size={58} />}
          title="Unable to load servicing cases"
          onAction={() => void servicingQuery.refetchAll()}
        />
      ) : isEmpty ? (
        <EmptyState
          description="Cases will appear here when one or more liens are enabled for servicing."
          icon={<Ionicons color="#f97332" name="briefcase-outline" size={58} />}
          title="No servicing cases yet"
        />
      ) : noMatches ? (
        <EmptyState
          actionLabel="Clear Search"
          description="Try another client, case ID, status, or law firm."
          icon={<Ionicons color="#f97332" name="search-outline" size={58} />}
          title="No matching servicing cases"
          onAction={() => {
            setSearch('');
            setPage(1);
          }}
        />
      ) : (
        <FlatList
          ref={listRef}
          contentContainerClassName="gap-3 px-6 pb-8 pt-3"
          data={pageCases}
          keyExtractor={(item) => item.caseId}
          ListFooterComponent={
            <Pagination
              currentPage={page}
              pageCount={pageCount}
              totalEntries={servicingQuery.cases.length}
              visibleThrough={Math.min(page * PAGE_SIZE, servicingQuery.cases.length)}
              onNext={() => setPage((current) => Math.min(pageCount, current + 1))}
              onPrevious={() => setPage((current) => Math.max(1, current - 1))}
            />
          }
          refreshing={servicingQuery.isRefetching}
          renderItem={({ item }) => (
            <ServicingCaseCard
              caseItem={item}
              onPress={() =>
                navigation.navigate('CaseDetail', {
                  caseId: item.caseId,
                  initialTab: 'servicing',
                })
              }
            />
          )}
          showsVerticalScrollIndicator={false}
          onRefresh={() => void servicingQuery.refetchAll()}
        />
      )}

      <AppMenu visible={menuVisible} onClose={() => setMenuVisible(false)} />
    </View>
  );
}

function IconButton({
  accessibilityLabel,
  disabled,
  icon,
  onPress,
}: {
  accessibilityLabel: string;
  disabled?: boolean;
  icon: keyof typeof Ionicons.glyphMap;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityLabel={accessibilityLabel}
      accessibilityRole="button"
      className={cx(
        'h-10 w-10 items-center justify-center rounded-full bg-white shadow-sm dark:bg-[#191a1f]',
        disabled && 'opacity-50'
      )}
      disabled={disabled}
      onPress={onPress}
    >
      <Ionicons color="#777b85" name={icon} size={20} />
    </Pressable>
  );
}

function Pagination({
  currentPage,
  pageCount,
  totalEntries,
  visibleThrough,
  onNext,
  onPrevious,
}: {
  currentPage: number;
  pageCount: number;
  totalEntries: number;
  visibleThrough: number;
  onNext: () => void;
  onPrevious: () => void;
}) {
  return (
    <View className="mt-2 flex-row items-center pb-2">
      <Text className={cx(FIGMA_TEXT.formLabel, 'flex-1 text-[#8a8d96] dark:text-[#8f929b]')}>
        {visibleThrough} of {totalEntries} entries
      </Text>
      <PageButton disabled={currentPage === 1} label="Previous" onPress={onPrevious} />
      <View className="w-2" />
      <PageButton disabled={currentPage === pageCount} label="Next" onPress={onNext} />
    </View>
  );
}

function PageButton({
  disabled,
  label,
  onPress,
}: {
  disabled: boolean;
  label: string;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityLabel={`${label} page`}
      accessibilityRole="button"
      className={cx(
        'h-8 flex-row items-center rounded-2xl border border-[#dedee0] px-3 dark:border-[#34363d]',
        disabled && 'opacity-50'
      )}
      disabled={disabled}
      onPress={onPress}
    >
      {label === 'Previous' ? <Ionicons color="#777b85" name="chevron-back" size={14} /> : null}
      <Text className={cx(FIGMA_TEXT.formLabel, 'text-[#202228] dark:text-white')}>{label}</Text>
      {label === 'Next' ? <Ionicons color="#777b85" name="chevron-forward" size={14} /> : null}
    </Pressable>
  );
}
