import { fireEvent, render, waitFor } from '@testing-library/react-native';
import { CaseDocumentsTab } from './index';

const mockUpload = jest.fn(() => Promise.resolve());
const mockShowError = jest.fn();
const mockShowSuccess = jest.fn();
const mockGetDocumentAsync = jest.fn();

jest.mock('expo-document-picker', () => ({
  getDocumentAsync: () => mockGetDocumentAsync(),
}));

jest.mock('@/features/cases/hooks', () => ({
  useCaseDocuments: () => ({
    data: { data: [], total: 0, limit: 200, offset: 0 },
    isError: false,
    isLoading: false,
    refetch: jest.fn(),
  }),
  useCaseDocumentTypes: () => ({
    data: [
      {
        id: 'document-type-1',
        code: 'MedicalRecord',
        name: 'Medical Record',
        isActive: true,
      },
    ],
    isError: false,
    isLoading: false,
  }),
  useUploadCaseDocument: () => ({
    isPending: false,
    mutateAsync: mockUpload,
  }),
}));

jest.mock('@/shared/hooks', () => ({
  useToast: () => ({
    showError: mockShowError,
    showInfo: jest.fn(),
    showSuccess: mockShowSuccess,
  }),
}));

describe('CaseDocumentsTab', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('selects a document type and uploads a supported case document', async () => {
    mockGetDocumentAsync.mockResolvedValue({
      assets: [
        {
          lastModified: 0,
          mimeType: 'application/pdf',
          name: 'medical-record.pdf',
          size: 2048,
          uri: 'file:///medical-record.pdf',
        },
      ],
      canceled: false,
      output: null,
    });

    const screen = render(<CaseDocumentsTab caseId="case-1" />);

    expect(screen.getByText('No Documents Yet')).toBeTruthy();
    fireEvent.press(screen.getByText('Choose File'));
    expect(screen.getByText('Select Document Type')).toBeTruthy();

    fireEvent.press(screen.getByTestId('document-type-document-type-1'));

    await waitFor(() => {
      expect(mockUpload).toHaveBeenCalledTimes(1);
    });
    expect(mockUpload.mock.calls[0]?.[0].documentType.id).toBe('document-type-1');
    expect(mockUpload.mock.calls[0]?.[0].file.name).toBe('medical-record.pdf');
    expect(mockShowSuccess).toHaveBeenCalledWith('Document uploaded successfully');
    expect(mockShowError).not.toHaveBeenCalled();
  });

  it('rejects a file larger than 50 MB before upload', async () => {
    mockGetDocumentAsync.mockResolvedValue({
      assets: [
        {
          lastModified: 0,
          mimeType: 'application/pdf',
          name: 'oversized.pdf',
          size: 50 * 1024 * 1024 + 1,
          uri: 'file:///oversized.pdf',
        },
      ],
      canceled: false,
      output: null,
    });

    const screen = render(<CaseDocumentsTab caseId="case-1" />);
    fireEvent.press(screen.getByText('Choose File'));
    fireEvent.press(screen.getByTestId('document-type-document-type-1'));

    await waitFor(() => {
      expect(mockShowError).toHaveBeenCalledWith('The selected file must be 50 MB or smaller.');
    });
    expect(mockUpload).not.toHaveBeenCalled();
  });
});
