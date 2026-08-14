import { fireEvent, render, waitFor } from '@testing-library/react-native';

import { CreateLienScreen, EditLienScreen } from './index';

const mockGoBack = jest.fn();
const mockNavigate = jest.fn();
const mockCreate = jest.fn().mockResolvedValue({ id: 'lien-1', lienNumber: 'LN-001' });
const mockUpdate = jest.fn().mockResolvedValue(undefined);
let mockRouteParams: Record<string, string> = { caseId: 'case-1' };

const mockFormValues = {
  lienNumber: 'LN-001',
  caseId: 'case-1',
  status: 'Open',
  purchaseDate: '2026-01-15',
  initialServiceDate: '2026-01-01',
  endServiceDate: '',
  notes: '',
  isBulk: false,
  isServicing: false,
  fundingCompanyId: '',
  facilityId: 'facility-1',
  facilityContactId: '',
  facilityEmail: '',
  facilityPhone: '',
  medicalProviderId: '',
  originalAmount: '',
  jurisdiction: '',
  subjectFirstName: '',
  subjectLastName: '',
  medicalCodes: [],
  deletedMedicalCodeIds: [],
  payee: '',
  outboundCheckNumber: '',
};

jest.mock('@react-navigation/native', () => ({
  useNavigation: () => ({ goBack: mockGoBack, navigate: mockNavigate }),
  useRoute: () => ({ params: mockRouteParams }),
}));

jest.mock('@tanstack/react-query', () => ({
  QueryClient: class QueryClient {},
  QueryClientProvider: ({ children }: { children: React.ReactNode }) => children,
  useQuery: () => ({ data: [], isLoading: false }),
}));

jest.mock('@/features/cases/hooks', () => ({
  useCases: () => ({ cases: [] }),
}));

jest.mock('@/features/liens/hooks', () => ({
  managementLienKeys: { all: ['management-liens'], facilities: () => ['facilities'] },
  useManagementLienDetail: (lienId: string) => ({
    data: lienId ? { formValues: mockFormValues } : undefined,
    isLoading: false,
  }),
  useCreateManagementLien: () => ({ isPending: false, mutateAsync: mockCreate }),
  useUpdateManagementLien: () => ({ isPending: false, mutateAsync: mockUpdate }),
}));

jest.mock('@/shared/hooks', () => ({
  useToast: () => ({ showError: jest.fn(), showSuccess: jest.fn() }),
}));

describe('CreateLienScreen', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockRouteParams = { caseId: 'case-1' };
  });

  it('requires confirmation before triggering the create mutation', async () => {
    const screen = render(<CreateLienScreen />);
    fireEvent.changeText(screen.getAllByPlaceholderText('YYYY-MM-DD')[0], '2026-01-15');
    fireEvent.changeText(screen.getAllByPlaceholderText('YYYY-MM-DD')[1], '2026-01-01');

    fireEvent.press(screen.getByText('Save'));
    expect(mockCreate).not.toHaveBeenCalled();

    fireEvent.press(screen.getByText('Yes, Create Lien'));
    await waitFor(() => expect(mockCreate).toHaveBeenCalledTimes(1));
  });
});

describe('EditLienScreen', () => {
  beforeEach(() => jest.clearAllMocks());

  const sectionCases = [
    {
      section: 'company',
      visibleTitle: 'Medical Lien & Funding Company Information',
      hiddenTitles: ['Medical Facility and Provider Information', 'Medical Code Information'],
    },
    {
      section: 'provider',
      visibleTitle: 'Medical Facility and Provider Information',
      hiddenTitles: ['Medical Lien & Funding Company Information', 'Medical Code Information'],
    },
    {
      section: 'medicalCodes',
      visibleTitle: 'Medical Code Information',
      hiddenTitles: ['Medical Lien & Funding Company Information', 'Medical Facility and Provider Information'],
    },
  ];

  sectionCases.forEach(({ section, visibleTitle, hiddenTitles }) => {
    it(`renders only the ${section} form for its card`, () => {
      mockRouteParams = { lienId: 'lien-1', section };
      const screen = render(<EditLienScreen />);

      expect(screen.getByText(visibleTitle)).toBeTruthy();
      hiddenTitles.forEach((title: string) => expect(screen.queryByText(title)).toBeNull());
    });
  });

  it('requires confirmation before triggering the section update', async () => {
    mockRouteParams = { lienId: 'lien-1', section: 'medicalCodes' };
    const screen = render(<EditLienScreen />);

    fireEvent.press(screen.getByText('Save'));
    expect(mockUpdate).not.toHaveBeenCalled();

    fireEvent.press(screen.getByText('Yes, Save Changes'));
    await waitFor(() => expect(mockUpdate).toHaveBeenCalledTimes(1));
  });
});
