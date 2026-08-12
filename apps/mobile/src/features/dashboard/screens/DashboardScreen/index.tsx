import { useCallback, useMemo, useRef, useState } from 'react';
import { RefreshControl, ScrollView } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useNavigation, type NavigationProp } from '@react-navigation/native';
import { useAtom } from 'jotai';
import { useColorScheme as useNativeWindColorScheme } from 'nativewind';
import { useQueryClient } from '@tanstack/react-query';

import type { DashboardDateRange, DashboardReportType } from '@/features/dashboard/types/types';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { AppMenu } from '@/shared/components/AppMenu';
import { DateRangePicker } from '@/shared/components/DateRangePicker';
import { useDashboardSettings } from '@/shared/hooks/useDashboardSettings';
import { useApiMode } from '@/shared/hooks/useApiMode';
import { useMenuSettings } from '@/shared/hooks/useMenuSettings';
import { accountModeAtom } from '@/shared/state/atoms';
import type {
  DashboardLawFirmCaseReportRow,
  DashboardMedicalProviderReportRow,
  DashboardStatResponse,
  DashboardTotalCaseReportRow,
  DashboardTotalLienReportRow,
  ReportFilterRequest,
} from '@/shared/api/endpoints/Cases';
import { BuyingDashboard } from './BuyingDashboard';
import { DashboardHeader } from './DashboardHeader';
import { SellingDashboard } from './SellingDashboard';

export interface StatCardData {
  label: string;
  value: string;
  trend: string;
  trendTone: 'positive' | 'negative';
}

export interface DonutSlice {
  label: string;
  value: number;
  color: string;
  amount?: string;
  percent?: string;
  details?: Array<{ label: string; value: string }>;
}

export interface SellerRisk {
  name: string;
  balance: string;
  share: string;
  risk: 'High' | 'Medium';
  rows?: Array<{ label: string; value: string }>;
}

export const ORANGE = '#f97332';
export const BLUE = '#3b82f6';
export const GREEN = '#22c55e';
export const YELLOW = '#f5b800';
export const RED = '#ef4444';
export const MUTED = '#8f929b';

export const SELLING_STATS: StatCardData[] = [
  { label: 'Total Lien Revenue', value: '$4,782,350.72', trend: '8.9%', trendTone: 'positive' },
  { label: 'Total Outstanding', value: '$3,842,196.18', trend: '6.4%', trendTone: 'positive' },
  { label: 'Past Amount Due', value: '$1,287,542.63', trend: '8.9%', trendTone: 'positive' },
  { label: 'Payments', value: '$635,251.44', trend: '5.0%', trendTone: 'negative' },
];

export const BUYING_STATS: StatCardData[] = [
  { label: 'Cash Deployed', value: '$573,775.74', trend: '8.9%', trendTone: 'positive' },
  { label: 'Cash Received', value: '$3,842,196.18', trend: '6.4%', trendTone: 'positive' },
];

export const SELLING_AGING: DonutSlice[] = [
  { label: '0-30 Days', value: 32.7, amount: '$1,125,842.50', percent: '(32.7%)', color: BLUE },
  { label: '31-60 Days', value: 21.2, amount: '$987,651.22', percent: '(21.2%)', color: ORANGE },
  { label: '61-90 Days', value: 19.2, amount: '$987,651.22', percent: '(19.2%)', color: GREEN },
  { label: '91-120 Days', value: 11.2, amount: '$754,221.17', percent: '(11.2%)', color: YELLOW },
  { label: '120+ Days', value: 10.8, amount: '$411,601.15', percent: '(10.8%)', color: RED },
];

export const SELLING_STATUS: DonutSlice[] = [
  { label: 'Active', value: 67.5, amount: '842', percent: '(67.5%)', color: BLUE },
  { label: 'Settled', value: 17.1, amount: '214', percent: '(17.1%)', color: ORANGE },
  { label: 'In Reduction', value: 9, amount: '112', percent: '(9.0%)', color: GREEN },
  { label: 'Paid', value: 4.5, amount: '56', percent: '(4.5%)', color: YELLOW },
  { label: 'Other / Closed', value: 1.9, amount: '24', percent: '(1.9%)', color: RED },
];

export const SELLING_TOP_BALANCES = [
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

export const SELLING_SELLERS: SellerRisk[] = [
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

export const BUYING_TOTAL_LIENS: DonutSlice[] = [
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

export const BUYING_TOTAL_CASES: DonutSlice[] = [
  { label: 'Case Settled', value: 93.83, amount: '4,479', percent: '(93.83%)', color: BLUE },
  { label: 'Closed', value: 2.51, amount: '120', percent: '(2.51%)', color: ORANGE },
  { label: 'Litigation (Open)', value: 2.39, amount: '114', percent: '(2.39%)', color: GREEN },
  { label: 'Demand Sent', value: 1.26, amount: '60', percent: '(1.26%)', color: YELLOW },
];

export const LAW_FIRM_ALLOCATION: DonutSlice[] = [
  { label: 'James Law Group', value: 42.86, amount: '75', percent: '(42.86%)', color: BLUE },
  { label: 'Adam Associates', value: 22.86, amount: '40', percent: '(22.86%)', color: ORANGE },
  { label: 'Anthony Injury Law', value: 17.14, amount: '30', percent: '(17.14%)', color: GREEN },
  { label: 'Benson & Bingham', value: 17.14, amount: '30', percent: '(17.14%)', color: YELLOW },
];

export const FACILITY_ALLOCATION: DonutSlice[] = [
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

export const LINE_POINTS = [2.4, 3.7, 2.6, 1.0, 2.5, 2.6];
export const ALL_DATES_RANGE: DashboardDateRange = { startDate: '', endDate: '' };

export function buildDashboardReportFilter(dateRange: DashboardDateRange): ReportFilterRequest {
  const filter: ReportFilterRequest = {
    page: 1,
    limit: 1000000,
  };

  if (dateRange.startDate && dateRange.endDate) {
    filter.startDate = dateRange.startDate;
    filter.endDate = dateRange.endDate;
  }

  return filter;
}

export function DashboardScreen() {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const queryClient = useQueryClient();
  const { colorScheme } = useNativeWindColorScheme();
  const [accountMode] = useAtom(accountModeAtom);
  const [drawerVisible, setDrawerVisible] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const refreshInFlightRef = useRef(false);
  const [dateRange, setDateRange] = useState<DashboardDateRange>(ALL_DATES_RANGE);
  const isDark = colorScheme === 'dark';
  const { hydrated: dashboardSettingsHydrated, settings: dashboardSettings } =
    useDashboardSettings();
  const { settings: menuVisibility } = useMenuSettings();
  const { mode: apiMode } = useApiMode();
  const useDashboardDummyData = dashboardSettings.useDummyData;
  const reportFilter = useMemo(() => buildDashboardReportFilter(dateRange), [dateRange]);
  const handleViewReport = (reportType: DashboardReportType) => {
    navigation.navigate('DashboardReportDetail', { reportType, dateRange });
  };
  const handleRefresh = useCallback(async () => {
    if (refreshInFlightRef.current || !dashboardSettingsHydrated || useDashboardDummyData) {
      return;
    }

    refreshInFlightRef.current = true;
    setIsRefreshing(true);

    try {
      await queryClient.refetchQueries({ queryKey: ['dashboard'], type: 'active' });
    } finally {
      refreshInFlightRef.current = false;
      setIsRefreshing(false);
    }
  }, [dashboardSettingsHydrated, queryClient, useDashboardDummyData]);

  return (
    <SafeAreaView edges={['top']} className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <ScrollView
        className="flex-1 px-4"
        contentContainerStyle={{ paddingBottom: 26 }}
        refreshControl={
          <RefreshControl
            colors={[ORANGE]}
            enabled={dashboardSettingsHydrated && !useDashboardDummyData}
            progressBackgroundColor={isDark ? '#191a1f' : '#ffffff'}
            refreshing={isRefreshing}
            tintColor={ORANGE}
            onRefresh={() => {
              void handleRefresh();
            }}
          />
        }
        showsVerticalScrollIndicator={false}
      >
        <DashboardHeader
          accountMode={accountMode}
          isDark={isDark}
          onOpenMenu={() => setDrawerVisible(true)}
          showXenia={apiMode === 'current' && menuVisibility.xeniaAi}
          onOpenXenia={() => navigation.navigate('XeniaAI')}
        />
        <DateRangePicker
          allowAllDates
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

export const SLICE_COLORS = [BLUE, ORANGE, GREEN, YELLOW, RED];

export function formatCurrency(value: number): string {
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

export function mapLawFirmReportGrouped(rows: DashboardLawFirmCaseReportRow[]): DonutSlice[] {
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

export function mapMedicalFacilityReportGrouped(
  rows: DashboardMedicalProviderReportRow[]
): DonutSlice[] {
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

export function mapTotalLienReportToDashboard(rows: DashboardTotalLienReportRow[]):
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

export function mapTotalCaseReportToDashboard(rows: DashboardTotalCaseReportRow[]):
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

export function readStatAmount(data: DashboardStatResponse | undefined): number | undefined {
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

export const LEGEND_PAGE_SIZE = 5;

export function buildLineChart(width: number, height: number, values: number[]) {
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
