import { useEffect, useRef, useState } from 'react';
import { FlatList, Pressable, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation } from '@react-navigation/native';
import type { NavigationProp } from '@react-navigation/native';

import { CaseCard, CaseFilterModal } from '@/features/cases/components';
import { useCases, useExportCases } from '@/features/cases/hooks';
import {
  EMPTY_CASE_FILTERS,
  type CaseFilterKey,
  type CaseFilters,
  type CaseListItem,
} from '@/features/cases/types/types';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { AppMenu } from '@/shared/components/AppMenu';
import { EmptyState } from '@/shared/components/EmptyState';
import { SearchBar } from '@/shared/components/SearchBar';
import { Spinner } from '@/shared/components/Spinner';
import { useToast } from '@/shared/hooks';
import { cx, FIGMA_TEXT } from '@/shared/styles';

const PAGE_SIZE = 5;

function activeFilterCount(filters: CaseFilters): number {
  return Object.values(filters).filter(Boolean).length;
}

export function CasesListScreen() {
  const listRef = useRef<FlatList<CaseListItem>>(null);
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const toast = useToast();
  const [search, setSearch] = useState('');
  const [filters, setFilters] = useState<CaseFilters>({ ...EMPTY_CASE_FILTERS });
  const [draftFilters, setDraftFilters] = useState<CaseFilters>({ ...EMPTY_CASE_FILTERS });
  const [filterVisible, setFilterVisible] = useState(false);
  const [menuVisible, setMenuVisible] = useState(false);
  const [page, setPage] = useState(1);
  const casesQuery = useCases(search, filters);
  const exportCases = useExportCases();
  const filterCount = activeFilterCount(filters);
  const pageCount = Math.max(1, Math.ceil(casesQuery.cases.length / PAGE_SIZE));
  const pageCases = casesQuery.cases.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  useEffect(() => {
    if (page > pageCount) setPage(pageCount);
  }, [page, pageCount]);

  useEffect(() => {
    listRef.current?.scrollToOffset({ animated: page > 1, offset: 0 });
  }, [page]);

  function openFilters() {
    setDraftFilters({ ...filters });
    setFilterVisible(true);
  }

  async function handleExport() {
    try {
      await exportCases.mutateAsync({
        keyword: search.trim() || undefined,
        lawFirmId: filters.lawFirmId || undefined,
        accidentTypeId: filters.accidentTypeId || undefined,
        caseManagerId: filters.caseManagerId || undefined,
        statusId: filters.statusId || undefined,
      });
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to export cases');
    }
  }

  const isEmpty = !casesQuery.isLoading && casesQuery.totalCount === 0;
  const hasNoMatches = !casesQuery.isLoading && casesQuery.totalCount > 0 && casesQuery.cases.length === 0;

  return (
    <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <SafeAreaView edges={['top']}>
        <View className="h-16 flex-row items-center justify-between px-5">
          <IconButton
            accessibilityLabel="Open menu"
            icon="menu-outline"
            onPress={() => setMenuVisible(true)}
          />
          <View className="flex-row items-center gap-3">
            <IconButton
              accessibilityLabel="Export cases"
              disabled={casesQuery.cases.length === 0 || exportCases.isPending}
              icon="share-outline"
              onPress={handleExport}
            />
            <IconButton
              accent
              accessibilityLabel="Create case"
              icon="add"
              onPress={() => navigation.navigate('CreateCase')}
            />
          </View>
        </View>
      </SafeAreaView>

      <View className="px-5 pb-3 pt-2">
        <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-[#202228] dark:text-white')}>
          All Cases
        </Text>
        <Text className={cx(FIGMA_TEXT.body, 'mt-1 text-[#858892] dark:text-[#a1a1aa]')}>
          {casesQuery.isLoading
            ? 'Loading your legal matters…'
            : `You have a total of ${casesQuery.totalCount} cases. Stay on top of your legal matters.`}
        </Text>
        <View className="mt-6 flex-row items-center gap-3">
          <View className="flex-1">
            <SearchBar
              placeholder="Search..."
              value={search}
              onChangeText={(value) => {
                setSearch(value);
                setPage(1);
              }}
            />
          </View>
          <IconButton
            accessibilityLabel="Filter cases"
            badge={filterCount}
            icon="options-outline"
            onPress={openFilters}
          />
        </View>
      </View>

      {casesQuery.isLoading ? (
        <View className="flex-1 items-center justify-center">
          <Spinner />
          <Text className={cx(FIGMA_TEXT.body, 'mt-3 text-[#6f737d] dark:text-[#a1a1aa]')}>
            Loading cases…
          </Text>
        </View>
      ) : casesQuery.isError ? (
        <EmptyState
          actionLabel="Try Again"
          description={
            casesQuery.error instanceof Error
              ? casesQuery.error.message
              : 'The case list could not be loaded.'
          }
          icon={<Ionicons color="#f97332" name="alert-circle-outline" size={58} />}
          title="Unable to load cases"
          onAction={() => void casesQuery.refetch()}
        />
      ) : isEmpty ? (
        <EmptyState
          actionLabel="Create Case"
          description="Create your first case to start tracking legal matters and liens."
          icon={<Ionicons color="#f97332" name="folder-open-outline" size={60} />}
          title="No cases yet"
          onAction={() => navigation.navigate('CreateCase')}
        />
      ) : hasNoMatches ? (
        <EmptyState
          actionLabel="Clear Filters"
          description="Try another search or remove the active filters."
          icon={<Ionicons color="#f97332" name="search-outline" size={58} />}
          title="No matching cases"
          onAction={() => {
            setSearch('');
            setFilters({ ...EMPTY_CASE_FILTERS });
            setPage(1);
          }}
        />
      ) : (
        <FlatList
          ref={listRef}
          contentContainerClassName="gap-4 px-5 pb-8 pt-3"
          data={pageCases}
          keyExtractor={(item) => item.id}
          ListFooterComponent={
            <Pagination
              currentPage={page}
              pageCount={pageCount}
              visibleThrough={Math.min(page * PAGE_SIZE, casesQuery.cases.length)}
              totalEntries={casesQuery.cases.length}
              onNext={() => setPage((current) => Math.min(pageCount, current + 1))}
              onPrevious={() => setPage((current) => Math.max(1, current - 1))}
            />
          }
          refreshing={casesQuery.isRefetching}
          renderItem={({ item }) => (
            <CaseCard
              caseItem={item}
              onPress={() => navigation.navigate('CaseDetail', { caseId: item.id })}
            />
          )}
          showsVerticalScrollIndicator={false}
          onRefresh={() => void casesQuery.refetch()}
        />
      )}

      <CaseFilterModal
        draft={draftFilters}
        options={casesQuery.filterOptions}
        visible={filterVisible}
        onApply={() => {
          setFilters({ ...draftFilters });
          setFilterVisible(false);
          setPage(1);
        }}
        onChange={(key: CaseFilterKey, value: string) =>
          setDraftFilters((current) => ({ ...current, [key]: value }))
        }
        onClose={() => setFilterVisible(false)}
        onReset={() => setDraftFilters({ ...EMPTY_CASE_FILTERS })}
      />
      <AppMenu visible={menuVisible} onClose={() => setMenuVisible(false)} />
    </View>
  );
}

function IconButton({
  accent,
  accessibilityLabel,
  badge,
  disabled,
  icon,
  onPress,
}: {
  accent?: boolean;
  accessibilityLabel: string;
  badge?: number;
  disabled?: boolean;
  icon: keyof typeof Ionicons.glyphMap;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityLabel={accessibilityLabel}
      accessibilityRole="button"
      className={cx(
        'h-11 w-11 items-center justify-center rounded-full shadow-sm',
        accent ? 'bg-[#f97332]' : 'bg-white dark:bg-[#191a1f]',
        disabled && 'opacity-50'
      )}
      disabled={disabled}
      onPress={onPress}
    >
      <Ionicons color={accent ? '#ffffff' : '#777b85'} name={icon} size={22} />
      {badge ? (
        <View className="absolute -right-1 -top-1 min-w-[18px] items-center rounded-full bg-[#f97332] px-1 py-0.5">
          <Text className={cx(FIGMA_TEXT.microMeta, 'text-white')}>{badge}</Text>
        </View>
      ) : null}
    </Pressable>
  );
}

function Pagination({
  currentPage,
  pageCount,
  visibleThrough,
  totalEntries,
  onNext,
  onPrevious,
}: {
  currentPage: number;
  pageCount: number;
  visibleThrough: number;
  totalEntries: number;
  onNext: () => void;
  onPrevious: () => void;
}) {
  return (
    <View className="mt-3 flex-row items-center pb-2">
      <Text className={cx(FIGMA_TEXT.formLabel, 'flex-1 text-[#8a8d96] dark:text-[#8f929b]')}>
        {visibleThrough} of {totalEntries} entries
      </Text>
      <PaginationButton
        disabled={currentPage === 1}
        icon="chevron-back"
        label="Previous"
        onPress={onPrevious}
      />
      <View className="w-2" />
      <PaginationButton
        disabled={currentPage === pageCount}
        icon="chevron-forward"
        iconAfter
        label="Next"
        onPress={onNext}
      />
    </View>
  );
}

function PaginationButton({
  disabled,
  icon,
  iconAfter,
  label,
  onPress,
}: {
  disabled: boolean;
  icon: keyof typeof Ionicons.glyphMap;
  iconAfter?: boolean;
  label: string;
  onPress: () => void;
}) {
  const glyph = <Ionicons color={disabled ? '#a8abb2' : '#34363c'} name={icon} size={16} />;

  return (
    <Pressable
      accessibilityLabel={`${label} page`}
      accessibilityRole="button"
      className={cx(
        'h-10 flex-row items-center gap-1 rounded-full border border-[#e5e6e8] bg-white px-3 dark:border-[#2d2e34] dark:bg-[#191a1f]',
        disabled && 'opacity-50'
      )}
      disabled={disabled}
      onPress={onPress}
    >
      {!iconAfter ? glyph : null}
      <Text className={cx(FIGMA_TEXT.formLabel, 'text-[#34363c] dark:text-white')}>{label}</Text>
      {iconAfter ? glyph : null}
    </Pressable>
  );
}
