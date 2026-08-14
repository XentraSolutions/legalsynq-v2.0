import { useEffect, useRef, useState } from 'react';
import { FlatList, Pressable, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation } from '@react-navigation/native';
import type { NavigationProp } from '@react-navigation/native';
import { SafeAreaView } from 'react-native-safe-area-context';

import {
  LienConfirmationModal,
  LienManagementFilterModal,
  ManagementLienCard,
} from '@/features/liens/components';
import { useExportLiens, useManagementLiens } from '@/features/liens/hooks';
import {
  EMPTY_LIEN_MANAGEMENT_FILTERS,
  type LienManagementFilters,
  type ManagementLienListItem,
} from '@/features/liens/types/types';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { AppMenu } from '@/shared/components/AppMenu';
import { EmptyState } from '@/shared/components/EmptyState';
import { SearchBar } from '@/shared/components/SearchBar';
import { Spinner } from '@/shared/components/Spinner';
import { useToast } from '@/shared/hooks';
import { cx, FIGMA_TEXT } from '@/shared/styles';

const PAGE_SIZE = 5;

function activeFilterCount(filters: LienManagementFilters): number {
  return [
    filters.purchaseStartDate || filters.purchaseEndDate,
    filters.closedStartDate || filters.closedEndDate,
    filters.lawFirmId,
    filters.medicalFacilityId,
    filters.caseManagerId,
    filters.statusId,
  ].filter(Boolean).length;
}

function legacyExportDate(value: string): string {
  const [year, month, day] = value.split('-');
  return year && month && day ? `${month}/${day}/${year}` : value;
}

export function MyLiensScreen() {
  const listRef = useRef<FlatList<ManagementLienListItem>>(null);
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const toast = useToast();
  const [search, setSearch] = useState('');
  const [filters, setFilters] = useState({ ...EMPTY_LIEN_MANAGEMENT_FILTERS });
  const [draftFilters, setDraftFilters] = useState({ ...EMPTY_LIEN_MANAGEMENT_FILTERS });
  const [filterVisible, setFilterVisible] = useState(false);
  const [exportVisible, setExportVisible] = useState(false);
  const [menuVisible, setMenuVisible] = useState(false);
  const [page, setPage] = useState(1);
  const liensQuery = useManagementLiens(search, filters);
  const exportLiens = useExportLiens();
  const filterCount = activeFilterCount(filters);
  const pageCount = Math.max(1, Math.ceil(liensQuery.liens.length / PAGE_SIZE));
  const pageLiens = liensQuery.liens.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  useEffect(() => {
    if (page > pageCount) setPage(pageCount);
  }, [page, pageCount]);

  useEffect(() => {
    listRef.current?.scrollToOffset({ animated: page > 1, offset: 0 });
  }, [page]);

  async function confirmExport() {
    try {
      await exportLiens.mutateAsync({
        lawFirmId: filters.lawFirmId || undefined,
        medicalFacilityId: filters.medicalFacilityId || undefined,
        caseManagerId: filters.caseManagerId || undefined,
        lienStatusId: filters.statusId || undefined,
        purchaseDate:
          filters.purchaseStartDate && filters.purchaseEndDate
            ? `${legacyExportDate(filters.purchaseStartDate)}-${legacyExportDate(filters.purchaseEndDate)}`
            : filters.purchaseStartDate
              ? legacyExportDate(filters.purchaseStartDate)
              : undefined,
      });
      setExportVisible(false);
      toast.showSuccess('Liens exported successfully');
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to export liens');
    }
  }

  const isEmpty = !liensQuery.isLoading && liensQuery.totalCount === 0;
  const noMatches = !liensQuery.isLoading && liensQuery.totalCount > 0 && liensQuery.liens.length === 0;

  return (
    <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <SafeAreaView edges={['top']}>
        <View className="h-16 flex-row items-center justify-between px-5">
          <IconButton accessibilityLabel="Open menu" icon="menu-outline" onPress={() => setMenuVisible(true)} />
          <View className="flex-row items-center gap-3">
            <IconButton
              accessibilityLabel="Export liens"
              disabled={liensQuery.liens.length === 0 || exportLiens.isPending}
              icon="share-outline"
              onPress={() => setExportVisible(true)}
            />
            <IconButton
              accent
              accessibilityLabel="Create lien"
              icon="add"
              onPress={() => navigation.navigate('CreateLien', {})}
            />
          </View>
        </View>
      </SafeAreaView>

      <View className="px-5 pb-3 pt-2">
        <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-[#202228] dark:text-white')}>Liens</Text>
        <Text className={cx(FIGMA_TEXT.body, 'mt-1 text-[#858892] dark:text-[#a1a1aa]')}>
          {liensQuery.isLoading
            ? 'Loading your liens…'
            : `You have a total of ${liensQuery.totalCount} liens. Keep track of your liens and monitor their progress.`}
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
            accessibilityLabel="Filter liens"
            badge={filterCount}
            icon="options-outline"
            onPress={() => {
              setDraftFilters({ ...filters });
              setFilterVisible(true);
            }}
          />
        </View>
      </View>

      {liensQuery.isLoading ? (
        <View className="flex-1 items-center justify-center">
          <Spinner />
          <Text className={cx(FIGMA_TEXT.body, 'mt-3 text-[#6f737d] dark:text-[#a1a1aa]')}>
            Loading liens…
          </Text>
        </View>
      ) : liensQuery.isError ? (
        <EmptyState
          actionLabel="Try Again"
          description={liensQuery.error instanceof Error ? liensQuery.error.message : 'The lien list could not be loaded.'}
          icon={<Ionicons color="#f97332" name="alert-circle-outline" size={58} />}
          title="Unable to load liens"
          onAction={() => void liensQuery.refetchAll()}
        />
      ) : isEmpty ? (
        <EmptyState
          actionLabel="Create Lien"
          description="Create your first lien to start tracking its progress."
          icon={<Ionicons color="#f97332" name="documents-outline" size={60} />}
          title="No liens yet"
          onAction={() => navigation.navigate('CreateLien', {})}
        />
      ) : noMatches ? (
        <EmptyState
          actionLabel="Clear Filters"
          description="Try another search or remove the active filters."
          icon={<Ionicons color="#f97332" name="search-outline" size={58} />}
          title="No matching liens"
          onAction={() => {
            setSearch('');
            setFilters({ ...EMPTY_LIEN_MANAGEMENT_FILTERS });
            setPage(1);
          }}
        />
      ) : (
        <FlatList
          ref={listRef}
          contentContainerClassName="gap-4 px-5 pb-8 pt-3"
          data={pageLiens}
          keyExtractor={(item) => item.id}
          ListFooterComponent={
            <Pagination
              currentPage={page}
              pageCount={pageCount}
              totalEntries={liensQuery.liens.length}
              visibleThrough={Math.min(page * PAGE_SIZE, liensQuery.liens.length)}
              onNext={() => setPage((current) => Math.min(pageCount, current + 1))}
              onPrevious={() => setPage((current) => Math.max(1, current - 1))}
            />
          }
          refreshing={liensQuery.isRefetching}
          renderItem={({ item }) => (
            <ManagementLienCard
              lien={item}
              onPress={() => navigation.navigate('ManagementLienDetail', { lienId: item.id })}
            />
          )}
          showsVerticalScrollIndicator={false}
          onRefresh={() => void liensQuery.refetchAll()}
        />
      )}

      <LienManagementFilterModal
        draft={draftFilters}
        options={liensQuery.filterOptions}
        visible={filterVisible}
        onApply={() => {
          setFilters({ ...draftFilters });
          setFilterVisible(false);
          setPage(1);
        }}
        onChange={setDraftFilters}
        onClose={() => setFilterVisible(false)}
        onReset={() => setDraftFilters({ ...EMPTY_LIEN_MANAGEMENT_FILTERS })}
      />
      <LienConfirmationModal
        confirmLabel="Yes, Export"
        description="Are you sure you want to export all matching liens? A CSV file will be generated for download."
        loading={exportLiens.isPending}
        title="Export All Liens?"
        visible={exportVisible}
        onCancel={() => setExportVisible(false)}
        onConfirm={() => void confirmExport()}
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
      <PageButton disabled={currentPage === 1} label="Previous" onPress={onPrevious} />
      <View className="w-2" />
      <PageButton disabled={currentPage === pageCount} label="Next" onPress={onNext} />
    </View>
  );
}

function PageButton({ disabled, label, onPress }: { disabled: boolean; label: string; onPress: () => void }) {
  return (
    <Pressable
      accessibilityLabel={`${label} page`}
      accessibilityRole="button"
      className={cx(
        'h-10 items-center justify-center rounded-full border border-[#e5e6e8] bg-white px-3 dark:border-[#2d2e34] dark:bg-[#191a1f]',
        disabled && 'opacity-50'
      )}
      disabled={disabled}
      onPress={onPress}
    >
      <Text className={cx(FIGMA_TEXT.formLabel, 'text-[#34363c] dark:text-white')}>{label}</Text>
    </Pressable>
  );
}
