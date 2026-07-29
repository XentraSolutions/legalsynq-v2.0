import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { Modal, Pressable, ScrollView, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, useRoute, type RouteProp } from '@react-navigation/native';
import { useColorScheme as useNativeWindColorScheme } from 'nativewind';

import {
  useDashboardLawFirmCaseReport,
  useDashboardMedicalProviderReport,
  useDashboardTotalCaseReport,
  useDashboardTotalLienReport,
} from '@/features/dashboard/hooks';
import type { DashboardReportType } from '@/features/dashboard/types/types';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { SearchBar } from '@/shared/components/SearchBar';
import type {
  DashboardLawFirmCaseReportRow,
  DashboardMedicalProviderReportRow,
  DashboardTotalCaseReportRow,
  DashboardTotalLienReportRow,
  ReportFilterRequest,
} from '@/shared/api/endpoints/Cases';
import { useDashboardSettings } from '@/shared/hooks/useDashboardSettings';
import { cx, FIGMA_COLORS, FIGMA_TEXT as TYPE } from '@/shared/styles';

type DetailRoute = RouteProp<MainStackParamList, 'DashboardReportDetail'>;

type StatusTone = 'success' | 'warning' | 'danger' | 'info';

type BreakdownItem = {
  id: string;
  key: string;
  status: string;
  statusColor?: string;
  statusTone?: StatusTone;
  showStatus?: boolean;
  fields: Array<{
    icon: keyof typeof Ionicons.glyphMap;
    label: string;
    value: string;
  }>;
};

type ReportModel = {
  title: string;
  subtitle: string;
  breakdownTitle: string;
  breakdownItems: BreakdownItem[];
};

type ReportPaginationMeta = {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

type BreakdownSortField = 'caseId' | 'dateOfLoss' | 'entity' | 'name' | 'plaintiff' | 'status';

type BreakdownSortDirection = 'asc' | 'desc';

type BreakdownSortOption = {
  field: BreakdownSortField;
  label: string;
};

type BreakdownFilterOption = {
  id: string;
  label: string;
};

const DETAIL_PAGE_SIZE = 5;
const DETAIL_FILTER_LIMIT = 10000;
const ALL_FILTER_ID = 'all';

const LIEN_BREAKDOWN: BreakdownItem[] = [
  createLienBreakdownItem('84517', 'Close', '26-42803', 'Sarah Kimura'),
  createLienBreakdownItem('63290', 'Close', '26-58114', 'James Okonkwo'),
  createLienBreakdownItem('91638', 'Open', '26-31951', 'Marcus Delgado'),
  createLienBreakdownItem('47826', 'Close', '26-63927', 'Elena Vasquez'),
  createLienBreakdownItem('55093', 'Open', '26-49381', 'Thomas Brewer'),
];

function buildDashboardReportFilter(
  dateRange: { endDate: string; startDate: string },
  page: number,
  limit = DETAIL_PAGE_SIZE
): ReportFilterRequest {
  return {
    page,
    limit,
    startDate: dateRange.startDate,
    endDate: dateRange.endDate,
  };
}

function normalizePagination(
  pagination: ReportPaginationMeta | undefined,
  fallbackPage: number
): ReportPaginationMeta {
  const totalCount = Math.max(0, pagination?.totalCount ?? 0);
  const pageSize = Math.max(1, pagination?.pageSize ?? DETAIL_PAGE_SIZE);
  const totalPages = Math.max(1, (pagination?.totalPages ?? Math.ceil(totalCount / pageSize)) || 1);
  const page = Math.min(Math.max(1, pagination?.page ?? fallbackPage), totalPages);

  return { page, pageSize, totalCount, totalPages };
}

function buildPaginationPageList(current: number, total: number): Array<number | 'ellipsis'> {
  const pages = Array.from(new Set([1, current, total])).sort((left, right) => left - right);
  const result: Array<number | 'ellipsis'> = [];

  pages.forEach((page, index) => {
    if (index > 0 && page - pages[index - 1] > 1) {
      result.push('ellipsis');
    }
    result.push(page);
  });

  return result;
}

function normalizeFilterId(value: string): string {
  return value.trim().toLowerCase();
}

function buildBreakdownKey(prefix: string, index: number, ...parts: string[]): string {
  const normalizedParts = parts
    .map((part) => normalizeFilterId(part).replace(/[^a-z0-9]+/g, '-'))
    .filter(Boolean)
    .join('-');

  return `${prefix}-${index}-${normalizedParts || 'row'}`;
}

function getBreakdownFieldValue(item: BreakdownItem, labels: string[]): string {
  const normalizedLabels = labels.map((label) => normalizeFilterId(label));
  return (
    item.fields.find((field) => normalizedLabels.includes(normalizeFilterId(field.label)))?.value ??
    ''
  );
}

function getBreakdownFilterLabel(reportType: DashboardReportType): string {
  if (reportType === 'total-liens') return 'Lien status';
  if (reportType === 'total-cases') return 'Case status';
  if (reportType === 'law-firm-allocation') return 'Law firm';
  return 'Medical facility';
}

function getSearchPlaceholder(reportType: DashboardReportType): string {
  if (reportType === 'total-liens') return 'Search liens';
  if (reportType === 'total-cases') return 'Search cases';
  if (reportType === 'law-firm-allocation') return 'Search law firm cases';
  return 'Search medical facility cases';
}

function getBreakdownFilterValue(reportType: DashboardReportType, item: BreakdownItem): string {
  if (reportType === 'total-liens' || reportType === 'total-cases') {
    return item.status;
  }

  if (reportType === 'law-firm-allocation') {
    return getBreakdownFieldValue(item, ['Law Firm']);
  }

  return getBreakdownFieldValue(item, ['Medical Facility', 'MedicalFacility']);
}

function getBreakdownFilterOptions(
  reportType: DashboardReportType,
  items: BreakdownItem[]
): BreakdownFilterOption[] {
  const options = new Map<string, string>();

  for (const item of items) {
    const value = getBreakdownFilterValue(reportType, item);
    if (value && value !== 'N/A') {
      options.set(normalizeFilterId(value), value);
    }
  }

  return [
    { id: ALL_FILTER_ID, label: 'All' },
    ...Array.from(options.entries())
      .sort((left, right) => left[1].localeCompare(right[1], undefined, { sensitivity: 'base' }))
      .map(([id, label]) => ({ id, label })),
  ];
}

function getSelectableFilterIds(options: BreakdownFilterOption[]): string[] {
  return options.filter((option) => option.id !== ALL_FILTER_ID).map((option) => option.id);
}

function toggleSelectedFilterId(
  selectedFilterIds: string[],
  filterId: string,
  options: BreakdownFilterOption[]
): string[] {
  if (filterId === ALL_FILTER_ID) {
    return [ALL_FILTER_ID];
  }

  const selectableFilterIds = getSelectableFilterIds(options);
  const currentSelection = selectedFilterIds.includes(ALL_FILTER_ID)
    ? []
    : selectedFilterIds.filter((id) => selectableFilterIds.includes(id));
  const nextSelection = currentSelection.includes(filterId)
    ? currentSelection.filter((id) => id !== filterId)
    : [...currentSelection, filterId];

  if (nextSelection.length === 0 || nextSelection.length === selectableFilterIds.length) {
    return [ALL_FILTER_ID];
  }

  return nextSelection;
}

function getBreakdownSortOptions(reportType: DashboardReportType): BreakdownSortOption[] {
  if (reportType === 'total-liens') {
    return [
      { field: 'name', label: 'Lien ID' },
      { field: 'status', label: 'Lien Status' },
      { field: 'caseId', label: 'Case ID' },
      { field: 'plaintiff', label: 'Plaintiff' },
    ];
  }

  if (reportType === 'total-cases') {
    return [
      { field: 'name', label: 'Client' },
      { field: 'status', label: 'Case Status' },
      { field: 'caseId', label: 'Case ID' },
      { field: 'dateOfLoss', label: 'Date of Loss' },
    ];
  }

  return [
    { field: 'name', label: 'Client' },
    {
      field: 'entity',
      label: reportType === 'law-firm-allocation' ? 'Law Firm' : 'Medical Facility',
    },
    { field: 'caseId', label: 'Case ID' },
    { field: 'dateOfLoss', label: 'Date of Loss' },
  ];
}

function getSortValue(item: BreakdownItem, field: BreakdownSortField): string {
  if (field === 'name') return item.id;
  if (field === 'status') return item.status;
  if (field === 'caseId') return getBreakdownFieldValue(item, ['Case ID']);
  if (field === 'dateOfLoss') return getBreakdownFieldValue(item, ['Date of Loss']);
  if (field === 'plaintiff') return getBreakdownFieldValue(item, ['Plaintiff Name']);
  return getBreakdownFieldValue(item, ['Law Firm', 'Medical Facility', 'MedicalFacility']);
}

function matchesSearchQuery(item: BreakdownItem, query: string): boolean {
  const normalizedQuery = query.trim().toLowerCase();
  if (!normalizedQuery) return true;

  const searchableText = [
    item.id,
    item.status,
    ...item.fields.flatMap((field) => [field.label, field.value]),
  ]
    .join(' ')
    .toLowerCase();

  return searchableText.includes(normalizedQuery);
}

function filterAndSortBreakdownItems({
  filterIds,
  items,
  query,
  reportType,
  sortDirection,
  sortField,
}: {
  filterIds: string[];
  items: BreakdownItem[];
  query: string;
  reportType: DashboardReportType;
  sortDirection: BreakdownSortDirection;
  sortField: BreakdownSortField;
}): BreakdownItem[] {
  const filtered = items.filter((item) => {
    const filterMatches =
      filterIds.length === 0 ||
      filterIds.includes(ALL_FILTER_ID) ||
      filterIds.includes(normalizeFilterId(getBreakdownFilterValue(reportType, item)));
    return filterMatches && matchesSearchQuery(item, query);
  });

  return filtered.sort((left, right) => {
    const comparison = getSortValue(left, sortField).localeCompare(
      getSortValue(right, sortField),
      undefined,
      {
        numeric: true,
        sensitivity: 'base',
      }
    );
    return sortDirection === 'asc' ? comparison : -comparison;
  });
}

export function DashboardReportDetailScreen() {
  const navigation = useNavigation();
  const route = useRoute<DetailRoute>();
  const { colorScheme } = useNativeWindColorScheme();
  const isDark = colorScheme === 'dark';
  const { hydrated: dashboardSettingsHydrated, settings: dashboardSettings } =
    useDashboardSettings();
  const useDashboardDummyData = dashboardSettings.useDummyData;
  const reportsEnabled = dashboardSettingsHydrated && !useDashboardDummyData;
  const [currentPage, setCurrentPage] = useState(1);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedFilterIds, setSelectedFilterIds] = useState<string[]>([ALL_FILTER_ID]);
  const [sortField, setSortField] = useState<BreakdownSortField>('name');
  const [sortDirection, setSortDirection] = useState<BreakdownSortDirection>('asc');
  const [filterSheetVisible, setFilterSheetVisible] = useState(false);
  const [sortSheetVisible, setSortSheetVisible] = useState(false);

  useEffect(() => {
    setCurrentPage(1);
    setSearchQuery('');
    setSelectedFilterIds([ALL_FILTER_ID]);
    setSortField('name');
    setSortDirection('asc');
  }, [route.params.dateRange.endDate, route.params.dateRange.startDate, route.params.reportType]);

  useEffect(() => {
    setCurrentPage(1);
  }, [searchQuery, selectedFilterIds, sortDirection, sortField]);

  const reportFilter = useMemo(
    () => buildDashboardReportFilter(route.params.dateRange, 1, DETAIL_FILTER_LIMIT),
    [route.params.dateRange]
  );
  const { data: totalLienReport } = useDashboardTotalLienReport(reportFilter, reportsEnabled);
  const { data: totalCaseReport } = useDashboardTotalCaseReport(reportFilter, reportsEnabled);
  const { data: lawFirmReport } = useDashboardLawFirmCaseReport(reportFilter, reportsEnabled);
  const { data: medicalProviderReport } = useDashboardMedicalProviderReport(
    reportFilter,
    reportsEnabled
  );
  const report = useMemo(
    () =>
      buildReport(
        route.params.reportType,
        totalLienReport?.items ?? [],
        totalCaseReport?.items ?? [],
        lawFirmReport?.items ?? [],
        medicalProviderReport?.items ?? [],
        {
          totalLiens: totalLienReport?.totalCount ?? 0,
          totalCases: totalCaseReport?.totalCount ?? 0,
          totalLawFirmCases: lawFirmReport?.totalCount ?? 0,
          totalMedicalFacilityCases: medicalProviderReport?.totalCount ?? 0,
        },
        useDashboardDummyData
      ),
    [
      lawFirmReport?.items,
      lawFirmReport?.totalCount,
      medicalProviderReport?.items,
      medicalProviderReport?.totalCount,
      route.params.reportType,
      totalCaseReport?.items,
      totalCaseReport?.totalCount,
      totalLienReport?.items,
      totalLienReport?.totalCount,
      useDashboardDummyData,
    ]
  );
  const filterLabel = getBreakdownFilterLabel(route.params.reportType);
  const filterOptions = useMemo(
    () => getBreakdownFilterOptions(route.params.reportType, report.breakdownItems),
    [report.breakdownItems, route.params.reportType]
  );
  const sortOptions = useMemo(
    () => getBreakdownSortOptions(route.params.reportType),
    [route.params.reportType]
  );
  const filteredBreakdownItems = useMemo(
    () =>
      filterAndSortBreakdownItems({
        filterIds: selectedFilterIds,
        items: report.breakdownItems,
        query: searchQuery,
        reportType: route.params.reportType,
        sortDirection,
        sortField,
      }),
    [
      report.breakdownItems,
      route.params.reportType,
      searchQuery,
      selectedFilterIds,
      sortDirection,
      sortField,
    ]
  );
  const normalizedPagination = normalizePagination(
    {
      page: currentPage,
      pageSize: DETAIL_PAGE_SIZE,
      totalCount: filteredBreakdownItems.length,
      totalPages: Math.max(1, Math.ceil(filteredBreakdownItems.length / DETAIL_PAGE_SIZE)),
    },
    currentPage
  );
  const pagedBreakdownItems = filteredBreakdownItems.slice(
    (normalizedPagination.page - 1) * normalizedPagination.pageSize,
    normalizedPagination.page * normalizedPagination.pageSize
  );
  const canGoPrevious = normalizedPagination.page > 1;
  const canGoNext = normalizedPagination.page < normalizedPagination.totalPages;

  useEffect(() => {
    const validFilterIds = new Set(filterOptions.map((option) => option.id));
    const validSelection = selectedFilterIds.filter((filterId) => validFilterIds.has(filterId));
    if (
      validSelection.length === 0 ||
      validSelection.includes(ALL_FILTER_ID) ||
      validSelection.length === getSelectableFilterIds(filterOptions).length
    ) {
      if (selectedFilterIds.length !== 1 || selectedFilterIds[0] !== ALL_FILTER_ID) {
        setSelectedFilterIds([ALL_FILTER_ID]);
      }
      return;
    }

    if (validSelection.length !== selectedFilterIds.length) {
      setSelectedFilterIds(validSelection);
    }
  }, [filterOptions, selectedFilterIds]);

  useEffect(() => {
    if (currentPage > normalizedPagination.totalPages) {
      setCurrentPage(normalizedPagination.totalPages);
    }
  }, [currentPage, normalizedPagination.totalPages]);

  const goToPreviousPage = () => {
    setCurrentPage((page) => Math.max(1, page - 1));
  };

  const goToNextPage = () => {
    setCurrentPage((page) => Math.min(normalizedPagination.totalPages, page + 1));
  };

  return (
    <SafeAreaView edges={['top']} className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <ScrollView
        className="flex-1"
        contentContainerStyle={{ paddingBottom: 28 }}
        showsVerticalScrollIndicator={false}
      >
        <View className="flex-row items-center justify-between px-6 py-4">
          <HeaderIconButton icon="arrow-back" isDark={isDark} onPress={() => navigation.goBack()} />
          <HeaderIconButton icon="share-outline" isDark={isDark} />
        </View>

        <View className="px-6 py-3">
          <Text className={cx(TYPE.screenTitle, 'text-[#18181b] dark:text-white')}>
            {report.title}
          </Text>
          <Text className={cx(TYPE.screenSubtitle, 'mt-2 text-[#71717a] dark:text-[#a1a1aa]')}>
            {report.subtitle}
          </Text>
        </View>

        <View className="px-6 py-3">
          <ReportTopControls
            isDark={isDark}
            searchPlaceholder={getSearchPlaceholder(route.params.reportType)}
            searchQuery={searchQuery}
            onOpenFilter={() => setFilterSheetVisible(true)}
            onOpenSort={() => setSortSheetVisible(true)}
            onSearchChange={setSearchQuery}
          />
        </View>

        <View className="gap-4 px-6 pt-2">
          {pagedBreakdownItems.length > 0 ? (
            pagedBreakdownItems.map((item) => (
              <BreakdownCard isDark={isDark} item={item} key={item.key} />
            ))
          ) : (
            <ReportCard className="px-5 py-8" isDark={isDark}>
              <Text
                className={cx(TYPE.rowMuted, 'text-center text-[#8d9098] dark:text-[#8f929b]')}
              >
                {report.breakdownItems.length > 0
                  ? 'No records match the active filters.'
                  : 'No detailed records available.'}
              </Text>
            </ReportCard>
          )}

          <PaginationRow
            canGoNext={canGoNext}
            canGoPrevious={canGoPrevious}
            pagination={normalizedPagination}
            onGoToPage={setCurrentPage}
            onNext={goToNextPage}
            onPrevious={goToPreviousPage}
          />
        </View>
      </ScrollView>
      <FilterBySheet
        filterLabel={filterLabel}
        isDark={isDark}
        options={filterOptions}
        selectedFilterIds={selectedFilterIds}
        visible={filterSheetVisible}
        onClose={() => setFilterSheetVisible(false)}
        onSelect={(filterId) => {
          setSelectedFilterIds((currentFilterIds) =>
            toggleSelectedFilterId(currentFilterIds, filterId, filterOptions)
          );
        }}
      />
      <SortBySheet
        isDark={isDark}
        selectedDirection={sortDirection}
        selectedField={sortField}
        visible={sortSheetVisible}
        options={sortOptions}
        onClose={() => setSortSheetVisible(false)}
        onDirectionChange={setSortDirection}
        onFieldChange={setSortField}
      />
    </SafeAreaView>
  );
}

function HeaderIconButton({
  icon,
  isDark,
  onPress,
}: {
  icon: keyof typeof Ionicons.glyphMap;
  isDark: boolean;
  onPress?: () => void;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      className="h-10 w-10 items-center justify-center rounded-full bg-white dark:bg-[#191a1f]"
      style={{
        shadowColor: isDark ? FIGMA_COLORS.shadowDark : FIGMA_COLORS.shadowLight,
        shadowOpacity: isDark ? 0.18 : 0.42,
        shadowRadius: 8,
        shadowOffset: { height: 3, width: 0 },
        elevation: 2,
      }}
      onPress={onPress}
    >
      <Ionicons color={isDark ? '#e7e7e9' : '#525762'} name={icon} size={18} />
    </Pressable>
  );
}

function ReportCard({
  children,
  className,
  isDark,
}: {
  children: ReactNode;
  className?: string;
  isDark: boolean;
}) {
  return (
    <View
      className={cx('items-center rounded-[20px] bg-white px-6 py-8 dark:bg-[#191a1f]', className)}
      style={{
        shadowColor: isDark ? FIGMA_COLORS.shadowDark : FIGMA_COLORS.shadowLight,
        shadowOpacity: isDark ? 0.18 : 0.44,
        shadowRadius: 10,
        shadowOffset: { height: 4, width: 0 },
        elevation: 2,
      }}
    >
      {children}
    </View>
  );
}

function ReportTopControls({
  isDark,
  searchPlaceholder,
  searchQuery,
  onOpenFilter,
  onOpenSort,
  onSearchChange,
}: {
  isDark: boolean;
  searchPlaceholder: string;
  searchQuery: string;
  onOpenFilter: () => void;
  onOpenSort: () => void;
  onSearchChange: (query: string) => void;
}) {
  return (
    <View className="flex-row items-center gap-3">
      <View className="flex-1">
        <SearchBar
          placeholder={searchPlaceholder}
          value={searchQuery}
          onChangeText={onSearchChange}
        />
      </View>
      <CircleIconButton
        accessibilityLabel="Filter"
        icon="options-outline"
        isDark={isDark}
        onPress={onOpenFilter}
      />
      <CircleIconButton
        accessibilityLabel="Sort"
        icon="swap-vertical-outline"
        isDark={isDark}
        onPress={onOpenSort}
      />
    </View>
  );
}

function CircleIconButton({
  accessibilityLabel,
  icon,
  isDark,
  onPress,
}: {
  accessibilityLabel: string;
  icon: keyof typeof Ionicons.glyphMap;
  isDark: boolean;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityLabel={accessibilityLabel}
      accessibilityRole="button"
      className="h-11 w-11 items-center justify-center rounded-full bg-white dark:bg-[#191a1f]"
      style={{
        shadowColor: isDark ? FIGMA_COLORS.shadowDark : FIGMA_COLORS.shadowLight,
        shadowOpacity: isDark ? 0.16 : 0.4,
        shadowRadius: 8,
        shadowOffset: { height: 3, width: 0 },
        elevation: 2,
      }}
      onPress={onPress}
    >
      <Ionicons color={isDark ? '#e7e7e9' : '#525762'} name={icon} size={20} />
    </Pressable>
  );
}

function FilterBySheet({
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

function SortBySheet({
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

function ReportOptionSheet({
  children,
  description,
  isDark,
  title,
  visible,
  onClose,
}: {
  children: ReactNode;
  description: string;
  isDark: boolean;
  title: string;
  visible: boolean;
  onClose: () => void;
}) {
  return (
    <Modal animationType="fade" transparent visible={visible} onRequestClose={onClose}>
      <View className="flex-1 justify-end bg-black/35 px-4 pb-6 dark:bg-black/70">
        <View
          className="max-h-[80%] rounded-[24px] bg-white p-4 dark:bg-[#191a1f]"
          style={{
            shadowColor: isDark ? FIGMA_COLORS.shadowDark : FIGMA_COLORS.shadowLight,
            shadowOpacity: isDark ? 0.28 : 0.45,
            shadowRadius: 12,
            shadowOffset: { height: 6, width: 0 },
            elevation: 4,
          }}
        >
          <View className="mx-auto mb-4 h-1 w-10 rounded-full bg-[#d7d9de] dark:bg-[#3a3b42]" />
          <View className="mb-4 flex-row items-start justify-between gap-4">
            <View className="flex-1">
              <Text className={cx(TYPE.cardTitle, 'text-[#18181b] dark:text-white')}>{title}</Text>
              <Text className={cx(TYPE.cardDescription, 'mt-1 text-[#71717a] dark:text-[#a1a1aa]')}>
                {description}
              </Text>
            </View>
            <Pressable accessibilityRole="button" hitSlop={12} onPress={onClose}>
              <Ionicons color={isDark ? '#a1a1aa' : '#71717a'} name="close-outline" size={22} />
            </Pressable>
          </View>
          <ScrollView showsVerticalScrollIndicator={false}>
            <View className="pb-2">{children}</View>
          </ScrollView>
        </View>
      </View>
    </Modal>
  );
}

function OptionRow({
  label,
  selected,
  onPress,
}: {
  label: string;
  selected: boolean;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      className={cx(
        'mb-2 min-h-[48px] flex-row items-center justify-between rounded-[14px] border px-4',
        selected
          ? 'border-[#f97332] bg-[#fff1e9] dark:bg-[#3b2418]'
          : 'border-[#eeeeef] bg-white dark:border-[#33343a] dark:bg-[#202126]'
      )}
      onPress={onPress}
    >
      <Text
        className={cx(
          TYPE.rowValue,
          selected ? 'text-[#18181b] dark:text-white' : 'text-[#525762] dark:text-[#e7e7e9]'
        )}
      >
        {label}
      </Text>
      {selected ? <Ionicons color="#f97332" name="checkmark-circle" size={20} /> : null}
    </Pressable>
  );
}

function BreakdownCard({ isDark, item }: { isDark: boolean; item: BreakdownItem }) {
  const statusTone: StatusTone =
    item.statusTone ??
    (item.status === 'Open' ? 'warning' : item.status === 'Active' ? 'info' : 'success');

  return (
    <View
      className="w-full rounded-[20px] bg-white p-6 dark:bg-[#191a1f]"
      style={{
        shadowColor: isDark ? FIGMA_COLORS.shadowDark : FIGMA_COLORS.shadowLight,
        shadowOpacity: isDark ? 0.18 : 0.4,
        shadowRadius: 8,
        shadowOffset: { height: 3, width: 0 },
        elevation: 2,
      }}
    >
      <View className="flex-row items-start justify-between gap-3">
        <Text className={cx(TYPE.cardTitle, 'flex-1 text-[#18181b] dark:text-white')}>
          {item.id}
        </Text>
        {item.showStatus === false ? null : (
          <StatusChip color={item.statusColor} status={item.status} tone={statusTone} />
        )}
      </View>
      <View className="mt-3 gap-3">
        {item.fields.map((field, fieldIndex) => (
          <View
            className="flex-row items-center justify-between gap-3"
            key={`${field.label}-${fieldIndex}`}
          >
            <View className="flex-row items-center gap-2">
              <Ionicons color="#8f929b" name={field.icon} size={14} />
              <Text className={cx(TYPE.rowMuted, 'text-[#71717a] dark:text-[#a1a1aa]')}>
                {field.label}
              </Text>
            </View>
            <Text
              className={cx(TYPE.rowValue, 'max-w-[55%] text-right text-[#18181b] dark:text-white')}
            >
              {field.value}
            </Text>
          </View>
        ))}
      </View>
    </View>
  );
}

function StatusChip({
  color,
  status,
  tone,
}: {
  color?: string;
  status: BreakdownItem['status'];
  tone: StatusTone;
}) {
  if (color) {
    return (
      <View
        style={{
          backgroundColor: `${color}22`,
          borderRadius: 999,
          paddingHorizontal: 12,
          paddingVertical: 4,
        }}
      >
        <Text className={TYPE.microStrong} style={{ color }}>
          {status}
        </Text>
      </View>
    );
  }

  const classes = {
    info: {
      container: 'bg-[#dbeafe] dark:bg-[#172554]',
      text: 'text-[#1d4ed8] dark:text-[#93c5fd]',
    },
    success: {
      container: 'bg-[#dcfce7] dark:bg-[#133225]',
      text: 'text-[#2b7744] dark:text-[#86efac]',
    },
    warning: {
      container: 'bg-[#fef3c7] dark:bg-[#3f2f14]',
      text: 'text-[#855f2c] dark:text-[#facc15]',
    },
    danger: {
      container: 'bg-[#fee2e2] dark:bg-[#3f1d1d]',
      text: 'text-[#a43532] dark:text-[#fca5a5]',
    },
  }[tone];

  return (
    <View className={cx('rounded-full px-3 py-1', classes.container)}>
      <Text className={cx(TYPE.microStrong, classes.text)}>{status}</Text>
    </View>
  );
}

function PaginationRow({
  canGoNext,
  canGoPrevious,
  pagination,
  onGoToPage,
  onNext,
  onPrevious,
}: {
  canGoNext: boolean;
  canGoPrevious: boolean;
  pagination: ReportPaginationMeta;
  onGoToPage: (page: number) => void;
  onNext: () => void;
  onPrevious: () => void;
}) {
  const pageList = buildPaginationPageList(pagination.page, pagination.totalPages);

  return (
    <View className="flex-row items-center gap-2">
      <View className="flex-1 flex-row items-center gap-2">
        {pageList.map((entry, index) =>
          entry === 'ellipsis' ? (
            <Text
              className={cx(TYPE.rowMuted, 'text-[#71717a] dark:text-[#a1a1aa]')}
              key={`ellipsis-${index}`}
            >
              ...
            </Text>
          ) : (
            <PagePill
              isCurrent={entry === pagination.page}
              key={entry}
              page={entry}
              onPress={() => onGoToPage(entry)}
            />
          )
        )}
      </View>
      <PaginationButton
        disabled={!canGoPrevious}
        icon="chevron-back-outline"
        label="Previous"
        onPress={onPrevious}
      />
      <PaginationButton
        disabled={!canGoNext}
        icon="chevron-forward-outline"
        label="Next"
        onPress={onNext}
      />
    </View>
  );
}

function PagePill({
  isCurrent,
  page,
  onPress,
}: {
  isCurrent: boolean;
  page: number;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      className={cx(
        'h-8 min-w-[32px] items-center justify-center rounded-2xl px-3',
        isCurrent && 'bg-[#ebebec] dark:bg-[#2a2b30]'
      )}
      disabled={isCurrent}
      onPress={onPress}
    >
      <Text className={cx(TYPE.rowValue, 'text-[#18181b] dark:text-white')}>{page}</Text>
    </Pressable>
  );
}

function PaginationButton({
  disabled,
  icon,
  label,
  onPress,
}: {
  disabled?: boolean;
  icon: keyof typeof Ionicons.glyphMap;
  label: string;
  onPress: () => void;
}) {
  const iconColor = disabled ? '#a1a1aa' : label === 'Next' ? '#18181b' : '#71717a';

  return (
    <Pressable
      accessibilityRole="button"
      className={cx(
        'h-8 flex-row items-center gap-1 rounded-2xl border border-[#dedee0] px-3 dark:border-[#33343a]',
        disabled && 'opacity-50'
      )}
      disabled={disabled}
      onPress={onPress}
    >
      {label === 'Previous' ? <Ionicons color={iconColor} name={icon} size={14} /> : null}
      <Text className={cx(TYPE.rowValue, 'text-[#18181b] dark:text-white')}>{label}</Text>
      {label === 'Next' ? <Ionicons color={iconColor} name={icon} size={14} /> : null}
    </Pressable>
  );
}

type ReportTotals = {
  totalLiens: number;
  totalCases: number;
  totalLawFirmCases: number;
  totalMedicalFacilityCases: number;
};

function formatCount(count: number): string {
  return count.toLocaleString();
}

function buildReport(
  reportType: DashboardReportType,
  totalLienRows: DashboardTotalLienReportRow[],
  totalCaseRows: DashboardTotalCaseReportRow[],
  lawFirmRows: DashboardLawFirmCaseReportRow[],
  medicalProviderRows: DashboardMedicalProviderReportRow[],
  totals: ReportTotals,
  useDummyData: boolean
): ReportModel {
  if (reportType === 'total-cases') {
    return {
      title: 'Total Cases',
      subtitle: `You have a total of ${formatCount(totals.totalCases)} cases. Stay on top of your legal matters`,
      breakdownTitle: 'Detailed Breakdown',
      breakdownItems: useDummyData ? [] : totalCaseRowsToBreakdownItems(totalCaseRows),
    };
  }

  if (reportType === 'law-firm-allocation') {
    return {
      title: 'Law Firm Case Allocation',
      subtitle: `You have a total of ${formatCount(totals.totalLawFirmCases)} law firm cases. Stay organized and monitor your firm's legal matters.`,
      breakdownTitle: 'Detailed Breakdown',
      breakdownItems: useDummyData ? [] : lawFirmCaseRowsToBreakdownItems(lawFirmRows),
    };
  }

  if (reportType === 'medical-facility-allocation') {
    return {
      title: 'Medical Facility Case Allocation',
      subtitle: `You have a total of ${formatCount(totals.totalMedicalFacilityCases)} medical facility cases. Review and manage them from one place.`,
      breakdownTitle: 'Detailed Breakdown',
      breakdownItems: useDummyData
        ? []
        : medicalFacilityCaseRowsToBreakdownItems(medicalProviderRows),
    };
  }

  return {
    title: 'Total Lien',
    subtitle: `You have a total of ${formatCount(totals.totalLiens)} liens. Review and manage them with ease.`,
    breakdownTitle: 'Detailed Breakdown',
    breakdownItems: useDummyData ? LIEN_BREAKDOWN : lienRowsToBreakdownItems(totalLienRows),
  };
}

function readReportText(row: Record<string, unknown>, keys: string[]): string | undefined {
  for (const key of keys) {
    const value = row[key];
    if (typeof value === 'string' && value.trim().length > 0) {
      return value;
    }

    if (typeof value === 'number' && Number.isFinite(value)) {
      return String(value);
    }
  }

  return undefined;
}

function normalizeLienStatusLabel(label?: string): 'Open' | 'Close' {
  const normalized = label?.trim().toLowerCase() ?? '';
  return normalized.includes('close') ||
    normalized.includes('settled') ||
    normalized.includes('paid')
    ? 'Close'
    : 'Open';
}

function getCaseStatusTone(status: string): StatusTone {
  const normalized = status.trim().toLowerCase();
  if (normalized.includes('closed')) return 'danger';
  if (normalized.includes('negotiat') || normalized.includes('pending') || normalized.includes('open')) {
    return 'warning';
  }
  if (normalized.includes('active')) return 'info';
  return 'success';
}

function totalCaseRowsToBreakdownItems(rows: DashboardTotalCaseReportRow[]): BreakdownItem[] {
  return rows.map((row, index) => {
    const r = row as Record<string, unknown>;

    const name =
      row.clientDisplayName ??
      (row.clientFirstName && row.clientLastName
        ? `${row.clientFirstName} ${row.clientLastName}`
        : undefined) ??
      row.patientName ??
      row.name ??
      readReportText(r, ['fullName', 'clientName', 'plaintiff', 'plaintiffName']) ??
      'N/A';

    const caseId =
      row.caseNumber ??
      row.caseReference ??
      row.externalReference ??
      row.caseId ??
      readReportText(r, ['caseNo', 'caseCode', 'referenceNumber']) ??
      (typeof r.id === 'string' ? r.id : undefined) ??
      'N/A';

    const rawStatus = row.status ?? row.caseStatus ?? row.currentStatus ?? row.statusName ?? 'N/A';

    const dateOfLoss =
      row.dateOfIncident ??
      row.dateOfLoss ??
      row.incidentDate ??
      row.lossDate ??
      readReportText(r, ['dateOfLoss', 'lossDate', 'incidentDate', 'dateOfIncident']) ??
      'N/A';

    return {
      id: name,
      key: buildBreakdownKey('total-case', index, caseId, name, rawStatus),
      status: rawStatus,
      statusTone: getCaseStatusTone(rawStatus),
      fields: [
        { icon: 'briefcase-outline', label: 'Case ID', value: caseId },
        { icon: 'calendar-outline', label: 'Date of Loss', value: dateOfLoss },
      ],
    };
  });
}

function lawFirmCaseRowsToBreakdownItems(rows: DashboardLawFirmCaseReportRow[]): BreakdownItem[] {
  return rows.map((row, index) => {
    const r = row as Record<string, unknown>;

    const name =
      row.clientDisplayName ??
      (row.clientFirstName && row.clientLastName
        ? `${row.clientFirstName} ${row.clientLastName}`
        : undefined) ??
      row.patientName ??
      readReportText(r, ['fullName', 'clientName', 'plaintiff', 'plaintiffName']) ??
      'N/A';

    const caseId =
      row.caseNumber ??
      row.caseReference ??
      row.caseId ??
      readReportText(r, ['caseNo', 'caseCode', 'referenceNumber']) ??
      (typeof r.id === 'string' ? r.id : undefined) ??
      'N/A';

    const dateOfLoss =
      row.dateOfIncident ??
      row.dateOfLoss ??
      row.incidentDate ??
      row.lossDate ??
      readReportText(r, ['dateOfLoss', 'lossDate', 'incidentDate', 'dateOfIncident']) ??
      'N/A';

    const lawFirm = readLawFirmName(row);

    return {
      id: name,
      key: buildBreakdownKey('law-firm-case', index, caseId, name, lawFirm),
      status: readReportText(r, ['status']) ?? 'Active',
      showStatus: false,
      fields: [
        { icon: 'briefcase-outline', label: 'Case ID', value: caseId },
        { icon: 'calendar-outline', label: 'Date of Loss', value: dateOfLoss },
        { icon: 'business-outline', label: 'Law Firm', value: lawFirm },
      ],
    };
  });
}

function medicalFacilityCaseRowsToBreakdownItems(
  rows: DashboardMedicalProviderReportRow[]
): BreakdownItem[] {
  return rows.map((row, index) => {
    const r = row as Record<string, unknown>;

    const name =
      row.clientDisplayName ??
      (row.clientFirstName && row.clientLastName
        ? `${row.clientFirstName} ${row.clientLastName}`
        : undefined) ??
      row.patientName ??
      readReportText(r, ['fullName', 'clientName', 'plaintiff', 'plaintiffName']) ??
      'N/A';

    const caseId =
      row.caseNumber ??
      row.caseReference ??
      row.caseId ??
      readReportText(r, ['caseNo', 'caseCode', 'referenceNumber']) ??
      (typeof r.id === 'string' ? r.id : undefined) ??
      'N/A';

    const dateOfLoss =
      row.dateOfIncident ??
      row.dateOfLoss ??
      row.incidentDate ??
      row.lossDate ??
      readReportText(r, ['dateOfLoss', 'lossDate', 'incidentDate', 'dateOfIncident']) ??
      'N/A';

    const facilityName = readFacilityName(row);

    return {
      id: name,
      key: buildBreakdownKey('medical-case', index, caseId, name, facilityName),
      status: 'Active',
      showStatus: false,
      fields: [
        { icon: 'briefcase-outline', label: 'Case ID', value: caseId },
        { icon: 'calendar-outline', label: 'Date of Loss', value: dateOfLoss },
        { icon: 'medical-outline', label: 'Medical Facility', value: facilityName },
      ],
    };
  });
}

function createLienBreakdownItem(
  lienId: string,
  status: 'Open' | 'Close',
  caseId: string,
  plaintiffName: string
): BreakdownItem {
  return {
    id: `Lien ID: ${lienId}`,
    key: buildBreakdownKey('lien-fallback', 0, lienId, caseId, plaintiffName),
    status,
    fields: [
      { icon: 'briefcase-outline', label: 'Case ID', value: caseId },
      { icon: 'person-outline', label: 'Plaintiff Name', value: plaintiffName },
    ],
  };
}

function lienRowsToBreakdownItems(rows: DashboardTotalLienReportRow[]): BreakdownItem[] {
  return rows.map((row, index) => {
    const record = row as Record<string, unknown>;
    const status = normalizeLienStatusLabel(
      readReportText(record, [
        'status',
        'lienStatus',
        'lienStatusName',
        'statusName',
        'label',
        'name',
      ])
    );
    const lienId =
      readReportText(record, ['lienId', 'liensId', 'lienCode', 'liensCode', 'lienNumber', 'id']) ??
      String(index + 1);
    const fields: BreakdownItem['fields'] = [
      {
        icon: 'briefcase-outline',
        label: 'Case ID',
        value: readReportText(record, ['caseId']) ?? 'N/A',
      },
      {
        icon: 'person-outline',
        label: 'Plaintiff Name',
        value: readReportText(record, ['clientName']) ?? 'N/A',
      },
    ];

    return {
      id: `Lien ID: ${lienId}`,
      key: buildBreakdownKey('total-lien', index, lienId, status),
      status,
      fields,
    };
  });
}

function readLawFirmName(row: DashboardLawFirmCaseReportRow): string {
  const r = row as Record<string, unknown>;
  const candidates = [row.lawFirm, row.lawfirm, row.lawFirmName, row.firmName, row.name];
  for (const val of candidates) {
    if (typeof val === 'string' && val.trim().length > 2) return val;
  }
  for (const key of [
    'organization',
    'organizationName',
    'orgName',
    'contactName',
    'firm',
    'title',
    'lawFirmTitle',
  ]) {
    const val = r[key];
    if (typeof val === 'string' && val.trim().length > 2) return val;
  }
  const skipStringKeys = new Set([
    'label',
    'status',
    'type',
    'id',
    'tenantId',
    'createdAt',
    'updatedAt',
  ]);
  for (const [key, val] of Object.entries(r)) {
    if (skipStringKeys.has(key)) continue;
    if (typeof val === 'string' && val.trim().length > 2) return val;
  }
  return row.label ?? 'Unknown Law Firm';
}

function readFacilityName(row: DashboardMedicalProviderReportRow): string {
  const r = row as Record<string, unknown>;
  const candidates = [
    row.facilityName,
    row.medicalProvider,
    row.medicalprovider,
    row.medicalProviderName,
    row.providerName,
    row.name,
  ];
  for (const val of candidates) {
    if (typeof val === 'string' && val.trim().length > 2) return val;
  }
  for (const key of [
    'organization',
    'organizationName',
    'orgName',
    'facility',
    'medicalFacility',
    'provider',
    'title',
  ]) {
    const val = r[key];
    if (typeof val === 'string' && val.trim().length > 2) return val;
  }
  const skipStringKeys = new Set([
    'label',
    'status',
    'type',
    'id',
    'tenantId',
    'createdAt',
    'updatedAt',
  ]);
  for (const [key, val] of Object.entries(r)) {
    if (skipStringKeys.has(key)) continue;
    if (typeof val === 'string' && val.trim().length > 2) return val;
  }
  return row.label ?? 'Unknown Facility';
}
