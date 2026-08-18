import { fireEvent, render } from '@testing-library/react-native';

import { ApiError } from '@/shared/types/api';

import { ApplicationDetailScreen } from './index';

const mockGoBack = jest.fn();
const mockRefetch = jest.fn();
const mockUseApplicationDetail = jest.fn();

jest.mock('@react-navigation/native', () => ({
  useNavigation: () => ({ goBack: mockGoBack }),
  useRoute: () => ({ params: { applicationId: '01989abc-1234-7000-8000-123456789abc' } }),
}));

jest.mock('@/features/applications/hooks', () => ({
  useApplicationDetail: (id: string) => mockUseApplicationDetail(id),
}));

const application = {
  id: '01989abc-1234-7000-8000-123456789abc',
  tenantId: '11111111-1111-1111-1111-111111111111',
  applicationNumber: 'APP-2026-001',
  applicantFirstName: 'Avery',
  applicantLastName: 'Morgan',
  email: 'avery@example.test',
  phone: '555-0100',
  requestedAmount: 25000,
  approvedAmount: 20000,
  caseType: 'Personal Injury',
  incidentDate: '2026-06-15',
  attorneyNotes: null,
  approvalTerms: null,
  denialReason: null,
  funderId: null,
  status: 'Approved',
  createdByUserId: null,
  updatedByUserId: null,
  createdAtUtc: '2026-06-16T10:00:00Z',
  updatedAtUtc: '2026-06-18T10:00:00Z',
};

describe('ApplicationDetailScreen', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockUseApplicationDetail.mockReturnValue({
      data: application,
      error: null,
      isError: false,
      isLoading: false,
      refetch: mockRefetch,
    });
  });

  it('loads the route applicationId and renders repository-backed fields', () => {
    const screen = render(<ApplicationDetailScreen />);

    expect(mockUseApplicationDetail).toHaveBeenCalledWith(application.id);
    expect(screen.getAllByText('APP-2026-001').length).toBeGreaterThan(0);
    expect(screen.getByText('Avery Morgan')).toBeTruthy();
    expect(screen.getByText('$25,000')).toBeTruthy();
    expect(screen.getByText('$20,000')).toBeTruthy();
  });

  it('renders the standard loading state', () => {
    mockUseApplicationDetail.mockReturnValue({
      data: undefined,
      error: null,
      isError: false,
      isLoading: true,
      refetch: mockRefetch,
    });

    const screen = render(<ApplicationDetailScreen />);
    expect(screen.getByLabelText('Loading application')).toBeTruthy();
  });

  it('renders an unavailable state and retries a service error', () => {
    mockUseApplicationDetail.mockReturnValue({
      data: undefined,
      error: new Error('Network unavailable'),
      isError: true,
      isLoading: false,
      refetch: mockRefetch,
    });

    const screen = render(<ApplicationDetailScreen />);
    expect(screen.getByText('Application unavailable')).toBeTruthy();
    fireEvent.press(screen.getByText('Try Again'));
    expect(mockRefetch).toHaveBeenCalledTimes(1);
  });

  it('renders not found as unavailable and navigates back', () => {
    mockUseApplicationDetail.mockReturnValue({
      data: undefined,
      error: new ApiError({ code: 'NOT_FOUND', message: 'Not found', statusCode: 404 }),
      isError: true,
      isLoading: false,
      refetch: mockRefetch,
    });

    const screen = render(<ApplicationDetailScreen />);
    fireEvent.press(screen.getByText('Go Back'));
    expect(mockGoBack).toHaveBeenCalledTimes(1);
    expect(mockRefetch).not.toHaveBeenCalled();
  });

  it('uses standard back navigation', () => {
    const screen = render(<ApplicationDetailScreen />);
    fireEvent.press(screen.getByRole('button'));
    expect(mockGoBack).toHaveBeenCalledTimes(1);
  });
});
