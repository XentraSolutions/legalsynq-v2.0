import { View } from 'react-native';
import type { DashboardReportType } from '@/features/dashboard/types/types';
import type { DashboardStatRequest, ReportFilterRequest } from '@/shared/api/endpoints/Cases';
import { useDashboardCashReceived, useDashboardDeployed, useDashboardLawFirmCaseReport, useDashboardMedicalProviderReport, useDashboardTotalCaseReport, useDashboardTotalLienReport } from '@/features/dashboard/hooks';
import { StatCardData, BUYING_STATS, BUYING_TOTAL_LIENS, BUYING_TOTAL_CASES, LAW_FIRM_ALLOCATION, FACILITY_ALLOCATION, formatCurrency, mapLawFirmReportGrouped, mapMedicalFacilityReportGrouped, mapTotalLienReportToDashboard, mapTotalCaseReportToDashboard, readStatAmount } from './index';
import { DashboardReportState } from './DashboardReportState';
import { DashboardStatState } from './DashboardStatState';
import { StatGrid } from './StatGrid';
import { DonutCard } from './DonutCard';

export function BuyingDashboard({
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
  const deployedQuery = useDashboardDeployed(statRequest, reportsEnabled);
  const cashReceivedQuery = useDashboardCashReceived(statRequest, reportsEnabled);
  const cashDeployed = readStatAmount(deployedQuery.data);
  const cashReceived = readStatAmount(cashReceivedQuery.data);
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
  const totalLienQuery = useDashboardTotalLienReport(reportFilter, reportsEnabled);
  const totalCaseQuery = useDashboardTotalCaseReport(reportFilter, reportsEnabled);
  const lawFirmQuery = useDashboardLawFirmCaseReport(reportFilter, reportsEnabled);
  const medicalProviderQuery = useDashboardMedicalProviderReport(reportFilter, reportsEnabled);
  const totalLienModel = mapTotalLienReportToDashboard(totalLienQuery.data?.items ?? []);
  const totalCaseModel = mapTotalCaseReportToDashboard(totalCaseQuery.data?.items ?? []);
  const lienSlices = useDummyData ? BUYING_TOTAL_LIENS : (totalLienModel?.slices ?? []);
  const totalLiens = useDummyData ? '239' : (totalLienModel?.totalLiens.toLocaleString() ?? '0');
  const totalPurchaseValue = useDummyData
    ? '$573,775.74'
    : formatCurrency(totalLienModel?.totalPurchase ?? 0);
  const totalLienValue = useDummyData
    ? '$2,287,386.12'
    : formatCurrency(totalLienModel?.totalBilling ?? 0);
  const lawFirmReportSlices = mapLawFirmReportGrouped(lawFirmQuery.data?.items ?? []);
  const lawFirmAllocationSlices = useDummyData ? LAW_FIRM_ALLOCATION : lawFirmReportSlices;
  const lawFirmTotalCases = useDummyData
    ? '175'
    : lawFirmReportSlices.reduce((sum, slice) => sum + slice.value, 0).toLocaleString();
  const facilityReportSlices = mapMedicalFacilityReportGrouped(
    medicalProviderQuery.data?.items ?? []
  );
  const facilityAllocationSlices = useDummyData ? FACILITY_ALLOCATION : facilityReportSlices;
  const facilityTotalCases = useDummyData
    ? '239'
    : facilityReportSlices.reduce((sum, slice) => sum + slice.value, 0).toLocaleString();

  return (
    <>
      {useDummyData ? (
        <StatGrid isDark={isDark} stats={buyingStats} />
      ) : (
        <View className="mt-4 flex-row flex-wrap justify-between gap-y-3">
          <DashboardStatState
            isDark={isDark}
            isError={deployedQuery.isError}
            isLoading={!dashboardSettingsHydrated || deployedQuery.isFetching}
            label={buyingStats[0].label}
            stat={buyingStats[0]}
            onRetry={() => {
              void deployedQuery.refetch();
            }}
          />
          <DashboardStatState
            isDark={isDark}
            isError={cashReceivedQuery.isError}
            isLoading={!dashboardSettingsHydrated || cashReceivedQuery.isFetching}
            label={buyingStats[1].label}
            stat={buyingStats[1]}
            onRetry={() => {
              void cashReceivedQuery.refetch();
            }}
          />
        </View>
      )}
      <DashboardReportState
        hasSummaryRows
        isDark={isDark}
        isError={!useDummyData && totalLienQuery.isError}
        isLoading={!useDummyData && (!dashboardSettingsHydrated || totalLienQuery.isFetching)}
        legendDetailRows={2}
        legendRows={2}
        title="Total Liens"
        onRetry={() => {
          void totalLienQuery.refetch();
        }}
      >
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
      </DashboardReportState>
      <DashboardReportState
        isDark={isDark}
        isError={!useDummyData && totalCaseQuery.isError}
        isLoading={!useDummyData && (!dashboardSettingsHydrated || totalCaseQuery.isFetching)}
        legendRows={4}
        title="Total Cases"
        onRetry={() => {
          void totalCaseQuery.refetch();
        }}
      >
        <DonutCard
          centerCaption="Total Cases"
          centerValue={
            useDummyData ? '4,773' : (totalCaseModel?.totalCases.toLocaleString() ?? '0')
          }
          icon="time-outline"
          isDark={isDark}
          slices={useDummyData ? BUYING_TOTAL_CASES : (totalCaseModel?.slices ?? [])}
          subtitle="Track the overall number of cases and view their current status distribution at a glance."
          title="Total Cases"
          onViewDetails={() => onViewReport('total-cases')}
        />
      </DashboardReportState>
      <DashboardReportState
        isDark={isDark}
        isError={!useDummyData && lawFirmQuery.isError}
        isLoading={!useDummyData && (!dashboardSettingsHydrated || lawFirmQuery.isFetching)}
        legendRows={4}
        title="Law Firm Case Allocation"
        onRetry={() => {
          void lawFirmQuery.refetch();
        }}
      >
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
      </DashboardReportState>
      <DashboardReportState
        isDark={isDark}
        isError={!useDummyData && medicalProviderQuery.isError}
        isLoading={!useDummyData && (!dashboardSettingsHydrated || medicalProviderQuery.isFetching)}
        legendRows={4}
        title="Medical Facility Case Allocation"
        onRetry={() => {
          void medicalProviderQuery.refetch();
        }}
      >
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
      </DashboardReportState>
    </>
  );
}
