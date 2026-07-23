import { fireEvent, render } from '@testing-library/react-native';

import { CaseDetailScreen } from './index';

const mockGoBack = jest.fn();
const mockNavigate = jest.fn();
const mockMutateAsync = jest.fn();
const mockUseCaseDetail = jest.fn();

const caseDetail = {
  accidentType: 'Motor Vehicle',
  caseManager: 'Aaron Law Group',
  caseNumber: '24-18743',
  claimNumber: 'CLM-100',
  clientAddress: '123 Main Street',
  clientDisplayName: 'Marcus Delgado',
  clientDob: '1995-01-10',
  clientEmail: 'marcus@example.com',
  clientFirstName: 'Marcus',
  clientLastName: 'Delgado',
  clientPhone: '555-0100',
  closedAtUtc: null,
  createdAtUtc: '2025-03-15T00:00:00Z',
  dateOfIncident: '2025-03-14',
  demandAmount: 10000,
  description: null,
  externalReference: 'EXT-100',
  id: 'case-1',
  insuranceCarrier: 'Acme Insurance',
  lawFirm: 'Aaron Law Group',
  notes: null,
  openedAtUtc: '2025-03-15T00:00:00Z',
  policyNumber: 'POL-100',
  settlementAmount: null,
  stateOfIncident: 'AZ',
  status: 'PreDemand',
  title: null,
  updatedAtUtc: '2025-03-15T00:00:00Z',
};

jest.mock('@react-navigation/native', () => ({
  useNavigation: () => ({ goBack: mockGoBack, navigate: mockNavigate }),
  useRoute: () => ({ params: { caseId: 'case-1' } }),
}));

jest.mock('@/features/cases/hooks', () => ({
  useAddCaseNote: () => ({ isPending: false, mutateAsync: mockMutateAsync }),
  useCaseDetail: () => mockUseCaseDetail(),
  useCaseNotes: () => ({ data: [], isLoading: false }),
  useCaseUpdates: () => ({ data: [], isLoading: false }),
}));

jest.mock('@/shared/hooks', () => ({
  useToast: () => ({
    showError: jest.fn(),
    showInfo: jest.fn(),
    showSuccess: jest.fn(),
  }),
}));

describe('CaseDetailScreen', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockUseCaseDetail.mockReturnValue({
      data: caseDetail,
      error: null,
      isError: false,
      isLoading: false,
      refetch: jest.fn(),
    });
  });

  it('renders the case header while the detail request is loading', () => {
    mockUseCaseDetail.mockReturnValue({
      data: undefined,
      error: null,
      isError: false,
      isLoading: true,
      refetch: jest.fn(),
    });

    const { getByLabelText, getByText } = render(<CaseDetailScreen />);

    expect(getByText('Case Details')).toBeTruthy();
    expect(getByText('Loading case...')).toBeTruthy();
    expect(getByLabelText('Go back')).toBeTruthy();
  });

  it('renders the Figma summary header, tabs, and ordered case fields', () => {
    const { getByLabelText, getByText, getAllByText } = render(<CaseDetailScreen />);

    expect(getAllByText('Marcus Delgado')).toHaveLength(2);
    expect(getByText('Case ID: 24-18743')).toBeTruthy();
    expect(getByText('Case Summary')).toBeTruthy();
    expect(getAllByText('24-18743').length).toBeGreaterThan(0);
    expect(getByText('Motor Vehicle')).toBeTruthy();
    expect(getByText('Pre-demand')).toBeTruthy();
    expect(getByText('03/14/2025')).toBeTruthy();
    expect(getByText('01/10/1995')).toBeTruthy();
    expect(getByText('AZ')).toBeTruthy();
    expect(getAllByText('Aaron Law Group')).toHaveLength(2);

    fireEvent.press(getByLabelText('Go back'));
    expect(mockGoBack).toHaveBeenCalledTimes(1);
  });

  it('switches between data-backed and template tabs', () => {
    const { getByTestId, getByText, queryByTestId } = render(<CaseDetailScreen />);

    expect(getByTestId('case-summary-page')).toBeTruthy();

    fireEvent.press(getByText('Details'));
    expect(queryByTestId('case-summary-page')).toBeNull();
    expect(getByTestId('case-details-page')).toBeTruthy();
    expect(getByText('Acme Insurance')).toBeTruthy();

    fireEvent.press(getByText('Documents'));
    expect(getByTestId('case-documents-page')).toBeTruthy();
    expect(getByText('This section is ready for case-specific content.')).toBeTruthy();
  });

  it('opens the manage case menu and routes to the payoff quote', () => {
    const { getByLabelText, getByText } = render(<CaseDetailScreen />);

    fireEvent.press(getByLabelText('Manage case'));
    expect(getByText('Manage Case')).toBeTruthy();
    expect(getByText('Merge Case')).toBeTruthy();
    expect(getByText('Delete Case')).toBeTruthy();

    fireEvent.press(getByText('Payoff Quote'));
    expect(mockNavigate).toHaveBeenCalledWith('PayoffQuote', { caseId: 'case-1' });
  });
});
