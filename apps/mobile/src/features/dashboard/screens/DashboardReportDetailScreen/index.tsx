import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, useRoute, type RouteProp } from '@react-navigation/native';
import { useColorScheme as useNativeWindColorScheme } from 'nativewind';
import Svg, { Circle } from 'react-native-svg';

import {
  useDashboardLawFirmCaseReport,
  useDashboardMedicalProviderReport,
  useDashboardPiechart,
  useDashboardTotalCaseReport,
  useDashboardTotalLienReport,
} from '@/features/dashboard/hooks';
import type { DashboardReportType } from '@/features/dashboard/types/types';
import type { MainStackParamList } from '@/navigation/types/navigation';
import type {
  DashboardLawFirmCaseReportRow,
  DashboardMedicalProviderReportRow,
  DashboardPiechart,
  DashboardTotalCaseReportRow,
  DashboardTotalLienReportRow,
  ReportFilterRequest,
} from '@/shared/api/endpoints/Cases';
import { useDashboardSettings } from '@/shared/hooks/useDashboardSettings';
import type { PagedResult } from '@/shared/types/api';
import { cx, FIGMA_COLORS, FIGMA_TEXT as TYPE } from '@/shared/styles';

type DetailRoute = RouteProp<MainStackParamList, 'DashboardReportDetail'>;

type DetailSlice = {
  label: string;
  value: number;
  color: string;
  amount?: string;
  percent?: string;
  details?: Array<{ label: string; value: string }>;
};

type BreakdownItem = {
  id: string;
  status: string;
  statusColor?: string;
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
  centerValue: string;
  centerCaption: string;
  slices: DetailSlice[];
  breakdownTitle: string;
  breakdownItems: BreakdownItem[];
};

type LienReportStatus = 'Open' | 'Close';

type LienReportStatusSummary = {
  billing: number;
  count: number;
  purchase: number;
};

type TotalLienReportSummary = {
  byStatus: Record<LienReportStatus, LienReportStatusSummary>;
  totalBilling: number;
  totalLiens: number;
  totalPurchase: number;
};

type ReportPaginationMeta = {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

const ORANGE = '#f97332';
const BLUE = '#3b82f6';
const GREEN = '#22c55e';
const YELLOW = '#f5b800';
const RED = '#ef4444';
const SLICE_COLORS = [BLUE, ORANGE, GREEN, YELLOW, RED];
const DETAIL_PAGE_SIZE = 5;

const TOTAL_LIEN_FALLBACK: DetailSlice[] = [
  {
    label: 'Open',
    value: 92.9,
    amount: '222',
    percent: '(92.9%)',
    color: BLUE,
    details: [
      { label: 'Purchase', value: '$563,238.44' },
      { label: 'Billing', value: '$2,232,740.12' },
    ],
  },
  {
    label: 'Close',
    value: 6.3,
    amount: '15',
    percent: '(6.3%)',
    color: ORANGE,
    details: [
      { label: 'Purchase', value: '$10,337.30' },
      { label: 'Billing', value: '$54,646.00' },
    ],
  },
];

const TOTAL_CASES_FALLBACK: DetailSlice[] = [
  { label: 'Case Settled', value: 93.83, amount: '4,479', percent: '(93.83%)', color: BLUE },
  { label: 'Closed', value: 2.51, amount: '120', percent: '(2.51%)', color: ORANGE },
  { label: 'Litigation (Open)', value: 2.39, amount: '114', percent: '(2.39%)', color: GREEN },
  { label: 'Demand Sent', value: 1.26, amount: '60', percent: '(1.26%)', color: YELLOW },
];

const LAW_FIRM_FALLBACK: DetailSlice[] = [
  { label: 'James Law Group', value: 42.86, amount: '75', percent: '(42.86%)', color: BLUE },
  { label: 'Adam Associates', value: 22.86, amount: '40', percent: '(22.86%)', color: ORANGE },
  { label: 'Anthony Injury Law', value: 17.14, amount: '30', percent: '(17.14%)', color: GREEN },
  { label: 'Benson & Bingham', value: 17.14, amount: '30', percent: '(17.14%)', color: YELLOW },
];

const FACILITY_FALLBACK: DetailSlice[] = [
  { label: 'Pueblo Medical', value: 41.84, amount: '100', percent: '(41.84%)', color: BLUE },
  { label: 'MUIR MD Associates', value: 26.78, amount: '64', percent: '(26.78%)', color: ORANGE },
  { label: 'Surgical Arts Center', value: 20.92, amount: '50', percent: '(20.92%)', color: GREEN },
  {
    label: 'Summit Surgical Center',
    value: 10.46,
    amount: '25',
    percent: '(10.46%)',
    color: YELLOW,
  },
];

const LIEN_BREAKDOWN: BreakdownItem[] = [
  createLienBreakdownItem('84517', 'Close', '26-42803', 'Sarah Kimura'),
  createLienBreakdownItem('63290', 'Close', '26-58114', 'James Okonkwo'),
  createLienBreakdownItem('91638', 'Open', '26-31951', 'Marcus Delgado'),
  createLienBreakdownItem('47826', 'Close', '26-63927', 'Elena Vasquez'),
  createLienBreakdownItem('55093', 'Open', '26-49381', 'Thomas Brewer'),
];

function formatDateForDisplay(value: string): string {
  return value.split('/').join(' / ');
}

function formatDateRangeLabel(dateRange: { endDate: string; startDate: string }): string {
  if (dateRange.startDate === dateRange.endDate) {
    return formatDateForDisplay(dateRange.startDate);
  }

  return `${formatDateForDisplay(dateRange.startDate)} - ${formatDateForDisplay(dateRange.endDate)}`;
}

function buildDashboardReportFilter(
  dateRange: { endDate: string; startDate: string },
  page: number
): ReportFilterRequest {
  return {
    page,
    limit: DETAIL_PAGE_SIZE,
    startDate: dateRange.startDate,
    endDate: dateRange.endDate,
  };
}

function getReportPagination(
  reportType: DashboardReportType,
  totalLienReport: PagedResult<DashboardTotalLienReportRow> | undefined,
  totalCaseReport: PagedResult<DashboardTotalCaseReportRow> | undefined,
  lawFirmReport: PagedResult<DashboardLawFirmCaseReportRow> | undefined,
  medicalProviderReport: PagedResult<DashboardMedicalProviderReportRow> | undefined,
  currentPage: number
): ReportPaginationMeta | undefined {
  if (reportType === 'total-cases') {
    if (!totalCaseReport) return undefined;
    const totalCount = totalCaseReport.totalCount;
    if (!totalCount) return undefined;
    return {
      page: totalCaseReport.page,
      pageSize: DETAIL_PAGE_SIZE,
      totalCount,
      totalPages: Math.max(1, Math.ceil(totalCount / DETAIL_PAGE_SIZE)),
    };
  }

  if (reportType === 'law-firm-allocation') {
    if (!lawFirmReport) return undefined;
    const totalCount = lawFirmReport.totalCount;
    if (!totalCount) return undefined;
    return {
      page: currentPage,
      pageSize: DETAIL_PAGE_SIZE,
      totalCount,
      totalPages: Math.max(1, Math.ceil(totalCount / DETAIL_PAGE_SIZE)),
    };
  }

  if (reportType === 'medical-facility-allocation') {
    if (!medicalProviderReport) return undefined;
    const totalCount = medicalProviderReport.totalCount;
    if (!totalCount) return undefined;
    return {
      page: currentPage,
      pageSize: DETAIL_PAGE_SIZE,
      totalCount,
      totalPages: Math.max(1, Math.ceil(totalCount / DETAIL_PAGE_SIZE)),
    };
  }

  return totalLienReport;
}

function getDummyReportPagination(reportType: DashboardReportType): ReportPaginationMeta {
  const totalCount = {
    'law-firm-allocation': LAW_FIRM_FALLBACK.length,
    'medical-facility-allocation': FACILITY_FALLBACK.length,
    'total-cases': TOTAL_CASES_FALLBACK.length,
    'total-liens': LIEN_BREAKDOWN.length,
  }[reportType];

  return {
    page: 1,
    pageSize: DETAIL_PAGE_SIZE,
    totalCount,
    totalPages: Math.max(1, Math.ceil(totalCount / DETAIL_PAGE_SIZE)),
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

function formatPaginationRange(pagination: ReportPaginationMeta): string {
  if (pagination.totalCount === 0) {
    return 'No records';
  }

  const start = (pagination.page - 1) * pagination.pageSize + 1;
  const end = Math.min(pagination.page * pagination.pageSize, pagination.totalCount);
  return `Showing ${start}-${end} of ${pagination.totalCount}`;
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

  useEffect(() => {
    setCurrentPage(1);
  }, [route.params.dateRange.endDate, route.params.dateRange.startDate, route.params.reportType]);

  const reportFilter = useMemo(
    () => buildDashboardReportFilter(route.params.dateRange, currentPage),
    [currentPage, route.params.dateRange]
  );
  const reportPeriodLabel = useMemo(
    () => formatDateRangeLabel(route.params.dateRange),
    [route.params.dateRange]
  );
  const { data: totalLienReport } = useDashboardTotalLienReport(reportFilter, reportsEnabled);
  const { data: totalCaseReport } = useDashboardTotalCaseReport(reportFilter, reportsEnabled);
  const lawFirmAllRowsFilter = useMemo(
    () => ({ ...buildDashboardReportFilter(route.params.dateRange, 1), limit: 10000 }),
    [route.params.dateRange]
  );
  const { data: lawFirmReport } = useDashboardLawFirmCaseReport(
    lawFirmAllRowsFilter,
    reportsEnabled
  );
  const medicalProviderAllRowsFilter = useMemo(
    () => ({ ...buildDashboardReportFilter(route.params.dateRange, 1), limit: 100 }),
    [route.params.dateRange]
  );
  const { data: medicalProviderReport } = useDashboardMedicalProviderReport(
    medicalProviderAllRowsFilter,
    reportsEnabled
  );
  const { data: piechartData } = useDashboardPiechart();
  const pagination = useMemo(
    () =>
      useDashboardDummyData
        ? getDummyReportPagination(route.params.reportType)
        : getReportPagination(
            route.params.reportType,
            totalLienReport,
            totalCaseReport,
            lawFirmReport,
            medicalProviderReport,
            currentPage
          ),
    [
      currentPage,
      lawFirmReport,
      medicalProviderReport,
      route.params.reportType,
      totalCaseReport,
      totalLienReport,
      useDashboardDummyData,
    ]
  );
  const normalizedPagination = normalizePagination(pagination, currentPage);
  const canGoPrevious = normalizedPagination.page > 1;
  const canGoNext = normalizedPagination.page < normalizedPagination.totalPages;
  const report = useMemo(
    () =>
      buildReport(
        route.params.reportType,
        totalLienReport?.items ?? [],
        totalCaseReport?.items ?? [],
        lawFirmReport?.items ?? [],
        medicalProviderReport?.items ?? [],
        reportPeriodLabel,
        useDashboardDummyData,
        piechartData,
        currentPage
      ),
    [
      currentPage,
      lawFirmReport?.items,
      medicalProviderReport?.items,
      piechartData,
      reportPeriodLabel,
      route.params.reportType,
      totalCaseReport?.items,
      totalLienReport?.items,
      useDashboardDummyData,
    ]
  );

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
          <ReportCard isDark={isDark}>
            <LargeDonutChart
              centerCaption={report.centerCaption}
              centerValue={report.centerValue}
              slices={report.slices}
            />
            <View className="mt-5 w-full">
              {report.slices.length > 0 ? (
                report.slices.map((slice, index) => (
                  <DetailLegendRow
                    key={slice.label}
                    isLast={index === report.slices.length - 1}
                    slice={slice}
                  />
                ))
              ) : (
                <Text
                  className={cx(TYPE.rowMuted, 'text-center text-[#8d9098] dark:text-[#8f929b]')}
                >
                  No report data available for the selected date range.
                </Text>
              )}
            </View>
          </ReportCard>
        </View>

        <View className="px-6 pt-2">
          <ReportCard className="px-5 py-5" isDark={isDark}>
            <View className="mb-2 w-full flex-row items-center justify-between">
              <View className="flex-row items-center gap-2">
                <Ionicons color={isDark ? '#a1a1aa' : '#525762'} name="list-outline" size={18} />
                <Text className={cx(TYPE.cardTitle, 'text-[#18181b] dark:text-white')}>
                  {report.breakdownTitle}
                </Text>
              </View>
              <Ionicons
                color={isDark ? '#a1a1aa' : '#71717a'}
                name="chevron-down-outline"
                size={18}
              />
            </View>

            {report.breakdownItems.length > 0 ? (
              report.breakdownItems.map((item, index) => (
                <BreakdownCard
                  isLast={index === report.breakdownItems.length - 1}
                  item={item}
                  key={item.id}
                />
              ))
            ) : (
              <Text
                className={cx(TYPE.rowMuted, 'py-6 text-center text-[#8d9098] dark:text-[#8f929b]')}
              >
                No detailed records available.
              </Text>
            )}

            <View className="mt-4 flex-row items-center justify-between gap-3">
              <View className="flex-1">
                <View className="flex-row items-center gap-3">
                  <View className="h-8 min-w-[32px] items-center justify-center rounded-full bg-[#ebebec] px-3 dark:bg-[#2a2b30]">
                    <Text className={cx(TYPE.rowValue, 'text-[#18181b] dark:text-white')}>
                      {normalizedPagination.page}
                    </Text>
                  </View>
                  <Text className={cx(TYPE.rowMuted, 'text-[#71717a] dark:text-[#a1a1aa]')}>
                    of {normalizedPagination.totalPages}
                  </Text>
                </View>
                <Text className={cx(TYPE.microMeta, 'mt-1 text-[#8b8f99] dark:text-[#8f929b]')}>
                  {formatPaginationRange(normalizedPagination)}
                </Text>
              </View>
              <View className="flex-row gap-2">
                <PaginationButton
                  disabled={!canGoPrevious}
                  icon="chevron-back-outline"
                  label="Previous"
                  onPress={goToPreviousPage}
                />
                <PaginationButton
                  disabled={!canGoNext}
                  icon="chevron-forward-outline"
                  label="Next"
                  onPress={goToNextPage}
                />
              </View>
            </View>
          </ReportCard>
        </View>
      </ScrollView>
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

function LargeDonutChart({
  centerCaption,
  centerValue,
  slices,
}: {
  centerCaption: string;
  centerValue: string;
  slices: DetailSlice[];
}) {
  const size = 192;
  const strokeWidth = 38;
  const radius = (size - strokeWidth) / 2;
  const circumference = 2 * Math.PI * radius;
  const total = slices.reduce((sum, slice) => sum + slice.value, 0) || 1;
  let accumulated = 0;

  return (
    <View className="items-center justify-center">
      <View className="h-[192px] w-[192px] items-center justify-center">
        <Svg height={size} width={size}>
          {slices.map((slice) => {
            const length = (slice.value / total) * circumference;
            const dashOffset = -accumulated;
            accumulated += length;
            return (
              <Circle
                cx={size / 2}
                cy={size / 2}
                fill="transparent"
                key={slice.label}
                r={radius}
                stroke={slice.color}
                strokeDasharray={`${length} ${circumference - length}`}
                strokeDashoffset={dashOffset}
                strokeLinecap="butt"
                strokeWidth={strokeWidth}
                transform={`rotate(-90 ${size / 2} ${size / 2})`}
              />
            );
          })}
        </Svg>
        <View className="absolute h-[96px] w-[96px] items-center justify-center rounded-full bg-white dark:bg-[#191a1f]">
          <Text className={cx(TYPE.donutValue, 'text-center text-[#18181b] dark:text-white')}>
            {centerValue}
          </Text>
          <Text
            className={cx(
              TYPE.donutCaption,
              'mt-0.5 text-center text-[#525762] dark:text-[#a1a1aa]'
            )}
          >
            {centerCaption}
          </Text>
        </View>
      </View>
    </View>
  );
}

function DetailLegendRow({ isLast, slice }: { isLast: boolean; slice: DetailSlice }) {
  return (
    <View
      className={`${isLast ? '' : 'border-b border-dashed border-[#e4e4e7] dark:border-[#2a2b30]'} py-3`}
    >
      <View className="flex-row items-center justify-between gap-3">
        <View className="flex-row items-center gap-3">
          <View className="h-4 w-1.5 rounded-full" style={{ backgroundColor: slice.color }} />
          <Text className={cx(TYPE.rowMuted, 'text-[#18181b] dark:text-[#f4f4f5]')}>
            {slice.label}
          </Text>
        </View>
        <Text className={cx(TYPE.rowValue, 'text-[#71717a] dark:text-[#a1a1aa]')}>
          {slice.amount} {slice.percent}
        </Text>
      </View>
      {slice.details?.map((detail) => (
        <View className="mt-3 flex-row items-center justify-between pl-8" key={detail.label}>
          <Text className={cx(TYPE.rowMuted, 'text-[#8b8f99] dark:text-[#8f929b]')}>
            {detail.label}
          </Text>
          <Text className={cx(TYPE.rowValue, 'text-[#8b8f99] dark:text-[#a3a4ab]')}>
            {detail.value}
          </Text>
        </View>
      ))}
    </View>
  );
}

function BreakdownCard({ isLast, item }: { isLast: boolean; item: BreakdownItem }) {
  const statusTone =
    item.status === 'Open' ? 'warning' : item.status === 'Active' ? 'info' : 'success';

  return (
    <View
      className={`${isLast ? '' : 'border-b border-[#e4e4e7] dark:border-[#2a2b30]'} w-full py-5`}
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
        {item.fields.map((field) => (
          <View className="flex-row items-center justify-between gap-3" key={field.label}>
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
  tone: 'success' | 'warning' | 'info';
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
  }[tone];

  return (
    <View className={cx('rounded-full px-3 py-1', classes.container)}>
      <Text className={cx(TYPE.microStrong, classes.text)}>{status}</Text>
    </View>
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
        'h-8 flex-row items-center gap-1 rounded-full border border-[#dedee0] px-3 dark:border-[#33343a]',
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

function mapPiechartCaseSlices(data: DashboardPiechart): DetailSlice[] {
  const total = data.totalCases || 1;
  return data.caseStatus.map((s, i) => {
    const pct = (s.value / total) * 100;
    return {
      label: s.label,
      value: s.value,
      amount: s.value.toLocaleString(),
      percent: `(${pct.toFixed(2)}%)`,
      color: SLICE_COLORS[i % SLICE_COLORS.length],
    };
  });
}

function mapPiechartLienSlices(data: DashboardPiechart): DetailSlice[] {
  const total = data.totalLiens || 1;
  const closedCount = data.lienStatus
    .filter((s) => s.label.toLowerCase() === 'closed')
    .reduce((sum, s) => sum + s.value, 0);
  const openCount = total - closedCount;
  const openPct = (openCount / total) * 100;
  const closedPct = (closedCount / total) * 100;

  return [
    {
      label: 'Open',
      value: openCount,
      amount: openCount.toLocaleString(),
      percent: `(${openPct.toFixed(1)}%)`,
      color: BLUE,
    },
    {
      label: 'Close',
      value: closedCount,
      amount: closedCount.toLocaleString(),
      percent: `(${closedPct.toFixed(1)}%)`,
      color: ORANGE,
    },
  ];
}

function buildReport(
  reportType: DashboardReportType,
  totalLienRows: DashboardTotalLienReportRow[],
  totalCaseRows: DashboardTotalCaseReportRow[],
  lawFirmReport: DashboardLawFirmCaseReportRow[],
  medicalProviderReport: DashboardMedicalProviderReportRow[],
  reportPeriodLabel: string,
  useDummyData: boolean,
  piechartData: DashboardPiechart | undefined,
  currentPage: number
): ReportModel {
  if (reportType === 'total-cases') {
    const reportData = useDummyData ? undefined : mapTotalCaseReportToDetail(totalCaseRows);
    const piechartSlices =
      !useDummyData && piechartData ? mapPiechartCaseSlices(piechartData) : undefined;
    const slices = useDummyData
      ? TOTAL_CASES_FALLBACK
      : (reportData?.slices ?? piechartSlices ?? []);
    const centerValue = useDummyData
      ? '4,773'
      : (reportData?.totalCases.toLocaleString() ??
        piechartData?.totalCases.toLocaleString() ??
        '0');
    return {
      title: 'Total Cases',
      subtitle:
        'Track the overall number of cases and view their current status distribution at a glance.',
      centerValue,
      centerCaption: 'Total Cases',
      slices,
      breakdownTitle: 'Detailed Breakdown',
      breakdownItems: useDummyData
        ? []
        : totalCaseRowsToBreakdownItems(
            totalCaseRows,
            new Map(slices.map((s) => [s.label.toLowerCase(), s.color]))
          ),
    };
  }

  if (reportType === 'law-firm-allocation') {
    const reportSlices = useDummyData ? [] : mapLawFirmReportGrouped(lawFirmReport);
    const slices = useDummyData ? LAW_FIRM_FALLBACK : reportSlices;
    return {
      title: 'Law Firm Case Allocation',
      subtitle: 'Distribution of total case volume across assigned legal firms.',
      centerValue: useDummyData
        ? '175'
        : reportSlices.reduce((sum, slice) => sum + slice.value, 0).toLocaleString(),
      centerCaption: 'Total Cases',
      slices,
      breakdownTitle: 'Detailed Breakdown',
      breakdownItems: useDummyData
        ? []
        : lawFirmCaseRowsToBreakdownItems(
            lawFirmReport.slice(
              (currentPage - 1) * DETAIL_PAGE_SIZE,
              currentPage * DETAIL_PAGE_SIZE
            )
          ),
    };
  }

  if (reportType === 'medical-facility-allocation') {
    const reportSlices = useDummyData ? [] : mapMedicalFacilityReportGrouped(medicalProviderReport);
    const slices = useDummyData ? FACILITY_FALLBACK : reportSlices;
    return {
      title: 'Medical Facility Case Allocation',
      subtitle: 'Distribution of total case volume across assigned healthcare facilities.',
      centerValue: useDummyData
        ? '239'
        : reportSlices.reduce((sum, slice) => sum + slice.value, 0).toLocaleString(),
      centerCaption: 'Total MD Cases',
      slices,
      breakdownTitle: 'Detailed Breakdown',
      breakdownItems: useDummyData
        ? []
        : medicalFacilityCaseRowsToBreakdownItems(
            medicalProviderReport.slice(
              (currentPage - 1) * DETAIL_PAGE_SIZE,
              currentPage * DETAIL_PAGE_SIZE
            )
          ),
    };
  }

  const reportData = useDummyData ? undefined : mapTotalLienReportToDetail(totalLienRows);
  const piechartLienSlices =
    !useDummyData && piechartData ? mapPiechartLienSlices(piechartData) : undefined;
  const slices = useDummyData
    ? TOTAL_LIEN_FALLBACK
    : (reportData?.slices ?? piechartLienSlices ?? []);
  const centerValue = useDummyData
    ? '239'
    : (reportData?.totalLiens.toLocaleString() ?? piechartData?.totalLiens.toLocaleString() ?? '0');
  return {
    title: 'Total Lien',
    subtitle: 'Breakdown of open and closed claims with total purchase and billing values.',
    centerValue,
    centerCaption: 'Total Liens',
    slices,
    breakdownTitle: 'Detailed Breakdown',
    breakdownItems: useDummyData ? LIEN_BREAKDOWN : lienRowsToBreakdownItems(totalLienRows),
  };
}

function totalCaseRowsToBreakdownItems(
  rows: DashboardTotalCaseReportRow[],
  statusColorMap: Map<string, string>
): BreakdownItem[] {
  return rows.map((row) => {
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

    const statusColor = statusColorMap.get(rawStatus.toLowerCase());

    return {
      id: name,
      status: rawStatus,
      statusColor,
      fields: [
        { icon: 'briefcase-outline', label: 'Case ID', value: caseId },
        { icon: 'calendar-outline', label: 'Date of Loss', value: dateOfLoss },
      ],
    };
  });
}

function lawFirmCaseRowsToBreakdownItems(rows: DashboardLawFirmCaseReportRow[]): BreakdownItem[] {
  return rows.map((row) => {
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
      status: readReportText(r, ['status']) ?? 'Active',
      showStatus: false,
      fields: [
        { icon: 'briefcase-outline', label: 'Case ID', value: caseId },
        { icon: 'business-outline', label: 'Law Firm', value: lawFirm },
        { icon: 'calendar-outline', label: 'Date of Loss', value: dateOfLoss },
      ],
    };
  });
}

function medicalFacilityCaseRowsToBreakdownItems(
  rows: DashboardMedicalProviderReportRow[]
): BreakdownItem[] {
  return rows.map((row) => {
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
      status: 'Active',
      showStatus: false,
      fields: [
        { icon: 'briefcase-outline', label: 'Case ID', value: caseId },
        { icon: 'calendar-outline', label: 'Date of Loss', value: dateOfLoss },
        { icon: 'medical-outline', label: 'MedicalFacility', value: facilityName },
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
    status,
    fields: [
      { icon: 'briefcase-outline', label: 'Case ID', value: caseId },
      { icon: 'person-outline', label: 'Plaintiff Name', value: plaintiffName },
    ],
  };
}

function formatCurrency(value: number): string {
  return value.toLocaleString('en-US', { style: 'currency', currency: 'USD' });
}

function numericValue(value: unknown): number | undefined {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value;
  }

  if (typeof value === 'string') {
    const parsed = Number(value.replace(/[^0-9.-]/g, ''));
    return Number.isFinite(parsed) ? parsed : undefined;
  }

  return undefined;
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

function readReportNumber(row: Record<string, unknown>, keys: string[]): number | undefined {
  for (const key of keys) {
    const value = numericValue(row[key]);
    if (value !== undefined) {
      return value;
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

function mapTotalLienReportToDetail(
  rows: DashboardTotalLienReportRow[]
):
  | { slices: DetailSlice[]; totalBilling: number; totalLiens: number; totalPurchase: number }
  | undefined {
  if (!rows.length) {
    return undefined;
  }

  const totals = rows.reduce<TotalLienReportSummary>(
    (summary, row) => {
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
      const count =
        readReportNumber(record, [
          'count',
          'total',
          'value',
          'lienCount',
          'liensCount',
          'totalLiens',
        ]) ?? 0;
      const purchase =
        readReportNumber(record, [
          'purchase',
          'purchaseAmount',
          'totalPurchase',
          'totalPurchaseAmount',
        ]) ?? 0;
      const billing =
        readReportNumber(record, [
          'billing',
          'billingAmount',
          'totalBilling',
          'totalBillingAmount',
        ]) ?? 0;

      summary.byStatus[status].count += count;
      summary.byStatus[status].purchase += purchase;
      summary.byStatus[status].billing += billing;
      summary.totalLiens += count;
      summary.totalPurchase += purchase;
      summary.totalBilling += billing;
      return summary;
    },
    {
      byStatus: {
        Close: { billing: 0, count: 0, purchase: 0 },
        Open: { billing: 0, count: 0, purchase: 0 },
      },
      totalBilling: 0,
      totalLiens: 0,
      totalPurchase: 0,
    }
  );

  if (totals.totalLiens === 0) {
    return undefined;
  }

  const totalLiens = totals.totalLiens || 1;
  const slices = (['Open', 'Close'] as const)
    .map((status) => {
      const statusTotal = totals.byStatus[status];
      const pct = (statusTotal.count / totalLiens) * 100;
      return {
        label: status,
        value: statusTotal.count,
        amount: statusTotal.count.toLocaleString(),
        percent: `(${pct.toFixed(1)}%)`,
        color: status === 'Open' ? BLUE : ORANGE,
        details: [
          { label: 'Purchase', value: formatCurrency(statusTotal.purchase) },
          { label: 'Billing', value: formatCurrency(statusTotal.billing) },
        ],
      } satisfies DetailSlice;
    })
    .filter((slice) => slice.value > 0);

  return {
    slices,
    totalBilling: totals.totalBilling,
    totalLiens: totals.totalLiens,
    totalPurchase: totals.totalPurchase,
  };
}

function mapTotalCaseReportToDetail(
  rows: DashboardTotalCaseReportRow[]
): { slices: DetailSlice[]; totalCases: number } | undefined {
  if (!rows.length) {
    return undefined;
  }

  const rowsWithCounts = rows
    .map((row) => {
      const record = row as Record<string, unknown>;
      return {
        count:
          readReportNumber(record, [
            'count',
            'total',
            'value',
            'caseCount',
            'cases',
            'totalCases',
          ]) ?? 0,
        label:
          readReportText(record, [
            'status',
            'caseStatus',
            'caseStatusName',
            'statusName',
            'label',
            'name',
          ]) ?? 'Unknown Status',
      };
    })
    .filter((row) => row.count > 0);

  if (!rowsWithCounts.length) {
    return undefined;
  }

  const totalCases = rowsWithCounts.reduce((sum, row) => sum + row.count, 0);
  const slices = rowsWithCounts.map((row, index) => {
    const pct = (row.count / totalCases) * 100;
    return {
      label: row.label,
      value: row.count,
      amount: row.count.toLocaleString(),
      percent: `(${pct.toFixed(2)}%)`,
      color: SLICE_COLORS[index % SLICE_COLORS.length],
    };
  });

  return { slices, totalCases };
}

function lienRowsToBreakdownItems(rows: DashboardTotalLienReportRow[]): BreakdownItem[] {
  return rows
    .map((row, index) => {
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
        readReportText(record, [
          'lienId',
          'liensId',
          'lienCode',
          'liensCode',
          'lienNumber',
          'id',
        ]) ?? String(index + 1);
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
        status,
        fields,
      };
    })
    .slice(0, 5);
}

function readLawFirmId(row: DashboardLawFirmCaseReportRow): string {
  const r = row as Record<string, unknown>;
  for (const key of [
    'lawFirmId',
    'lawfirmId',
    'lawFirmOrgId',
    'organizationId',
    'orgId',
    'firmId',
  ]) {
    const val = r[key];
    if (typeof val === 'string' && val.trim()) return val;
    if (typeof val === 'number') return String(val);
  }
  return readLawFirmName(row);
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

function mapLawFirmReportGrouped(rows: DashboardLawFirmCaseReportRow[]): DetailSlice[] {
  const groups = new Map<string, { label: string; count: number }>();
  for (const row of rows) {
    const id = readLawFirmId(row);
    const name = readLawFirmName(row);
    const existing = groups.get(id);
    if (existing) {
      existing.count += 1;
    } else {
      groups.set(id, { label: name, count: 1 });
    }
  }
  const entries = Array.from(groups.values()).filter((g) => g.count > 0);
  const total = entries.reduce((sum, g) => sum + g.count, 0) || 1;
  return entries.map((g, i) => {
    const pct = (g.count / total) * 100;
    return {
      label: g.label,
      value: g.count,
      amount: g.count.toLocaleString(),
      percent: `(${pct.toFixed(2)}%)`,
      color: SLICE_COLORS[i % SLICE_COLORS.length],
    };
  });
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

function mapMedicalFacilityReportGrouped(rows: DashboardMedicalProviderReportRow[]): DetailSlice[] {
  const groups = new Map<string, { label: string; count: number }>();
  for (const row of rows) {
    const name = readFacilityName(row);
    const existing = groups.get(name);
    if (existing) {
      existing.count += 1;
    } else {
      groups.set(name, { label: name, count: 1 });
    }
  }
  const entries = Array.from(groups.values()).filter((g) => g.count > 0);
  const total = entries.reduce((sum, g) => sum + g.count, 0) || 1;
  return entries.map((g, i) => {
    const pct = (g.count / total) * 100;
    return {
      label: g.label,
      value: g.count,
      amount: g.count.toLocaleString(),
      percent: `(${pct.toFixed(2)}%)`,
      color: SLICE_COLORS[i % SLICE_COLORS.length],
    };
  });
}
