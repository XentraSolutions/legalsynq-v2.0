import { useMemo, type ReactNode } from 'react';
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
} from '@/features/dashboard/hooks';
import type { DashboardReportType } from '@/features/dashboard/types/types';
import type { MainStackParamList } from '@/navigation/types/navigation';
import type {
  DashboardLawFirmCaseReportRow,
  DashboardMedicalProviderReportRow,
  DashboardPiechart,
} from '@/shared/api/endpoints/Cases';
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
  status: 'Open' | 'Close' | 'Settled' | 'Active';
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

const ORANGE = '#f97332';
const BLUE = '#3b82f6';
const GREEN = '#22c55e';
const YELLOW = '#f5b800';
const RED = '#ef4444';
const SLICE_COLORS = [BLUE, ORANGE, GREEN, YELLOW, RED];

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

export function DashboardReportDetailScreen() {
  const navigation = useNavigation();
  const route = useRoute<DetailRoute>();
  const { colorScheme } = useNativeWindColorScheme();
  const isDark = colorScheme === 'dark';
  const { data: piechartData } = useDashboardPiechart();
  const { data: lawFirmReport = [] } = useDashboardLawFirmCaseReport();
  const { data: medicalProviderReport = [] } = useDashboardMedicalProviderReport();
  const report = useMemo(
    () => buildReport(route.params.reportType, piechartData, lawFirmReport, medicalProviderReport),
    [lawFirmReport, medicalProviderReport, piechartData, route.params.reportType]
  );

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
              {report.slices.map((slice, index) => (
                <DetailLegendRow
                  key={slice.label}
                  isLast={index === report.slices.length - 1}
                  slice={slice}
                />
              ))}
            </View>
          </ReportCard>
        </View>

        <View className="px-6 pt-2">
          <ReportCard className="px-5 py-5" isDark={isDark}>
            <View className="mb-2 flex-row items-center justify-between">
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

            {report.breakdownItems.map((item, index) => (
              <BreakdownCard
                isLast={index === report.breakdownItems.length - 1}
                item={item}
                key={item.id}
              />
            ))}

            <View className="mt-4 flex-row items-center justify-between">
              <View className="flex-row items-center gap-3">
                <View className="h-8 w-8 items-center justify-center rounded-full bg-[#ebebec] dark:bg-[#2a2b30]">
                  <Text className={cx(TYPE.rowValue, 'text-[#18181b] dark:text-white')}>1</Text>
                </View>
                <Text className={cx(TYPE.rowMuted, 'text-[#71717a] dark:text-[#a1a1aa]')}>...</Text>
                <Text className={cx(TYPE.rowValue, 'text-[#18181b] dark:text-white')}>48</Text>
              </View>
              <View className="flex-row gap-2">
                <PaginationButton disabled icon="chevron-back-outline" label="Previous" />
                <PaginationButton icon="chevron-forward-outline" label="Next" />
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
        <StatusChip status={item.status} tone={statusTone} />
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
  status,
  tone,
}: {
  status: BreakdownItem['status'];
  tone: 'success' | 'warning' | 'info';
}) {
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
}: {
  disabled?: boolean;
  icon: keyof typeof Ionicons.glyphMap;
  label: string;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      className={cx(
        'h-8 flex-row items-center gap-1 rounded-full border border-[#dedee0] px-3 dark:border-[#33343a]',
        disabled && 'opacity-50'
      )}
      disabled={disabled}
    >
      {label === 'Previous' ? <Ionicons color="#71717a" name={icon} size={14} /> : null}
      <Text className={cx(TYPE.rowValue, 'text-[#18181b] dark:text-white')}>{label}</Text>
      {label === 'Next' ? <Ionicons color="#18181b" name={icon} size={14} /> : null}
    </Pressable>
  );
}

function buildReport(
  reportType: DashboardReportType,
  piechartData: DashboardPiechart | undefined,
  lawFirmReport: DashboardLawFirmCaseReportRow[],
  medicalProviderReport: DashboardMedicalProviderReportRow[]
): ReportModel {
  if (reportType === 'total-cases') {
    const slices = piechartData?.caseStatus.length
      ? mapStatusesToSlices(piechartData.caseStatus)
      : TOTAL_CASES_FALLBACK;
    return {
      title: 'Total Cases',
      subtitle:
        'Track the overall number of cases and view their current status distribution at a glance.',
      centerValue: piechartData?.totalCases.toLocaleString() ?? '4,773',
      centerCaption: 'Total Cases',
      slices,
      breakdownTitle: 'Detailed Breakdown',
      breakdownItems: slicesToBreakdownItems(slices, 'case'),
    };
  }

  if (reportType === 'law-firm-allocation') {
    const reportSlices = mapAllocationReportToSlices(
      lawFirmReport,
      getLawFirmLabel,
      getLawFirmCaseCount
    );
    const hasLiveData = reportSlices.length > 0;
    const slices = hasLiveData ? reportSlices : LAW_FIRM_FALLBACK;
    return {
      title: 'Law Firm Case Allocation',
      subtitle: 'Distribution of total case volume across assigned legal firms.',
      centerValue: hasLiveData
        ? reportSlices.reduce((sum, slice) => sum + slice.value, 0).toLocaleString()
        : '175',
      centerCaption: 'Total Cases',
      slices,
      breakdownTitle: 'Detailed Breakdown',
      breakdownItems: slicesToBreakdownItems(slices, 'lawFirm'),
    };
  }

  if (reportType === 'medical-facility-allocation') {
    const reportSlices = mapAllocationReportToSlices(
      medicalProviderReport,
      getMedicalProviderLabel,
      getMedicalProviderCaseCount
    );
    const hasLiveData = reportSlices.length > 0;
    const slices = hasLiveData ? reportSlices : FACILITY_FALLBACK;
    return {
      title: 'Medical Facility Case Allocation',
      subtitle: 'Distribution of total case volume across assigned healthcare facilities.',
      centerValue: hasLiveData
        ? reportSlices.reduce((sum, slice) => sum + slice.value, 0).toLocaleString()
        : '239',
      centerCaption: 'Total MD Cases',
      slices,
      breakdownTitle: 'Detailed Breakdown',
      breakdownItems: slicesToBreakdownItems(slices, 'facility'),
    };
  }

  const slices = piechartData ? mapPiechartToLienSlices(piechartData) : TOTAL_LIEN_FALLBACK;
  return {
    title: 'Total Lien',
    subtitle: 'Breakdown of open and closed claims with total purchase and billing values.',
    centerValue: piechartData?.totalLiens.toLocaleString() ?? '239',
    centerCaption: 'Total Liens',
    slices,
    breakdownTitle: 'Detailed Breakdown',
    breakdownItems: LIEN_BREAKDOWN,
  };
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

function slicesToBreakdownItems(
  slices: DetailSlice[],
  kind: 'case' | 'lawFirm' | 'facility'
): BreakdownItem[] {
  return slices
    .map((slice) => {
      const label = slice.label;
      const status: BreakdownItem['status'] = kind === 'case' ? 'Active' : 'Settled';
      const fields: BreakdownItem['fields'] = [
        {
          icon: 'folder-outline',
          label: kind === 'case' ? 'Status Count' : 'Total Cases',
          value: slice.amount ?? String(slice.value),
        },
        {
          icon: 'pie-chart-outline',
          label: 'Share',
          value: slice.percent?.replace(/[()]/g, '') ?? `${slice.value}%`,
        },
        { icon: 'calendar-outline', label: 'Report Period', value: '10 / 27 / 2026' },
      ];

      return {
        id: label,
        status,
        fields,
      };
    })
    .slice(0, 5);
}

function mapPiechartToLienSlices(data: DashboardPiechart): DetailSlice[] {
  const total = data.totalLiens || 1;
  const closedCount = data.lienStatus
    .filter((status) => {
      const label = status.label.toLowerCase();
      return label === 'closed' || label === 'close';
    })
    .reduce((sum, status) => sum + status.value, 0);
  const openCount = total - closedCount;
  const openPct = (openCount / total) * 100;
  const closedPct = (closedCount / total) * 100;

  return [
    {
      label: 'Open',
      value: openPct,
      amount: String(openCount),
      percent: `(${openPct.toFixed(1)}%)`,
      color: BLUE,
      details: [
        { label: 'Purchase', value: formatCurrency(data.totalLienValue * 0.25) },
        { label: 'Billing', value: formatCurrency(data.totalLienValue) },
      ],
    },
    {
      label: 'Close',
      value: closedPct,
      amount: String(closedCount),
      percent: `(${closedPct.toFixed(1)}%)`,
      color: ORANGE,
      details: [
        { label: 'Purchase', value: formatCurrency(data.totalLienValue * 0.05) },
        { label: 'Billing', value: formatCurrency(data.totalLienValue * 0.08) },
      ],
    },
  ];
}

function mapStatusesToSlices(statuses: Array<{ label: string; value: number }>): DetailSlice[] {
  const total = statuses.reduce((sum, status) => sum + status.value, 0) || 1;

  return statuses.map((status, index) => {
    const pct = (status.value / total) * 100;
    return {
      label: status.label,
      value: status.value,
      amount: status.value.toLocaleString(),
      percent: `(${pct.toFixed(2)}%)`,
      color: SLICE_COLORS[index % SLICE_COLORS.length],
    };
  });
}

function mapAllocationReportToSlices<Row>(
  rows: Row[],
  getLabel: (row: Row) => string,
  getCount: (row: Row) => number
): DetailSlice[] {
  const rowsWithCounts = rows
    .map((row) => ({ label: getLabel(row), count: getCount(row) }))
    .filter((row) => row.count > 0);
  const total = rowsWithCounts.reduce((sum, row) => sum + row.count, 0) || 1;

  return rowsWithCounts.map((row, index) => {
    const pct = (row.count / total) * 100;
    return {
      label: row.label,
      value: row.count,
      amount: row.count.toLocaleString(),
      percent: `(${pct.toFixed(2)}%)`,
      color: SLICE_COLORS[index % SLICE_COLORS.length],
    };
  });
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

function getLawFirmCaseCount(row: DashboardLawFirmCaseReportRow): number {
  return (
    numericValue(row.totalCases) ??
    numericValue(row.totalCase) ??
    numericValue(row.caseCount) ??
    numericValue(row.cases) ??
    numericValue(row.count) ??
    numericValue(row.total) ??
    numericValue(row.value) ??
    0
  );
}

function getLawFirmLabel(row: DashboardLawFirmCaseReportRow): string {
  return (
    row.lawFirm ?? row.lawfirm ?? row.lawFirmName ?? row.name ?? row.label ?? 'Unknown Law Firm'
  );
}

function getMedicalProviderCaseCount(row: DashboardMedicalProviderReportRow): number {
  return (
    numericValue(row.totalCases) ??
    numericValue(row.totalCase) ??
    numericValue(row.caseCount) ??
    numericValue(row.cases) ??
    numericValue(row.count) ??
    numericValue(row.total) ??
    numericValue(row.value) ??
    0
  );
}

function getMedicalProviderLabel(row: DashboardMedicalProviderReportRow): string {
  return (
    row.facilityName ??
    row.medicalProvider ??
    row.medicalprovider ??
    row.medicalProviderName ??
    row.providerName ??
    row.name ??
    row.label ??
    'Unknown Facility'
  );
}
