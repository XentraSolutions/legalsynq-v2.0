import { render } from '@testing-library/react-native';

import { DashboardReportSkeleton, DashboardStatCardSkeleton } from './index';

describe('dashboard report skeletons', () => {
  it('renders a stat card placeholder', () => {
    const { getByTestId, getAllByTestId } = render(<DashboardStatCardSkeleton isDark={false} />);

    expect(getByTestId('dashboard-stat-skeleton')).toBeTruthy();
    expect(getAllByTestId('skeleton', { includeHiddenElements: true })).toHaveLength(2);
  });

  it('renders the requested legend and summary placeholders', () => {
    const { getByTestId, getAllByTestId } = render(
      <DashboardReportSkeleton hasSummaryRows isDark={false} legendDetailRows={2} legendRows={2} />
    );

    expect(getByTestId('dashboard-report-skeleton')).toBeTruthy();
    expect(getAllByTestId('skeleton', { includeHiddenElements: true })).toHaveLength(24);
  });
});
