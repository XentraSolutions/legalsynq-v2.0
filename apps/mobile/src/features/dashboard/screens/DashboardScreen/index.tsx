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
import type { ReportFilterRequest } from '@/shared/api/endpoints/Cases';
import { BuyingDashboard } from './buying';
import { DashboardHeader } from './DashboardHeader';
import { SellingDashboard } from './selling';
import { ORANGE } from './dashboardShared';

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
          <SellingDashboard
            dashboardSettingsHydrated={dashboardSettingsHydrated}
            dateRange={dateRange}
            isDark={isDark}
            useDummyData={useDashboardDummyData}
          />
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
