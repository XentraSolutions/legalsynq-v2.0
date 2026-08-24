import { fireEvent, render, waitFor } from '@testing-library/react-native';

import { MyLiensScreen } from './index';

const mockNavigate = jest.fn();
const mockExport = jest.fn().mockResolvedValue(undefined);
const mockLiens = Array.from({ length: 7 }, (_, index) => ({
  id: `lien-${index + 1}`,
  lienNumber: `LN-${index + 1}`,
  patientName: `Patient ${index + 1}`,
  status: 'Open',
  purchaseAmount: 5000 + index,
  medicalFacility: `Facility ${index + 1}`,
  medicalFacilityId: `facility-${index + 1}`,
  lawFirm: `Law Firm ${index + 1}`,
  lawFirmId: `firm-${index + 1}`,
  caseManager: `Manager ${index + 1}`,
  caseManagerId: `manager-${index + 1}`,
  caseId: `case-${index + 1}`,
  purchaseDate: '2026-01-15',
  closedDate: '',
}));

jest.mock('@react-navigation/native', () => ({
  useNavigation: () => ({ navigate: mockNavigate }),
}));

jest.mock('@/features/liens/hooks', () => ({
  useManagementLiens: () => ({
    liens: mockLiens,
    totalCount: mockLiens.length,
    filterOptions: {
      lawFirmId: [],
      medicalFacilityId: [],
      caseManagerId: [],
      statusId: [],
    },
    isLoading: false,
    isError: false,
    isRefetching: false,
    error: null,
    refetchAll: jest.fn(),
  }),
  useExportLiens: () => ({ isPending: false, mutateAsync: mockExport }),
}));

jest.mock('@/shared/hooks', () => ({
  useToast: () => ({ showError: jest.fn(), showSuccess: jest.fn() }),
}));

jest.mock('@/shared/components/AppMenu', () => ({ AppMenu: () => null }));

describe('MyLiensScreen', () => {
  beforeEach(() => jest.clearAllMocks());

  it('matches case-list pagination and opens management routes', () => {
    const screen = render(<MyLiensScreen />);

    expect(screen.getByText('Patient 1')).toBeTruthy();
    expect(screen.queryByText('Patient 6')).toBeNull();
    expect(screen.getByText('5 of 7 entries')).toBeTruthy();

    fireEvent.press(screen.getByLabelText('Next page'));
    expect(screen.getByText('Patient 6')).toBeTruthy();
    expect(screen.getByText('7 of 7 entries')).toBeTruthy();

    fireEvent.press(screen.getByLabelText('Create lien'));
    expect(mockNavigate).toHaveBeenCalledWith('CreateLien', {});
  });

  it('does not call export until the user confirms', async () => {
    const screen = render(<MyLiensScreen />);

    fireEvent.press(screen.getByLabelText('Export liens'));
    expect(mockExport).not.toHaveBeenCalled();

    fireEvent.press(screen.getByText('Yes, Export'));
    await waitFor(() => expect(mockExport).toHaveBeenCalledTimes(1));
  });
});
