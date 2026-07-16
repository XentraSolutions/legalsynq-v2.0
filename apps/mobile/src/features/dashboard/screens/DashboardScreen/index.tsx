import { useMemo, useState, type ReactNode } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, type NavigationProp } from '@react-navigation/native';
import Svg, { Circle, Defs, LinearGradient, Path, Polyline, Stop } from 'react-native-svg';
import { useAtom } from 'jotai';
import { useColorScheme as useNativeWindColorScheme } from 'nativewind';

import {
  useDashboardCashReceived,
  useDashboardDeployed,
  useDashboardLawFirmCaseReport,
  useDashboardMedicalProviderReport,
  useDashboardTotalCaseReport,
  useDashboardTotalLienReport,
} from '@/features/dashboard/hooks';
import type { DashboardDateRange, DashboardReportType } from '@/features/dashboard/types/types';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { AppMenu } from '@/shared/components/AppMenu';
import { DateRangePicker } from '@/shared/components/DateRangePicker';
import { useDashboardSettings } from '@/shared/hooks/useDashboardSettings';
import { accountModeAtom, type AccountMode } from '@/shared/state/atoms';
import { cx, FIGMA_COLORS, FIGMA_TEXT as TYPE } from '@/shared/styles';
import type {
  DashboardLawFirmCaseReportRow,
  DashboardMedicalProviderReportRow,
  DashboardStatRequest,
  DashboardStatResponse,
  DashboardTotalCaseReportRow,
  DashboardTotalLienReportRow,
  ReportFilterRequest,
} from '@/shared/api/endpoints/Cases';
import { useAuth } from '@/shared';

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
const DEFAULT_DASHBOARD_DATE = new Date();

function padDatePart(value: number): string {
  return String(value).padStart(2, '0');
}

function formatApiDate(date: Date): string {
  return `${padDatePart(date.getMonth() + 1)}/${padDatePart(date.getDate())}/${date.getFullYear()}`;
}

function createSingleDayRange(date: Date): DashboardDateRange {
  const end = formatApiDate(date);
  const start = new Date(date);
  const lastMonth = new Date(start.setMonth(start.getMonth() - 1));
  return { startDate: formatApiDate(lastMonth), endDate: end };
}

function buildDashboardReportFilter(dateRange: DashboardDateRange): ReportFilterRequest {
  return {
    page: 1,
    limit: 1000000,
    startDate: dateRange.startDate,
    endDate: dateRange.endDate,
  };
}

export function DashboardScreen() {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const { colorScheme } = useNativeWindColorScheme();
  const [accountMode] = useAtom(accountModeAtom);
  const [drawerVisible, setDrawerVisible] = useState(false);
  const [dateRange, setDateRange] = useState<DashboardDateRange>(() =>
    createSingleDayRange(DEFAULT_DASHBOARD_DATE)
  );
  const isDark = colorScheme === 'dark';
  const { hydrated: dashboardSettingsHydrated, settings: dashboardSettings } =
    useDashboardSettings();
  const useDashboardDummyData = dashboardSettings.useDummyData;
  const reportFilter = useMemo(() => buildDashboardReportFilter(dateRange), [dateRange]);
  const handleViewReport = (reportType: DashboardReportType) => {
    navigation.navigate('DashboardReportDetail', { reportType, dateRange });
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
        <DateRangePicker
          containerClassName="mt-4"
          isDark={isDark}
          modalDescription="Filter dashboard reports by selected start and end dates."
          value={dateRange}
          onChange={setDateRange}
        />
        {accountMode === 'selling' ? (
          <SellingDashboard isDark={isDark} useDummyData={useDashboardDummyData} />
        ) : (
          <BuyingDashboard
            dashboardSettingsHydrated={dashboardSettingsHydrated}
            isDark={isDark}
            reportFilter={reportFilter}
            useDummyData={useDashboardDummyData}
            onViewReport={handleViewReport}
          />
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
  const { user } = useAuth();
  const userName = user ? `${user.firstName}`.trim() : '';
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
          Welcome, {userName}
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

function SellingDashboard({ isDark, useDummyData }: { isDark: boolean; useDummyData: boolean }) {
  if (!useDummyData) {
    return (
      <DashboardEmptyStateCard
        isDark={isDark}
        message="Selling report data is not available from the API yet."
        title="No selling report data"
      />
    );
  }

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
  // scan all string fields longer than 2 chars, skipping known non-name fields
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

function mapLawFirmReportGrouped(rows: DashboardLawFirmCaseReportRow[]): DonutSlice[] {
  const groups = new Map<string, { label: string; count: number }>();
  for (const row of rows) {
    const r = row as Record<string, unknown>;
    const name = readLawFirmName(row);
    const rowCount = readReportNumber(r, ['totalCases', 'totalCase', 'caseCount', 'cases']) ?? 1;
    const existing = groups.get(name);
    if (existing) {
      existing.count += rowCount;
    } else {
      groups.set(name, { label: name, count: rowCount });
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

function mapMedicalFacilityReportGrouped(rows: DashboardMedicalProviderReportRow[]): DonutSlice[] {
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

function readReportText(row: Record<string, unknown>, keys: string[]): string | undefined {
  for (const key of keys) {
    const value = row[key];
    if (typeof value === 'string' && value.trim().length > 0) {
      return value.trim();
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

function normalizeLienStatusLabel(label: string | undefined): 'Open' | 'Close' {
  const normalized = label?.toLowerCase() ?? '';
  return normalized.includes('close') ||
    normalized.includes('settled') ||
    normalized.includes('paid')
    ? 'Close'
    : 'Open';
}

function mapTotalLienReportToDashboard(rows: DashboardTotalLienReportRow[]):
  | {
      slices: DonutSlice[];
      totalBilling: number;
      totalLiens: number;
      totalPurchase: number;
    }
  | undefined {
  if (rows.length === 0) {
    return undefined;
  }

  const grouped = new Map<'Open' | 'Close', { billing: number; count: number; purchase: number }>();

  rows.forEach((row) => {
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
    const current = grouped.get(status) ?? { billing: 0, count: 0, purchase: 0 };
    current.count +=
      readReportNumber(record, [
        'totalLiens',
        'liensCount',
        'lienCount',
        'count',
        'total',
        'value',
      ]) ?? 1;
    current.purchase +=
      readReportNumber(record, [
        'totalPurchaseAmount',
        'totalPurchase',
        'purchaseAmount',
        'purchase',
      ]) ?? 0;
    current.billing +=
      readReportNumber(record, [
        'totalBillingAmount',
        'totalBilling',
        'billingAmount',
        'billing',
      ]) ?? 0;
    grouped.set(status, current);
  });

  const totalLiens = Array.from(grouped.values()).reduce((sum, item) => sum + item.count, 0);
  if (totalLiens <= 0) {
    return undefined;
  }

  const orderedStatuses: Array<'Open' | 'Close'> = ['Open', 'Close'];
  const slices = orderedStatuses.reduce<DonutSlice[]>((items, status, index) => {
    const item = grouped.get(status);
    if (!item || item.count <= 0) {
      return items;
    }

    const percentage = (item.count / totalLiens) * 100;
    items.push({
      label: status,
      value: item.count,
      amount: item.count.toLocaleString(),
      percent: `(${percentage.toFixed(1)}%)`,
      color: index === 0 ? BLUE : ORANGE,
      details: [
        { label: 'Purchase', value: formatCurrency(item.purchase) },
        { label: 'Billing', value: formatCurrency(item.billing) },
      ],
    });

    return items;
  }, []);

  return {
    slices,
    totalBilling: Array.from(grouped.values()).reduce((sum, item) => sum + item.billing, 0),
    totalLiens,
    totalPurchase: Array.from(grouped.values()).reduce((sum, item) => sum + item.purchase, 0),
  };
}

function mapTotalCaseReportToDashboard(rows: DashboardTotalCaseReportRow[]):
  | {
      slices: DonutSlice[];
      totalCases: number;
    }
  | undefined {
  if (rows.length === 0) {
    return undefined;
  }

  const grouped = new Map<string, number>();

  rows.forEach((row) => {
    const record = row as Record<string, unknown>;
    const label =
      readReportText(record, [
        'caseStatus',
        'currentStatus',
        'status',
        'statusName',
        'label',
        'name',
      ]) ?? 'Unknown';
    const count =
      readReportNumber(record, ['totalCases', 'caseCount', 'cases', 'count', 'total', 'value']) ??
      1;
    grouped.set(label, (grouped.get(label) ?? 0) + count);
  });

  const totalCases = Array.from(grouped.values()).reduce((sum, count) => sum + count, 0);
  if (totalCases <= 0) {
    return undefined;
  }

  const slices = Array.from(grouped.entries()).map(([label, count], index) => {
    const percentage = (count / totalCases) * 100;
    return {
      label,
      value: count,
      amount: count.toLocaleString(),
      percent: `(${percentage.toFixed(2)}%)`,
      color: SLICE_COLORS[index % SLICE_COLORS.length],
    };
  });

  return { slices, totalCases };
}

function readStatAmount(data: DashboardStatResponse | undefined): number | undefined {
  if (!data) return undefined;
  const r = data as Record<string, unknown>;
  for (const key of [
    'totalAmount',
    'total',
    'amount',
    'value',
    'cashDeployed',
    'deployed',
    'cashReceived',
    'received',
  ]) {
    const raw = r[key];
    if (typeof raw === 'number' && Number.isFinite(raw)) return raw;
    if (typeof raw === 'string') {
      const parsed = Number(raw.replace(/[^0-9.-]/g, ''));
      if (Number.isFinite(parsed) && parsed > 0) return parsed;
    }
  }
  return undefined;
}

function BuyingDashboard({
  dashboardSettingsHydrated,
  isDark,
  reportFilter,
  useDummyData,
  onViewReport,
}: {
  dashboardSettingsHydrated: boolean;
  isDark: boolean;
  reportFilter: ReportFilterRequest;
  useDummyData: boolean;
  onViewReport: (reportType: DashboardReportType) => void;
}) {
  const reportsEnabled = dashboardSettingsHydrated && !useDummyData;
  const statRequest: DashboardStatRequest = {
    fromDate: reportFilter.startDate ?? '',
    toDate: reportFilter.endDate ?? '',
  };
  const { data: deployedData } = useDashboardDeployed(statRequest, reportsEnabled);
  const { data: cashReceivedData } = useDashboardCashReceived(statRequest, reportsEnabled);
  const cashDeployed = readStatAmount(deployedData);
  const cashReceived = readStatAmount(cashReceivedData);
  const buyingStats: StatCardData[] = useDummyData
    ? BUYING_STATS
    : [
        {
          label: 'Cash Deployed',
          value: cashDeployed !== undefined ? formatCurrency(cashDeployed) : '—',
          trend: '0%',
          trendTone: 'positive',
        },
        {
          label: 'Cash Received',
          value: cashReceived !== undefined ? formatCurrency(cashReceived) : '—',
          trend: '0%',
          trendTone: 'positive',
        },
      ];
  const { data: totalLienReport } = useDashboardTotalLienReport(reportFilter, reportsEnabled);
  const { data: totalCaseReport } = useDashboardTotalCaseReport(reportFilter, reportsEnabled);
  const { data: lawFirmReport } = useDashboardLawFirmCaseReport(reportFilter, reportsEnabled);
  const { data: medicalProviderReport } = useDashboardMedicalProviderReport(
    reportFilter,
    reportsEnabled
  );
  const totalLienModel = mapTotalLienReportToDashboard(totalLienReport?.items ?? []);
  const totalCaseModel = mapTotalCaseReportToDashboard(totalCaseReport?.items ?? []);
  const lienSlices = useDummyData ? BUYING_TOTAL_LIENS : (totalLienModel?.slices ?? []);
  const totalLiens = useDummyData ? '239' : (totalLienModel?.totalLiens.toLocaleString() ?? '0');
  const totalPurchaseValue = useDummyData
    ? '$573,775.74'
    : formatCurrency(totalLienModel?.totalPurchase ?? 0);
  const totalLienValue = useDummyData
    ? '$2,287,386.12'
    : formatCurrency(totalLienModel?.totalBilling ?? 0);
  const lawFirmReportSlices = mapLawFirmReportGrouped(lawFirmReport?.items ?? []);
  const lawFirmAllocationSlices = useDummyData ? LAW_FIRM_ALLOCATION : lawFirmReportSlices;
  const lawFirmTotalCases = useDummyData
    ? '175'
    : lawFirmReportSlices.reduce((sum, slice) => sum + slice.value, 0).toLocaleString();
  const facilityReportSlices = mapMedicalFacilityReportGrouped(medicalProviderReport?.items ?? []);
  const facilityAllocationSlices = useDummyData ? FACILITY_ALLOCATION : facilityReportSlices;
  const facilityTotalCases = useDummyData
    ? '239'
    : facilityReportSlices.reduce((sum, slice) => sum + slice.value, 0).toLocaleString();

  return (
    <>
      <StatGrid isDark={isDark} stats={buyingStats} />
      <DonutCard
        centerCaption="Total Liens"
        centerValue={totalLiens}
        icon="time-outline"
        isDark={isDark}
        slices={lienSlices}
        subtitle="Breakdown of open and closed claims with total purchase and billing values."
        summaryRows={[
          { label: 'Total Purchase Amount', value: totalPurchaseValue },
          { label: 'Total Billing Amount', value: totalLienValue },
        ]}
        title="Total Liens"
        onViewDetails={() => onViewReport('total-liens')}
      />
      <DonutCard
        centerCaption="Total Cases"
        centerValue={useDummyData ? '4,773' : (totalCaseModel?.totalCases.toLocaleString() ?? '0')}
        icon="time-outline"
        isDark={isDark}
        slices={useDummyData ? BUYING_TOTAL_CASES : (totalCaseModel?.slices ?? [])}
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
          {/* <View
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
          </View> */}
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

function DashboardEmptyStateCard({
  isDark,
  message,
  title,
}: {
  isDark: boolean;
  message: string;
  title: string;
}) {
  return (
    <CardShell isDark={isDark}>
      <View className="items-center py-6">
        <View className="h-12 w-12 items-center justify-center rounded-full bg-[#ececee] dark:bg-[#2a2b30]">
          <Ionicons color={MUTED} name="analytics-outline" size={22} />
        </View>
        <Text className={cx(TYPE.cardTitle, 'mt-4 text-center text-[#24272d] dark:text-white')}>
          {title}
        </Text>
        <Text
          className={cx(
            TYPE.cardDescription,
            'mt-2 text-center text-[#8d9098] dark:text-[#8f929b]'
          )}
        >
          {message}
        </Text>
      </View>
    </CardShell>
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

const LEGEND_PAGE_SIZE = 5;

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
  const [legendPage, setLegendPage] = useState(1);
  const totalLegendPages = Math.max(1, Math.ceil(slices.length / LEGEND_PAGE_SIZE));
  const currentLegendPage = Math.min(legendPage, totalLegendPages);
  const pagedSlices = slices.slice(
    (currentLegendPage - 1) * LEGEND_PAGE_SIZE,
    currentLegendPage * LEGEND_PAGE_SIZE
  );

  return (
    <CardShell isDark={isDark}>
      <SectionTitle icon={icon} subtitle={subtitle} title={title} />
      <DonutChart centerCaption={centerCaption} centerValue={centerValue} slices={slices} />
      {slices.length > 0 ? (
        <>
          <View className="mt-4">
            {pagedSlices.map((slice, index) => (
              <LegendRow
                key={slice.label}
                isLast={index === pagedSlices.length - 1}
                slice={slice}
              />
            ))}
          </View>
          {slices.length > LEGEND_PAGE_SIZE ? (
            <LegendPagination
              page={currentLegendPage}
              totalPages={totalLegendPages}
              onNext={() => setLegendPage((page) => Math.min(totalLegendPages, page + 1))}
              onPrevious={() => setLegendPage((page) => Math.max(1, page - 1))}
            />
          ) : null}
        </>
      ) : (
        <Text className={cx(TYPE.rowMuted, 'mt-5 text-center text-[#8d9098] dark:text-[#8f929b]')}>
          No report data available for the selected date range.
        </Text>
      )}
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
  const total = slices.reduce((sum, slice) => sum + slice.value, 0) || 1;
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

function LegendPagination({
  page,
  totalPages,
  onNext,
  onPrevious,
}: {
  page: number;
  totalPages: number;
  onNext: () => void;
  onPrevious: () => void;
}) {
  const canGoPrevious = page > 1;
  const canGoNext = page < totalPages;

  return (
    <View className="mt-3 flex-row items-center justify-between border-t border-[#ececf0] pt-3 dark:border-[#292a2f]">
      <Pressable
        accessibilityRole="button"
        className={cx(
          'h-8 flex-row items-center gap-1 rounded-full border border-[#dedee0] px-3 dark:border-[#33343a]',
          !canGoPrevious && 'opacity-50'
        )}
        disabled={!canGoPrevious}
        onPress={onPrevious}
      >
        <Ionicons
          color={canGoPrevious ? '#71717a' : '#a1a1aa'}
          name="chevron-back-outline"
          size={14}
        />
        <Text className={cx(TYPE.rowValue, 'text-[#22252b] dark:text-white')}>Previous</Text>
      </Pressable>
      <Text className={cx(TYPE.rowMuted, 'text-[#8d9098] dark:text-[#8f929b]')}>
        Page {page} of {totalPages}
      </Text>
      <Pressable
        accessibilityRole="button"
        className={cx(
          'h-8 flex-row items-center gap-1 rounded-full border border-[#dedee0] px-3 dark:border-[#33343a]',
          !canGoNext && 'opacity-50'
        )}
        disabled={!canGoNext}
        onPress={onNext}
      >
        <Text className={cx(TYPE.rowValue, 'text-[#22252b] dark:text-white')}>Next</Text>
        <Ionicons
          color={canGoNext ? '#22252b' : '#a1a1aa'}
          name="chevron-forward-outline"
          size={14}
        />
      </Pressable>
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
