import { useMemo, useState, type ReactNode } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, type NavigationProp } from '@react-navigation/native';
import Svg, { Circle, Defs, LinearGradient, Path, Polyline, Stop } from 'react-native-svg';
import { useAtom } from 'jotai';
import { useColorScheme as useNativeWindColorScheme } from 'nativewind';

import {
  useDashboardLawFirmCaseReport,
  useDashboardMedicalProviderReport,
  useDashboardPiechart,
} from '@/features/dashboard/hooks';
import type { DashboardReportType } from '@/features/dashboard/types/types';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { AppMenu } from '@/shared/components/AppMenu';
import { accountModeAtom, type AccountMode } from '@/shared/state/atoms';
import { cx, FIGMA_COLORS, FIGMA_TEXT as TYPE } from '@/shared/styles';
import type {
  DashboardPiechart,
  DashboardLawFirmCaseReportRow,
  DashboardMedicalProviderReportRow,
} from '@/shared/api/endpoints/Cases';

interface StatCardData {
  label: string;
  value: string;
  trend: string;
  trendTone: 'positive' | 'negative';
}

interface DonutSlice {
  label: string;
  value: number;
  color: string;
  amount?: string;
  percent?: string;
  details?: Array<{ label: string; value: string }>;
}

interface SellerRisk {
  name: string;
  balance: string;
  share: string;
  risk: 'High' | 'Medium';
  rows?: Array<{ label: string; value: string }>;
}

const ORANGE = '#f97332';
const BLUE = '#3b82f6';
const GREEN = '#22c55e';
const YELLOW = '#f5b800';
const RED = '#ef4444';
const MUTED = '#8f929b';

const SELLING_STATS: StatCardData[] = [
  { label: 'Total Lien Revenue', value: '$4,782,350.72', trend: '8.9%', trendTone: 'positive' },
  { label: 'Total Outstanding', value: '$3,842,196.18', trend: '6.4%', trendTone: 'positive' },
  { label: 'Past Amount Due', value: '$1,287,542.63', trend: '8.9%', trendTone: 'positive' },
  { label: 'Payments', value: '$635,251.44', trend: '5.0%', trendTone: 'negative' },
];

const BUYING_STATS: StatCardData[] = [
  { label: 'Cash Deployed', value: '$573,775.74', trend: '8.9%', trendTone: 'positive' },
  { label: 'Cash Received', value: '$3,842,196.18', trend: '6.4%', trendTone: 'positive' },
];

const SELLING_AGING: DonutSlice[] = [
  { label: '0-30 Days', value: 32.7, amount: '$1,125,842.50', percent: '(32.7%)', color: BLUE },
  { label: '31-60 Days', value: 21.2, amount: '$987,651.22', percent: '(21.2%)', color: ORANGE },
  { label: '61-90 Days', value: 19.2, amount: '$987,651.22', percent: '(19.2%)', color: GREEN },
  { label: '91-120 Days', value: 11.2, amount: '$754,221.17', percent: '(11.2%)', color: YELLOW },
  { label: '120+ Days', value: 10.8, amount: '$411,601.15', percent: '(10.8%)', color: RED },
];

const SELLING_STATUS: DonutSlice[] = [
  { label: 'Active', value: 67.5, amount: '842', percent: '(67.5%)', color: BLUE },
  { label: 'Settled', value: 17.1, amount: '214', percent: '(17.1%)', color: ORANGE },
  { label: 'In Reduction', value: 9, amount: '112', percent: '(9.0%)', color: GREEN },
  { label: 'Paid', value: 4.5, amount: '56', percent: '(4.5%)', color: YELLOW },
  { label: 'Other / Closed', value: 1.9, amount: '24', percent: '(1.9%)', color: RED },
];

const SELLING_TOP_BALANCES = [
  {
    name: 'Apex Mutual',
    subtitle: 'Active Accounts: 182',
    balance: '$1,125,842.50',
    share: '23.5%',
    mark: 'pie',
  },
  {
    name: 'Nova Care',
    subtitle: 'Active Accounts: 132',
    balance: '$687,421.88',
    share: '14.4%',
    mark: 'cube',
  },
  {
    name: 'Summit Ins.',
    subtitle: 'Active Accounts: 98',
    balance: '$456,218.33',
    share: '9.5%',
    mark: 'wave',
  },
  {
    name: 'Beacon Life',
    subtitle: 'Active Accounts: 76',
    balance: '$321,775.19',
    share: '6.7%',
    mark: 'bars',
  },
  {
    name: 'Vanguard',
    subtitle: 'Active Accounts: 64',
    balance: '$289,114.22',
    share: '6.0%',
    mark: 'v',
  },
];

const SELLING_SELLERS: SellerRisk[] = [
  {
    name: 'Apex Mutual',
    balance: '$1,125,842.50',
    share: '17.1%',
    risk: 'High',
    rows: [
      { label: '0 - 30 Days:', value: '$412,512.00' },
      { label: '31 - 60 Days:', value: '$298,451.23' },
      { label: '61 - 90 Days:', value: '$221,114.55' },
      { label: '91 - 120 Days:', value: '$112,662.11' },
      { label: '120+ Days:', value: '$81,102.30' },
    ],
  },
  { name: 'Nova Care', balance: '$687,421.88', share: '29.1%', risk: 'High' },
  { name: 'Summit Ins.', balance: '$456,218.33', share: '22.8%', risk: 'Medium' },
  { name: 'Beacon Life', balance: '$321,775.19', share: '29.7%', risk: 'High' },
  { name: 'Vanguard', balance: '$289,114.22', share: '40.3%', risk: 'High' },
];

const BUYING_TOTAL_LIENS: DonutSlice[] = [
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

const BUYING_TOTAL_CASES: DonutSlice[] = [
  { label: 'Case Settled', value: 93.83, amount: '4,479', percent: '(93.83%)', color: BLUE },
  { label: 'Closed', value: 2.51, amount: '120', percent: '(2.51%)', color: ORANGE },
  { label: 'Litigation (Open)', value: 2.39, amount: '114', percent: '(2.39%)', color: GREEN },
  { label: 'Demand Sent', value: 1.26, amount: '60', percent: '(1.26%)', color: YELLOW },
];

const LAW_FIRM_ALLOCATION: DonutSlice[] = [
  { label: 'James Law Group', value: 42.86, amount: '75', percent: '(42.86%)', color: BLUE },
  { label: 'Adam Associates', value: 22.86, amount: '40', percent: '(22.86%)', color: ORANGE },
  { label: 'Anthony Injury Law', value: 17.14, amount: '30', percent: '(17.14%)', color: GREEN },
  { label: 'Benson & Bingham', value: 17.14, amount: '30', percent: '(17.14%)', color: YELLOW },
];

const FACILITY_ALLOCATION: DonutSlice[] = [
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

const LINE_POINTS = [2.4, 3.7, 2.6, 1.0, 2.5, 2.6];

export function DashboardScreen() {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const { colorScheme } = useNativeWindColorScheme();
  const [accountMode] = useAtom(accountModeAtom);
  const [drawerVisible, setDrawerVisible] = useState(false);
  const isDark = colorScheme === 'dark';
  const handleViewReport = (reportType: DashboardReportType) => {
    navigation.navigate('DashboardReportDetail', { reportType });
  };

  return (
    <SafeAreaView edges={['top']} className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <ScrollView
        className="flex-1 px-4"
        contentContainerStyle={{ paddingBottom: 26 }}
        showsVerticalScrollIndicator={false}
      >
        <DashboardHeader
          accountMode={accountMode}
          isDark={isDark}
          onOpenMenu={() => setDrawerVisible(true)}
        />
        <DatePill isDark={isDark} />
        {accountMode === 'selling' ? (
          <SellingDashboard isDark={isDark} />
        ) : (
          <BuyingDashboard isDark={isDark} onViewReport={handleViewReport} />
        )}
      </ScrollView>
      <AppMenu visible={drawerVisible} onClose={() => setDrawerVisible(false)} />
    </SafeAreaView>
  );
}

function DashboardHeader({
  accountMode,
  isDark,
  onOpenMenu,
}: {
  accountMode: AccountMode;
  isDark: boolean;
  onOpenMenu: () => void;
}) {
  const subtitle = accountMode === 'selling' ? 'Lien selling dashboard' : 'Lien buying dashboard';
  const iconColor = isDark ? '#a1a1aa' : '#6f737d';

  return (
    <View className="mt-2 flex-row items-center">
      <CircleButton
        icon="menu-outline"
        iconColor={iconColor}
        isDark={isDark}
        onPress={onOpenMenu}
      />
      <View className="ml-3 flex-1">
        <Text className={cx(TYPE.dashboardGreeting, 'text-[#1f2329] dark:text-white')}>
          Welcome, John
        </Text>
        <Text className={cx(TYPE.dashboardSubtitle, 'mt-0.5 text-[#8a8d96] dark:text-[#8d9099]')}>
          {subtitle}
        </Text>
      </View>
      <View className="flex-row gap-2">
        <CircleButton icon="search-outline" iconColor={iconColor} isDark={isDark} />
        <CircleButton dot icon="notifications-outline" iconColor={iconColor} isDark={isDark} />
      </View>
    </View>
  );
}

function CircleButton({
  dot,
  icon,
  iconColor,
  isDark,
  onPress,
}: {
  dot?: boolean;
  icon: keyof typeof Ionicons.glyphMap;
  iconColor: string;
  isDark: boolean;
  onPress?: () => void;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      className="h-9 w-9 items-center justify-center rounded-full bg-white dark:bg-[#191a1f]"
      style={{
        shadowColor: isDark ? FIGMA_COLORS.shadowDark : FIGMA_COLORS.shadowLight,
        shadowOpacity: isDark ? 0.18 : 0.5,
        shadowRadius: 8,
        shadowOffset: { height: 3, width: 0 },
        elevation: 2,
      }}
      onPress={onPress}
    >
      <Ionicons color={iconColor} name={icon} size={19} />
      {dot ? <View className="absolute right-2 top-2 h-2 w-2 rounded-full bg-[#ef4444]" /> : null}
    </Pressable>
  );
}

function DatePill({ isDark }: { isDark: boolean }) {
  return (
    <View
      className="mt-4 h-9 flex-row items-center justify-between rounded-xl bg-white px-4 dark:bg-[#191a1f]"
      style={{
        shadowColor: isDark ? FIGMA_COLORS.shadowDark : FIGMA_COLORS.shadowLight,
        shadowOpacity: isDark ? 0.15 : 0.35,
        shadowRadius: 8,
        shadowOffset: { height: 3, width: 0 },
        elevation: 1,
      }}
    >
      <Text className={cx(TYPE.dateLabel, 'text-[#6f737d] dark:text-[#a1a1aa]')}>
        10 / 27 / 2026
      </Text>
      <Ionicons color={isDark ? '#a1a1aa' : '#6f737d'} name="calendar-clear-outline" size={16} />
    </View>
  );
}

function SellingDashboard({ isDark }: { isDark: boolean }) {
  return (
    <>
      <StatGrid isDark={isDark} stats={SELLING_STATS} />
      <DonutCard
        centerCaption="Total A/R"
        centerValue="$3.8M"
        icon="pie-chart-outline"
        isDark={isDark}
        slices={SELLING_AGING}
        subtitle="Breakdown of outstanding accounts receivable by age and duration."
        title="A/R Aging Summary"
      />
      <DonutCard
        centerCaption="Total Liens"
        centerValue="1,248"
        icon="pie-chart-outline"
        isDark={isDark}
        slices={SELLING_STATUS}
        subtitle="Breakdown of total case liens by their current operational status."
        title="Liens by Status"
      />
      <LineChartCard isDark={isDark} />
      <TopBalanceCard isDark={isDark} />
      <AgingSellerCard isDark={isDark} />
    </>
  );
}

const SLICE_COLORS = [BLUE, ORANGE, GREEN, YELLOW, RED];

function mapPiechartToLienSlices(data: DashboardPiechart): DonutSlice[] {
  const total = data.totalLiens || 1;
  const closedCount = data.lienStatus
    .filter((s) => {
      const label = s.label.toLowerCase();
      return label === 'closed' || label === 'close';
    })
    .reduce((sum, s) => sum + s.value, 0);
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
    },
    {
      label: 'Close',
      value: closedPct,
      amount: String(closedCount),
      percent: `(${closedPct.toFixed(1)}%)`,
      color: ORANGE,
    },
  ];
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

function mapAllocationReportToSlices<Row>(
  rows: Row[],
  getLabel: (row: Row) => string,
  getCount: (row: Row) => number
): DonutSlice[] {
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

function BuyingDashboard({
  isDark,
  onViewReport,
}: {
  isDark: boolean;
  onViewReport: (reportType: DashboardReportType) => void;
}) {
  const { data: piechartData } = useDashboardPiechart();
  const { data: lawFirmReport = [] } = useDashboardLawFirmCaseReport();
  const { data: medicalProviderReport = [] } = useDashboardMedicalProviderReport();
  const lienSlices = piechartData ? mapPiechartToLienSlices(piechartData) : BUYING_TOTAL_LIENS;
  const totalLiens = piechartData ? String(piechartData.totalLiens) : '239';
  const totalLienValue = piechartData
    ? formatCurrency(piechartData.totalLienValue)
    : '$2,287,386.12';
  const lawFirmReportSlices = mapAllocationReportToSlices(
    lawFirmReport,
    getLawFirmLabel,
    getLawFirmCaseCount
  );
  const lawFirmAllocationSlices =
    lawFirmReportSlices.length > 0 ? lawFirmReportSlices : LAW_FIRM_ALLOCATION;
  const lawFirmTotalCases =
    lawFirmReportSlices.length > 0
      ? lawFirmReportSlices.reduce((sum, slice) => sum + slice.value, 0).toLocaleString()
      : '175';
  const facilityReportSlices = mapAllocationReportToSlices(
    medicalProviderReport,
    getMedicalProviderLabel,
    getMedicalProviderCaseCount
  );
  const facilityAllocationSlices =
    facilityReportSlices.length > 0 ? facilityReportSlices : FACILITY_ALLOCATION;
  const facilityTotalCases =
    facilityReportSlices.length > 0
      ? facilityReportSlices.reduce((sum, slice) => sum + slice.value, 0).toLocaleString()
      : '239';

  return (
    <>
      <StatGrid isDark={isDark} stats={BUYING_STATS} />
      <DonutCard
        centerCaption="Total Liens"
        centerValue={totalLiens}
        icon="time-outline"
        isDark={isDark}
        slices={lienSlices}
        subtitle="Breakdown of open and closed claims with total purchase and billing values."
        summaryRows={[{ label: 'Total Billing Amount', value: totalLienValue }]}
        title="Total Liens"
        onViewDetails={() => onViewReport('total-liens')}
      />
      <DonutCard
        centerCaption="Total Cases"
        centerValue="4,773"
        icon="time-outline"
        isDark={isDark}
        slices={BUYING_TOTAL_CASES}
        subtitle="Track the overall number of cases and view their current status distribution at a glance."
        title="Total Cases"
        onViewDetails={() => onViewReport('total-cases')}
      />
      <DonutCard
        centerCaption="Total Cases"
        centerValue={lawFirmTotalCases}
        icon="time-outline"
        isDark={isDark}
        slices={lawFirmAllocationSlices}
        subtitle="Distribution of total case volume across assigned legal firms."
        title="Law Firm Case Allocation"
        onViewDetails={() => onViewReport('law-firm-allocation')}
      />
      <DonutCard
        centerCaption="Total Cases"
        centerValue={facilityTotalCases}
        icon="time-outline"
        isDark={isDark}
        slices={facilityAllocationSlices}
        subtitle="Distribution of total case volume across assigned healthcare facilities."
        title="Medical Facility Case Allocation"
        onViewDetails={() => onViewReport('medical-facility-allocation')}
      />
    </>
  );
}

function StatGrid({ isDark, stats }: { isDark: boolean; stats: StatCardData[] }) {
  return (
    <View className="mt-4 flex-row flex-wrap justify-between gap-y-3">
      {stats.map((stat) => (
        <View
          key={stat.label}
          className="w-[48%] rounded-[14px] bg-white p-4 dark:bg-[#191a1f]"
          style={{
            shadowColor: isDark ? FIGMA_COLORS.shadowDark : FIGMA_COLORS.shadowLight,
            shadowOpacity: isDark ? 0.16 : 0.45,
            shadowRadius: 9,
            shadowOffset: { height: 4, width: 0 },
            elevation: 2,
          }}
        >
          <Text className={cx(TYPE.statLabel, 'text-[#8d9098] dark:text-[#8f929b]')}>
            {stat.label}
          </Text>
          <Text className={cx(TYPE.statValue, 'mt-4 text-[#22252b] dark:text-[#f4f4f5]')}>
            {stat.value}
          </Text>
          <View
            className={`mt-2 self-start rounded-full px-2 py-1 ${
              stat.trendTone === 'positive'
                ? 'bg-[#e8f8ef] dark:bg-[#133225]'
                : 'bg-[#fde9ea] dark:bg-[#3a1f24]'
            }`}
          >
            <Text
              className={`${TYPE.microStrong} ${
                stat.trendTone === 'positive' ? 'text-[#19a45b]' : 'text-[#ef5d62]'
              }`}
            >
              {stat.trendTone === 'positive' ? '↑' : '↓'} {stat.trend}
            </Text>
          </View>
        </View>
      ))}
    </View>
  );
}

function CardShell({
  children,
  isDark,
  className,
}: {
  children: ReactNode;
  isDark: boolean;
  className?: string;
}) {
  return (
    <View
      className={['mt-5 rounded-[16px] bg-white p-5 dark:bg-[#191a1f]', className]
        .filter(Boolean)
        .join(' ')}
      style={{
        shadowColor: isDark ? FIGMA_COLORS.shadowDark : FIGMA_COLORS.shadowLight,
        shadowOpacity: isDark ? 0.18 : 0.45,
        shadowRadius: 10,
        shadowOffset: { height: 4, width: 0 },
        elevation: 2,
      }}
    >
      {children}
    </View>
  );
}

function SectionTitle({
  icon,
  subtitle,
  title,
}: {
  icon: keyof typeof Ionicons.glyphMap;
  subtitle: string;
  title: string;
}) {
  return (
    <View>
      <View className="flex-row items-center gap-2">
        <Ionicons color={MUTED} name={icon} size={17} />
        <Text className={cx(TYPE.cardTitle, 'text-[#24272d] dark:text-[#f5f5f5]')}>{title}</Text>
      </View>
      <Text className={cx(TYPE.cardDescription, 'mt-2 text-[#8d9098] dark:text-[#8f929b]')}>
        {subtitle}
      </Text>
    </View>
  );
}

function DonutCard({
  centerCaption,
  centerValue,
  icon,
  isDark,
  slices,
  subtitle,
  summaryRows,
  onViewDetails,
  title,
}: {
  centerCaption: string;
  centerValue: string;
  icon: keyof typeof Ionicons.glyphMap;
  isDark: boolean;
  slices: DonutSlice[];
  subtitle: string;
  summaryRows?: Array<{ label: string; value: string }>;
  onViewDetails?: () => void;
  title: string;
}) {
  return (
    <CardShell isDark={isDark}>
      <SectionTitle icon={icon} subtitle={subtitle} title={title} />
      <DonutChart centerCaption={centerCaption} centerValue={centerValue} slices={slices} />
      <View className="mt-4">
        {slices.map((slice, index) => (
          <LegendRow key={slice.label} isLast={index === slices.length - 1} slice={slice} />
        ))}
      </View>
      {summaryRows ? (
        <View className="mt-3 gap-4 border-t border-[#ececf0] pt-4 dark:border-[#292a2f]">
          {summaryRows.map((row) => (
            <View className="flex-row items-center justify-between" key={row.label}>
              <Text className={cx(TYPE.rowLabel, 'text-[#535762] dark:text-[#c7c8cc]')}>
                {row.label}
              </Text>
              <Text className={cx(TYPE.rowLabel, 'text-[#22252b] dark:text-[#f4f4f5]')}>
                {row.value}
              </Text>
            </View>
          ))}
        </View>
      ) : null}
      {onViewDetails ? (
        <Pressable
          accessibilityRole="button"
          className="mt-5 h-9 items-center justify-center rounded-full bg-[#ececee] dark:bg-[#2a2b30]"
          onPress={onViewDetails}
        >
          <Text className={cx(TYPE.cta, 'text-[#555964] dark:text-[#e7e7e9]')}>View Details</Text>
        </Pressable>
      ) : (
        <View className="mt-5 h-9 items-center justify-center rounded-full bg-[#ececee] dark:bg-[#2a2b30]">
          <Text className={cx(TYPE.cta, 'text-[#555964] dark:text-[#e7e7e9]')}>View Details</Text>
        </View>
      )}
    </CardShell>
  );
}

function DonutChart({
  centerCaption,
  centerValue,
  slices,
}: {
  centerCaption: string;
  centerValue: string;
  slices: DonutSlice[];
}) {
  const size = 156;
  const strokeWidth = 28;
  const radius = (size - strokeWidth) / 2;
  const circumference = 2 * Math.PI * radius;
  const total = slices.reduce((sum, slice) => sum + slice.value, 0);
  let accumulated = 0;

  return (
    <View className="mt-7 items-center justify-center">
      <View className="h-[156px] w-[156px] items-center justify-center">
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
        <View className="absolute h-[86px] w-[86px] items-center justify-center rounded-full bg-white dark:bg-[#191a1f]">
          <Text className={cx(TYPE.donutValue, 'text-center text-[#25282e] dark:text-white')}>
            {centerValue}
          </Text>
          <Text
            className={cx(
              TYPE.donutCaption,
              'mt-0.5 text-center text-[#767a84] dark:text-[#a1a1aa]'
            )}
          >
            {centerCaption}
          </Text>
        </View>
      </View>
    </View>
  );
}

function LegendRow({ isLast, slice }: { isLast: boolean; slice: DonutSlice }) {
  return (
    <View
      className={`${isLast ? '' : 'border-b border-dashed border-[#e8e8ec] dark:border-[#292a2f]'} py-3`}
    >
      <View className="flex-row items-center justify-between gap-3">
        <View className="flex-row items-center gap-3">
          <View className="h-4 w-1.5 rounded-full" style={{ backgroundColor: slice.color }} />
          <Text className={cx(TYPE.rowLabel, 'text-[#4d515c] dark:text-[#e1e1e4]')}>
            {slice.label}
          </Text>
        </View>
        <Text className={cx(TYPE.rowValue, 'text-[#6e727c] dark:text-[#a3a4ab]')}>
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

function LineChartCard({ isDark }: { isDark: boolean }) {
  const chart = useMemo(() => buildLineChart(220, 132, LINE_POINTS), []);
  const gridColor = isDark ? '#2a2b30' : '#e7e8ec';
  const labelColor = isDark ? '#8f929b' : '#8a8e98';

  return (
    <CardShell isDark={isDark}>
      <SectionTitle
        icon="analytics-outline"
        subtitle="Track fluctuations and growth in lien totals over time."
        title="Liens Over Time"
      />
      <View className="mt-7 flex-row">
        <View className="w-9 justify-between pb-6">
          {['$4M', '$3M', '$2M', '$1M', '$0'].map((label) => (
            <Text className={TYPE.microMeta} key={label} style={{ color: labelColor }}>
              {label}
            </Text>
          ))}
        </View>
        <View className="flex-1">
          <Svg height={150} width="100%" viewBox="0 0 220 150">
            <Defs>
              <LinearGradient id="lineFill" x1="0" x2="0" y1="0" y2="1">
                <Stop offset="0" stopColor={BLUE} stopOpacity={isDark ? 0.45 : 0.28} />
                <Stop offset="1" stopColor={BLUE} stopOpacity="0.03" />
              </LinearGradient>
            </Defs>
            {[0, 1, 2, 3, 4].map((index) => (
              <Path
                d={`M0 ${index * 27 + 8} H220`}
                key={index}
                stroke={gridColor}
                strokeWidth="1"
              />
            ))}
            <Path d={chart.areaPath} fill="url(#lineFill)" />
            <Polyline
              fill="none"
              points={chart.pointsString}
              stroke={BLUE}
              strokeLinecap="round"
              strokeWidth="3"
            />
            {chart.points.map((point) => (
              <Circle cx={point.x} cy={point.y} fill={BLUE} key={`${point.x}-${point.y}`} r="3" />
            ))}
          </Svg>
          <View className="mt-1 flex-row justify-between px-1">
            {['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun'].map((label) => (
              <Text className={TYPE.microMeta} key={label} style={{ color: labelColor }}>
                {label}
              </Text>
            ))}
          </View>
        </View>
      </View>
    </CardShell>
  );
}

function buildLineChart(width: number, height: number, values: number[]) {
  const min = 0;
  const max = 4;
  const top = 8;
  const bottom = height - 12;
  const xStep = width / (values.length - 1);
  const points = values.map((value, index) => ({
    x: index * xStep,
    y: top + ((max - value) / (max - min)) * (bottom - top),
  }));
  const pointsString = points.map((point) => `${point.x},${point.y}`).join(' ');
  const first = points[0];
  const last = points[points.length - 1];
  const linePath = points
    .map((point, index) => `${index === 0 ? 'M' : 'L'} ${point.x},${point.y}`)
    .join(' ');
  const areaPath = `${linePath} L ${last.x},${bottom} L ${first.x},${bottom} Z`;

  return { areaPath, points, pointsString };
}

function TopBalanceCard({ isDark }: { isDark: boolean }) {
  return (
    <CardShell isDark={isDark}>
      <SectionTitle
        icon="bar-chart-outline"
        subtitle="Highest outstanding lien balances ranked by total value and share."
        title="Top 5 Liens By Balance"
      />
      <View className="mt-5 gap-4">
        {SELLING_TOP_BALANCES.map((item) => (
          <View className="flex-row items-center" key={item.name}>
            <BrandMark variant={item.mark} />
            <View className="ml-3 flex-1">
              <Text className={cx(TYPE.rowLabel, 'text-[#2e3138] dark:text-[#f5f5f5]')}>
                {item.name}
              </Text>
              <Text className={cx(TYPE.microMeta, 'mt-0.5 text-[#8d9098] dark:text-[#8f929b]')}>
                {item.subtitle}
              </Text>
            </View>
            <View className="items-end">
              <Text className={cx(TYPE.rowLabel, 'text-[#2e3138] dark:text-[#f5f5f5]')}>
                {item.balance}
              </Text>
              <Text className={cx(TYPE.microMeta, 'mt-0.5 text-[#8d9098] dark:text-[#8f929b]')}>
                {item.share}
              </Text>
            </View>
          </View>
        ))}
      </View>
    </CardShell>
  );
}

function BrandMark({ variant }: { variant: string }) {
  return (
    <View className="h-9 w-9 items-center justify-center rounded-xl bg-[#ede9ff] dark:bg-[#292344]">
      {variant === 'bars' ? (
        <View className="flex-row items-end gap-1">
          {[12, 21, 28].map((height) => (
            <View className="w-1.5 rounded-full bg-[#6254ff]" key={height} style={{ height }} />
          ))}
        </View>
      ) : variant === 'cube' ? (
        <View className="h-6 w-6 rotate-45 rounded-[5px] bg-[#3b82f6]" />
      ) : variant === 'wave' ? (
        <View className="flex-row items-center gap-0.5">
          {[22, 18, 24].map((height) => (
            <View
              className="w-2 rotate-[-30deg] rounded-full bg-[#6254ff]"
              key={height}
              style={{ height }}
            />
          ))}
        </View>
      ) : variant === 'v' ? (
        <View className="h-7 w-6 flex-row items-end gap-1">
          <View className="h-6 w-2 rounded-t-full bg-[#6254ff]" />
          <View className="h-4 w-2 rounded-t-full bg-[#6254ff]" />
        </View>
      ) : (
        <Svg height={26} width={26} viewBox="0 0 26 26">
          <Circle cx="13" cy="13" fill="#6254ff" r="12" />
          <Path d="M13 1 A12 12 0 0 1 25 13 L13 13 Z" fill="#ffffff" opacity="0.9" />
          <Path d="M13 13 L4 22 A12 12 0 0 1 1 13 Z" fill="#ffffff" opacity="0.7" />
        </Svg>
      )}
    </View>
  );
}

function AgingSellerCard({ isDark }: { isDark: boolean }) {
  return (
    <CardShell isDark={isDark}>
      <SectionTitle
        icon="time-outline"
        subtitle="Track overdue timelines and risk statuses by provider."
        title="Aging By Lien Seller"
      />
      <View className="mt-5">
        {SELLING_SELLERS.map((seller, index) => (
          <SellerRiskRow
            expanded={index === 0}
            isLast={index === SELLING_SELLERS.length - 1}
            key={seller.name}
            seller={seller}
          />
        ))}
      </View>
    </CardShell>
  );
}

function SellerRiskRow({
  expanded,
  isLast,
  seller,
}: {
  expanded: boolean;
  isLast: boolean;
  seller: SellerRisk;
}) {
  const riskClass =
    seller.risk === 'High' ? 'bg-[#fde8e9] dark:bg-[#3a1f24]' : 'bg-[#fff4d6] dark:bg-[#3a301c]';
  const riskTextClass = seller.risk === 'High' ? 'text-[#de4b54]' : 'text-[#a77912]';

  return (
    <View
      className={`${isLast ? '' : 'border-b border-[#ececf0] dark:border-[#292a2f]'} pb-4 ${expanded ? '' : 'pt-4'}`}
    >
      <View className="flex-row items-center">
        <Ionicons
          color={MUTED}
          name={expanded ? 'chevron-up-outline' : 'chevron-down-outline'}
          size={15}
        />
        <View className="ml-3 flex-1">
          <Text className={cx(TYPE.rowLabel, 'text-[#3a3d44] dark:text-[#f4f4f5]')}>
            {seller.name}
          </Text>
          <Text className={cx(TYPE.rowMeta, 'mt-2 text-[#8d9098] dark:text-[#8f929b]')}>
            {seller.balance}
          </Text>
        </View>
        <View className="items-end">
          <View className={`rounded-full px-2 py-1 ${riskClass}`}>
            <Text className={`${TYPE.microStrong} ${riskTextClass}`}>● {seller.risk}</Text>
          </View>
          <Text className={cx(TYPE.rowMeta, 'mt-2 text-[#767a84] dark:text-[#a3a4ab]')}>
            {seller.share}
          </Text>
        </View>
      </View>
      {expanded && seller.rows ? (
        <View className="mt-4 gap-4 pl-7">
          {seller.rows.map((row) => (
            <View className="flex-row justify-between" key={row.label}>
              <Text className={cx(TYPE.rowMeta, 'text-[#8d9098] dark:text-[#8f929b]')}>
                {row.label}
              </Text>
              <Text className={cx(TYPE.rowLabel, 'text-[#424650] dark:text-[#e6e6e8]')}>
                {row.value}
              </Text>
            </View>
          ))}
        </View>
      ) : null}
    </View>
  );
}
