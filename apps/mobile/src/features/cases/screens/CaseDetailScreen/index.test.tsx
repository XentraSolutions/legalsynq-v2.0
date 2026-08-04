import { Alert } from 'react-native';
import { fireEvent, render, waitFor } from '@testing-library/react-native';

import { CaseDetailScreen } from './index';

const mockGoBack = jest.fn();
const mockNavigate = jest.fn();
const mockMutateAsync = jest.fn();
const mockDeleteCase = jest.fn(() => Promise.resolve());
const mockMergeCase = jest.fn(() => Promise.resolve());
const mockUseCaseDetail = jest.fn();
const mockAlert = jest.fn();
const mockRefetchDocuments = jest.fn();
const mockUploadCaseDocument = jest.fn();
const mockCaseLiens = Array.from({ length: 6 }, (_, index) => ({
  id: `lien-${index + 1}`,
  lienNumber: `26-41823-0${index + 1}`,
  patientName: 'Marcus Delgado',
  status: 'Open',
  purchaseAmount: 9750 + index,
  billingAmount: 21300 + index,
  initialServiceDate: '2025-01-22',
  purchaseDate: '2025-02-05',
  medicalFacility: `Case Facility ${index + 1}`,
  medicalFacilityId: `facility-${index + 1}`,
  lawFirm: 'Aaron Law Group',
  lawFirmId: 'firm-1',
  caseManager: 'Aaron Law Group',
  caseManagerId: 'manager-1',
  caseId: 'case-1',
  closedDate: '',
}));

Alert.alert = mockAlert;

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
  notes:
    'documentType=Case Intake; currentMedicalStatus=Pre-demand; lead=Jordan Lee; shareCase=true; isUccFiled=true',
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
  useCaseDocuments: () => ({
    data: {
      data: [
        {
          id: 'document-1',
          tenantId: 'tenant-1',
          productId: 'SYNQLIEN',
          referenceId: 'case-1',
          referenceType: 'Case',
          documentTypeId: 'document-type-1',
          title: 'Case_Brief_Martinez.pdf',
          status: 'ACTIVE',
          mimeType: 'application/pdf',
          fileSizeBytes: 1024,
          versionCount: 1,
          scanStatus: 'CLEAN',
          scanThreats: [],
          isDeleted: false,
          createdAt: '2026-05-03T00:00:00Z',
          createdBy: 'user-1',
          updatedAt: '2026-05-03T00:00:00Z',
          updatedBy: 'user-1',
        },
      ],
      total: 1,
      limit: 200,
      offset: 0,
    },
    isError: false,
    isLoading: false,
    refetch: mockRefetchDocuments,
  }),
  useCaseDocumentTypes: () => ({
    data: [
      {
        id: 'document-type-1',
        code: 'LegalBrief',
        name: 'Legal Brief',
        isActive: true,
      },
    ],
    isError: false,
    isLoading: false,
  }),
  useCaseLienUpdates: () => ({ data: [], isLoading: false }),
  useCases: () => ({
    cases: [
      {
        caseNumber: '24-20000',
        clientName: 'Jordan Kim',
        id: 'case-2',
      },
    ],
    isLoading: false,
  }),
  useCaseNotes: () => ({ data: [], isLoading: false }),
  useCaseUpdates: () => ({ data: [], isLoading: false }),
  useDeleteCase: () => ({ isPending: false, mutateAsync: mockDeleteCase }),
  useDeleteCaseNote: () => ({ isPending: false, mutateAsync: jest.fn() }),
  useMergeCase: () => ({ isPending: false, mutateAsync: mockMergeCase }),
  useUploadCaseDocument: () => ({
    isPending: false,
    mutateAsync: mockUploadCaseDocument,
  }),
}));

jest.mock('@/features/liens/hooks', () => ({
  useCaseManagementLiens: () => ({
    liens: mockCaseLiens,
    totalCount: mockCaseLiens.length,
    filterOptions: {
      lawFirmId: [],
      medicalFacilityId: [],
      caseManagerId: [],
      statusId: [],
    },
    isLoading: false,
    isError: false,
    isRefetching: false,
    refetchAll: jest.fn(),
  }),
}));

jest.mock('@/shared/hooks', () => ({
  useAuth: () => ({ user: { id: 'user-1' } }),
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
    expect(getByText('Document Type')).toBeTruthy();
    expect(getByText('Case Intake')).toBeTruthy();
    expect(getByText('Current Medical Status')).toBeTruthy();
    expect(getByText('Jordan Lee')).toBeTruthy();
    expect(getByText('Share this Case with Associated Law Firm')).toBeTruthy();
    expect(getByText('UCC Filed')).toBeTruthy();

    fireEvent.press(getByText('Documents'));
    expect(getByTestId('case-documents-page')).toBeTruthy();
    expect(getByText('Case_Brief_Martinez.pdf')).toBeTruthy();
    expect(getByText('Legal Brief')).toBeTruthy();
    expect(getByText('05/03/2026')).toBeTruthy();
    expect(getByText('Upload More')).toBeTruthy();

    fireEvent.press(getByText('Notes'));
    expect(getByTestId('case-notes-page')).toBeTruthy();
    expect(getByText('Case Tracking')).toBeTruthy();
    expect(getByText('Feeds')).toBeTruthy();
    expect(getByText('No Case Tracking Notes')).toBeTruthy();
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

  it('lists only the selected case liens and opens case-scoped lien routes', () => {
    const screen = render(<CaseDetailScreen />);

    fireEvent.press(screen.getByText('Liens'));

    expect(screen.getByTestId('case-liens-page')).toBeTruthy();
    expect(screen.getByText('Case Facility 1')).toBeTruthy();
    expect(screen.queryByText('Case Facility 6')).toBeNull();
    expect(screen.getByText('5 of 6 entries')).toBeTruthy();
    expect(screen.getByText('No Recent Updates')).toBeTruthy();

    fireEvent.press(screen.getAllByText('View Lien')[0]);
    expect(mockNavigate).toHaveBeenCalledWith('ManagementLienDetail', { lienId: 'lien-1' });

    fireEvent.press(screen.getByLabelText('Add case lien'));
    expect(mockNavigate).toHaveBeenCalledWith('CreateLien', { caseId: 'case-1' });

    fireEvent.press(screen.getByLabelText('Next page'));
    expect(screen.getByText('Case Facility 6')).toBeTruthy();
  });

  it('requires confirmation before merging or deleting a case', async () => {
    const { getByLabelText, getByText } = render(<CaseDetailScreen />);

    fireEvent.press(getByLabelText('Manage case'));
    fireEvent.press(getByText('Merge Case'));

    expect(getByText('Select Case to Merge')).toBeTruthy();
    fireEvent.press(getByText('Jordan Kim (24-20000)'));
    expect(mockAlert).toHaveBeenCalledTimes(1);
    expect(mockAlert.mock.calls[0][0]).toBe('Merge Cases');
    expect(mockAlert.mock.calls[0][1]).toContain('Jordan Kim (24-20000)');
    expect(Array.isArray(mockAlert.mock.calls[0][2])).toBe(true);
    expect(mockMergeCase).not.toHaveBeenCalled();

    mockAlert.mock.calls[0][2][1].onPress();
    await waitFor(() => expect(mockMergeCase).toHaveBeenCalledWith('case-2'));

    fireEvent.press(getByLabelText('Manage case'));
    fireEvent.press(getByText('Delete Case'));
    expect(mockAlert).toHaveBeenCalledTimes(2);
    expect(mockAlert.mock.calls[1][0]).toBe('Delete Case');
    expect(mockAlert.mock.calls[1][1]).toContain('cannot be undone');
    expect(Array.isArray(mockAlert.mock.calls[1][2])).toBe(true);
    expect(mockDeleteCase).not.toHaveBeenCalled();

    mockAlert.mock.calls[1][2][1].onPress();
    await waitFor(() => expect(mockDeleteCase).toHaveBeenCalledTimes(1));
  });
});
