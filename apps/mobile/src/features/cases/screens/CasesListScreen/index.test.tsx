import { fireEvent, render } from '@testing-library/react-native';

import { CasesListScreen } from './index';

const mockNavigate = jest.fn();
const mockMutateAsync = jest.fn();
const mockCases = Array.from({ length: 7 }, (_, index) => ({
  accidentType: `Accident ${index + 1}`,
  accidentTypeId: `accident-${index + 1}`,
  caseManager: `Manager ${index + 1}`,
  caseManagerId: `manager-${index + 1}`,
  caseNumber: `CASE-${index + 1}`,
  clientName: `Client ${index + 1}`,
  dateOfLoss: '2025-03-14',
  id: `case-${index + 1}`,
  lawFirm: `Law Firm ${index + 1}`,
  lawFirmId: `firm-${index + 1}`,
  status: 'Open',
  updatedAt: '2025-03-15',
}));

jest.mock('@react-navigation/native', () => ({
  useNavigation: () => ({ navigate: mockNavigate }),
}));

jest.mock('@/features/cases/hooks', () => ({
  useCases: () => ({
    cases: mockCases,
    error: null,
    filterOptions: {
      accidentTypeId: [],
      caseManagerId: [],
      lawFirmId: [],
      statusId: [],
    },
    isError: false,
    isLoading: false,
    isRefetching: false,
    refetch: jest.fn(),
    totalCount: mockCases.length,
  }),
  useExportCases: () => ({ isPending: false, mutateAsync: mockMutateAsync }),
}));

jest.mock('@/shared/hooks', () => ({
  useToast: () => ({ showError: jest.fn() }),
}));

jest.mock('@/shared/components/AppMenu', () => ({ AppMenu: () => null }));

describe('CasesListScreen', () => {
  beforeEach(() => jest.clearAllMocks());

  it('places create and export in the top action bar and exposes the adjacent filter action', () => {
    const { getByLabelText } = render(<CasesListScreen />);

    expect(getByLabelText('Export cases')).toBeTruthy();
    expect(getByLabelText('Create case')).toBeTruthy();
    expect(getByLabelText('Filter cases')).toBeTruthy();

    fireEvent.press(getByLabelText('Create case'));
    expect(mockNavigate).toHaveBeenCalledWith('CreateCase');
  });

  it('moves forward and backward through five-case pages', () => {
    const { getByLabelText, getByText, queryByText } = render(<CasesListScreen />);

    expect(getByText('Client 1')).toBeTruthy();
    expect(queryByText('Client 6')).toBeNull();
    expect(getByText('5 of 7 entries')).toBeTruthy();

    fireEvent.press(getByLabelText('Next page'));
    expect(queryByText('Client 1')).toBeNull();
    expect(getByText('Client 6')).toBeTruthy();
    expect(getByText('7 of 7 entries')).toBeTruthy();

    fireEvent.press(getByLabelText('Previous page'));
    expect(getByText('Client 1')).toBeTruthy();
    expect(queryByText('Client 6')).toBeNull();
  });
});
