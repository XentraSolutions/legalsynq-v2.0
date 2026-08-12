import { act, fireEvent, render } from '@testing-library/react-native';
import { RefreshControl } from 'react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { ALL_DATES_RANGE, buildDashboardReportFilter, DashboardScreen } from './index';

const mockNavigate = jest.fn();
const mockRefetch = jest.fn(() => Promise.resolve());
const mockUseDashboardCashReceived = jest.fn();
const mockUseDashboardDeployed = jest.fn();
const mockQueryResults = {
  cashReceived: createQueryResult(),
  deployed: createQueryResult(),
  lawFirm: createQueryResult(),
  medicalProvider: createQueryResult(),
  totalCase: createQueryResult(),
  totalLien: createQueryResult(),
};

function createQueryResult() {
  return {
    data: undefined,
    isError: false,
    isFetching: true,
    refetch: mockRefetch,
  };
}

jest.mock('@react-navigation/native', () => ({
  useNavigation: () => ({ navigate: mockNavigate }),
}));

jest.mock('jotai', () => ({
  atom: (initialValue: unknown) => ({ initialValue }),
  useAtom: () => ['buying'],
}));

jest.mock('nativewind', () => ({
  useColorScheme: () => ({ colorScheme: 'light' }),
}));

jest.mock('@/shared', () => ({
  useAuth: () => ({ user: { firstName: 'Alex' } }),
}));

jest.mock('@/shared/hooks/useDashboardSettings', () => ({
  useDashboardSettings: () => ({ hydrated: true, settings: { useDummyData: false } }),
}));

jest.mock('@/shared/hooks/useMenuSettings', () => ({
  useMenuSettings: () => ({ settings: { xeniaAi: true } }),
}));

jest.mock('@/shared/hooks/useApiMode', () => ({
  useApiMode: () => ({ mode: 'current' }),
}));

jest.mock('@/shared/components/AppMenu', () => ({
  AppMenu: () => null,
}));

jest.mock('@/shared/components/DateRangePicker', () => ({
  DateRangePicker: () => null,
}));

jest.mock('@/features/dashboard/components', () => {
  const React = require('react');
  const { View } = require('react-native');

  return {
    DashboardReportSkeleton: () =>
      React.createElement(View, { testID: 'dashboard-report-skeleton' }),
    DashboardStatCardSkeleton: () =>
      React.createElement(View, { testID: 'dashboard-stat-skeleton' }),
  };
});

jest.mock('@/features/dashboard/hooks', () => ({
  useDashboardCashReceived: (...args: unknown[]) => {
    mockUseDashboardCashReceived(...args);
    return mockQueryResults.cashReceived;
  },
  useDashboardDeployed: (...args: unknown[]) => {
    mockUseDashboardDeployed(...args);
    return mockQueryResults.deployed;
  },
  useDashboardLawFirmCaseReport: () => mockQueryResults.lawFirm,
  useDashboardMedicalProviderReport: () => mockQueryResults.medicalProvider,
  useDashboardTotalCaseReport: () => mockQueryResults.totalCase,
  useDashboardTotalLienReport: () => mockQueryResults.totalLien,
}));

function renderScreen(queryClient = new QueryClient()) {
  return render(
    <QueryClientProvider client={queryClient}>
      <DashboardScreen />
    </QueryClientProvider>
  );
}

describe('DashboardScreen', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    Object.values(mockQueryResults).forEach((result) => {
      result.data = undefined;
      result.isError = false;
      result.isFetching = true;
    });
  });

  it('omits date parameters for the default All Dates filter', () => {
    expect(buildDashboardReportFilter(ALL_DATES_RANGE)).toEqual({ page: 1, limit: 1000000 });
  });

  it('passes dashboard pagination to deployed and cash-received statistics', () => {
    renderScreen();

    const expectedRequest = { startDate: '', endDate: '', page: 1, limit: 1000000 };
    expect(mockUseDashboardDeployed).toHaveBeenCalledWith(expectedRequest, true);
    expect(mockUseDashboardCashReceived).toHaveBeenCalledWith(expectedRequest, true);
  });

  it('passes matching start and end dates for a custom filter', () => {
    expect(buildDashboardReportFilter({ startDate: '01/01/2026', endDate: '01/31/2026' })).toEqual({
      page: 1,
      limit: 1000000,
      startDate: '01/01/2026',
      endDate: '01/31/2026',
    });
  });

  it('renders each API-backed report skeleton independently', () => {
    const { getAllByTestId } = renderScreen();

    expect(getAllByTestId('dashboard-stat-skeleton')).toHaveLength(2);
    expect(getAllByTestId('dashboard-report-skeleton')).toHaveLength(4);
  });

  it('opens Xenia AI from the dashboard shortcut', () => {
    const { getByLabelText } = renderScreen();

    fireEvent.press(getByLabelText('Open Xenia AI'));
    expect(mockNavigate).toHaveBeenCalledWith('XeniaAI');
  });

  it('removes only the skeleton for a report that has settled', () => {
    mockQueryResults.totalLien.isFetching = false;

    const { getAllByTestId } = renderScreen();

    expect(getAllByTestId('dashboard-report-skeleton')).toHaveLength(3);
  });

  it('replaces a failed report skeleton with a retry state', () => {
    mockQueryResults.totalCase.isFetching = false;
    mockQueryResults.totalCase.isError = true;

    const { getByText, getAllByTestId } = renderScreen();

    expect(getByText('Total Cases could not be loaded')).toBeTruthy();
    expect(getAllByTestId('dashboard-report-skeleton')).toHaveLength(3);

    fireEvent.press(getByText('Retry'));
    expect(mockRefetch).toHaveBeenCalledTimes(1);
  });

  it('prevents overlapping pull-to-refresh requests', async () => {
    const queryClient = new QueryClient();
    let finishRefresh: (() => void) | undefined;
    const refreshPromise = new Promise<void>((resolve) => {
      finishRefresh = resolve;
    });
    const refetchQueries = jest.fn(() => refreshPromise);
    queryClient.refetchQueries = refetchQueries;
    const { UNSAFE_getByType } = renderScreen(queryClient);
    const refreshControl = UNSAFE_getByType(RefreshControl);

    await act(async () => {
      refreshControl.props.onRefresh();
      refreshControl.props.onRefresh();
      await Promise.resolve();
    });

    expect(refetchQueries).toHaveBeenCalledTimes(1);
    expect(refetchQueries).toHaveBeenCalledWith({ queryKey: ['dashboard'], type: 'active' });
    expect(UNSAFE_getByType(RefreshControl).props.refreshing).toBe(true);

    await act(async () => {
      finishRefresh?.();
      await refreshPromise;
    });

    expect(UNSAFE_getByType(RefreshControl).props.refreshing).toBe(false);
  });
});
