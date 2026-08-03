import { fireEvent, render, waitFor } from '@testing-library/react-native';

import { ManagementLienDetailScreen } from './index';

const mockGoBack = jest.fn();
const mockNavigate = jest.fn();
const mockUploadDocument = jest.fn().mockResolvedValue({ id: 'document-1' });
const mockPickDocument = jest.fn().mockResolvedValue({
  canceled: false,
  assets: [
    {
      uri: 'file:///medical-record.pdf',
      name: 'medical-record.pdf',
      mimeType: 'application/pdf',
    },
  ],
});

jest.mock('expo-document-picker', () => ({
  getDocumentAsync: () => mockPickDocument(),
}));

jest.mock('@react-navigation/native', () => ({
  useNavigation: () => ({ goBack: mockGoBack, navigate: mockNavigate }),
  useRoute: () => ({ params: { lienId: 'lien-1' } }),
}));

jest.mock('@tanstack/react-query', () => ({
  QueryClient: class QueryClient {},
  QueryClientProvider: ({ children }: { children: React.ReactNode }) => children,
  useQuery: ({ queryKey }: { queryKey: unknown[] }) => ({
    data: queryKey.includes('detail-contacts')
      ? { providers: [], fundingCompanies: [] }
      : queryKey.includes('document-types')
        ? [
            {
              id: 'document-type-1',
              code: 'MedicalRecord',
              name: 'Medical Record',
              description: 'Medical records supporting the lien',
              isActive: true,
            },
          ]
        : [],
    isLoading: false,
    isError: false,
  }),
}));

jest.mock('@/features/liens/hooks', () => ({
  managementLienKeys: {
    all: ['management-liens'],
    facilities: () => ['management-liens', 'facilities'],
    documentTypes: () => ['management-liens', 'document-types'],
  },
  useUploadLienDocument: () => ({
    isPending: false,
    mutateAsync: mockUploadDocument,
  }),
  useManagementLienDetail: () => ({
    isLoading: false,
    isError: false,
    data: {
      lien: { id: 'lien-1', purchasePrice: 6500 },
      details: { codeList: [], documentList: [] },
      formValues: {
        status: 'Open',
        purchaseDate: '02/05/2025',
        initialServiceDate: '01/22/2025',
        endServiceDate: '01/22/2026',
        fundingCompanyId: '',
        notes: 'Lien notes',
        facilityId: '',
        facilityContactId: '',
        facilityEmail: '',
        medicalProviderId: '',
      },
    },
  }),
}));

jest.mock('@/shared/hooks', () => ({
  useAuth: () => ({ user: { tenantId: 'tenant-1' } }),
  useToast: () => ({ showError: jest.fn(), showSuccess: jest.fn() }),
}));

describe('ManagementLienDetailScreen', () => {
  beforeEach(() => jest.clearAllMocks());

  it('renders every Figma card expanded by default and collapses each independently', () => {
    const screen = render(<ManagementLienDetailScreen />);

    expect(screen.getByText('Medical Lien & Funding Company Information')).toBeTruthy();
    expect(screen.getByText('Medical Facility and Provider Information')).toBeTruthy();
    expect(screen.getByText('Medical Code Information')).toBeTruthy();
    expect(screen.getByText('Uploaded Documents')).toBeTruthy();
    expect(screen.getByText('Lien Status')).toBeTruthy();
    expect(screen.getByText('Facility Name')).toBeTruthy();
    expect(screen.getByText('No medical codes added.')).toBeTruthy();
    expect(screen.getByText('No documents uploaded.')).toBeTruthy();

    fireEvent.press(
      screen.getAllByLabelText('Collapse Medical Lien & Funding Company Information')[0]
    );
    expect(screen.queryByText('Lien Status')).toBeNull();
    expect(screen.getByText('Facility Name')).toBeTruthy();
  });

  it('places edit actions in editable card headers', () => {
    const screen = render(<ManagementLienDetailScreen />);

    expect(screen.getByLabelText('Edit Medical Lien & Funding Company Information')).toBeTruthy();
    expect(screen.getByLabelText('Edit Medical Facility and Provider Information')).toBeTruthy();
    expect(screen.getByLabelText('Edit Medical Code Information')).toBeTruthy();
    expect(screen.queryByLabelText('Edit Uploaded Documents')).toBeNull();

    fireEvent.press(screen.getByLabelText('Edit Medical Code Information'));
    expect(mockNavigate).toHaveBeenCalledWith('EditLien', {
      lienId: 'lien-1',
      section: 'medicalCodes',
    });
  });

  it('confirms before uploading a selected lien document', async () => {
    const screen = render(<ManagementLienDetailScreen />);

    fireEvent.press(screen.getByText('Upload More'));
    expect(screen.getByText('Select the document type before uploading your file.')).toBeTruthy();
    expect(screen.getByText('Add New Document Type')).toBeTruthy();
    expect(screen.getByText('Cancel')).toBeTruthy();
    fireEvent.press(screen.getByTestId('document-type-document-type-1'));

    await waitFor(() => expect(screen.getByText('Upload Document?')).toBeTruthy());
    expect(mockUploadDocument).not.toHaveBeenCalled();

    fireEvent.press(screen.getByText('Yes, Upload Document'));
    await waitFor(() => expect(mockUploadDocument).toHaveBeenCalledTimes(1));
    const upload = mockUploadDocument.mock.calls[0]?.[0] as {
      tenantId: string;
      documentType: { id: string };
      file: { name: string };
    };
    expect(upload.tenantId).toBe('tenant-1');
    expect(upload.documentType.id).toBe('document-type-1');
    expect(upload.file.name).toBe('medical-record.pdf');
  });

  it('keeps document type selection open when file selection is cancelled', async () => {
    mockPickDocument.mockResolvedValueOnce({ canceled: true, assets: [] });
    const screen = render(<ManagementLienDetailScreen />);

    fireEvent.press(screen.getByText('Upload More'));
    fireEvent.press(screen.getByTestId('document-type-document-type-1'));

    await waitFor(() => expect(mockPickDocument).toHaveBeenCalledTimes(1));
    expect(screen.getByText('Select the document type before uploading your file.')).toBeTruthy();
    expect(screen.getByTestId('document-type-document-type-1')).toBeTruthy();
    expect(screen.queryByText('Upload Document?')).toBeNull();
  });
});
