import { fireEvent, render, waitFor } from '@testing-library/react-native';

import { ServicingListScreen } from './index';

const mockNavigate = jest.fn();
const mockExport = jest.fn().mockResolvedValue(undefined);
const mockShowSuccess = jest.fn();
const mockServicingCases = Array.from({ length: 6 }, (_, index) => ({
  billingAmount: 20000 + index,
  caseId: `case-${index + 1}`,
  caseNumber: `24-1874${index + 1}`,
  clientName: `Client ${index + 1}`,
  lawFirm: `Law Firm ${index + 1}`,
  purchaseAmount: 10000 + index,
  status: 'PreDemand',
}));

jest.mock('@react-navigation/native', () => ({
  useNavigation: () => ({ navigate: mockNavigate }),
}));

jest.mock('@/features/servicing/hooks', () => ({
  useExportServicingCases: () => ({ isPending: false, mutateAsync: mockExport }),
  useServicingCases: () => ({
    cases: mockServicingCases,
    error: null,
    isError: false,
    isLoading: false,
    isRefetching: false,
    refetchAll: jest.fn(),
    totalCount: mockServicingCases.length,
  }),
}));

jest.mock('@/shared/components/AppMenu', () => ({ AppMenu: () => null }));

jest.mock('@/shared/hooks', () => ({
  useToast: () => ({
    showError: jest.fn(),
    showSuccess: mockShowSuccess,
  }),
}));

describe('ServicingListScreen', () => {
  beforeEach(() => jest.clearAllMocks());

  it('renders five servicing cards per page and opens the case servicing tab', () => {
    const screen = render(<ServicingListScreen />);

    expect(
      screen.getByText('You have 6 servicing cases. Stay on top of their progress and updates.')
    ).toBeTruthy();
    expect(screen.getByText('Client 1')).toBeTruthy();
    expect(screen.queryByText('Client 6')).toBeNull();
    expect(screen.getByText('5 of 6 entries')).toBeTruthy();

    fireEvent.press(screen.getByLabelText('View servicing for 24-18741'));
    expect(mockNavigate).toHaveBeenCalledWith('CaseDetail', {
      caseId: 'case-1',
      initialTab: 'servicing',
    });

    fireEvent.press(screen.getByLabelText('Next page'));
    expect(screen.getByText('Client 6')).toBeTruthy();
    expect(screen.getByText('6 of 6 entries')).toBeTruthy();
  });

  it('exports the current servicing list', async () => {
    const screen = render(<ServicingListScreen />);

    fireEvent.press(screen.getByLabelText('Export servicing cases'));

    await waitFor(() => expect(mockExport).toHaveBeenCalledWith(mockServicingCases));
    expect(mockShowSuccess).toHaveBeenCalledWith('Servicing cases exported successfully');
  });
});
