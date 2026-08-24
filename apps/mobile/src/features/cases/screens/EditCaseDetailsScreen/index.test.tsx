import { fireEvent, render, waitFor } from '@testing-library/react-native';

import { EditCaseDetailsScreen } from './index';

const mockMutateAsync = jest.fn();
const editCaseDetail = {
  accidentType: 'Motor Vehicle',
  dateOfIncident: '2025-03-14',
  description: 'Follow up next week',
  notes: 'currentMedicalStatus=Pre-demand; leadId=lead-1; lead=Jordan Lee',
  stateOfIncident: 'AZ',
  status: 'PreDemand',
};

jest.mock('@react-navigation/native', () => ({
  useNavigation: () => ({ goBack: jest.fn() }),
  useRoute: () => ({ params: { caseId: 'case-1' } }),
}));

jest.mock('@/features/cases/hooks', () => ({
  useCaseDetail: () => ({
    data: editCaseDetail,
    isError: false,
    isLoading: false,
  }),
  useCaseTrackingOptions: () => ({
    data: {
      caseTypes: [{ code: 'MVA', id: 'type-1', name: 'Motor Vehicle' }],
      leads: [{ displayName: 'Jordan Lee', id: 'lead-1' }],
      medicalStatuses: [{ code: 'PRE', id: 'medical-1', name: 'Pre-demand' }],
      states: [
        { code: 'AZ', id: 'state-1', name: 'Arizona' },
        ...Array.from({ length: 20 }, (_, index) => ({
          code: `S${index + 1}`,
          id: `state-${index + 2}`,
          name: `State ${index + 1}`,
        })),
      ],
    },
  }),
  useUpdateCaseDetails: () => ({ isPending: false, mutateAsync: mockMutateAsync }),
}));

jest.mock('@/shared/hooks', () => ({
  useToast: () => ({ showError: jest.fn(), showSuccess: jest.fn() }),
}));

describe('EditCaseDetailsScreen', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockMutateAsync.mockResolvedValue(undefined);
  });

  it('renders the Figma-aligned case detail fields', () => {
    const { getAllByText, getByDisplayValue, getByLabelText, getByText } = render(
      <EditCaseDetailsScreen />
    );

    expect(getByText('Edit Case Details')).toBeTruthy();
    expect(getAllByText('Pre-demand')).toHaveLength(2);
    expect(getByText('Motor Vehicle')).toBeTruthy();
    expect(getByText('AZ')).toBeTruthy();
    expect(getByText('Jordan Lee')).toBeTruthy();
    expect(getByText('Tracking Follow Up')).toBeTruthy();
    expect(getByText('03 / 14 / 2025')).toBeTruthy();
    expect(getByLabelText('Select date of loss')).toBeTruthy();
    expect(getByDisplayValue('Follow up next week')).toBeTruthy();
  });

  it('saves the tracking note through the description field supported by the API', async () => {
    const { getByDisplayValue, getByText } = render(<EditCaseDetailsScreen />);

    fireEvent.changeText(getByDisplayValue('Follow up next week'), 'Call the client Friday');
    fireEvent.press(getByText('Continue'));

    await waitFor(() => {
      expect(mockMutateAsync).toHaveBeenCalledWith({
        primary: {
          dateOfLoss: '2025-03-14',
          status: 'PreDemand',
        },
        details: {
          description: 'Call the client Friday',
          notes:
            'currentMedicalStatus=Pre-demand; leadId=lead-1; lead=Jordan Lee; accidentType=Motor Vehicle; stateOfIncident=AZ',
        },
      });
    });
  });

  it('adds search for long option lists and filters the scrollable choices', () => {
    const { getByLabelText, getByPlaceholderText, getByText, queryByText } = render(
      <EditCaseDetailsScreen />
    );

    fireEvent.press(getByLabelText('Select state of incident'));
    expect(getByText('Arizona (AZ)')).toBeTruthy();

    fireEvent.changeText(getByPlaceholderText('Search state of incident...'), 'State 20');

    expect(getByText('State 20 (S20)')).toBeTruthy();
    expect(queryByText('Arizona (AZ)')).toBeNull();
  });
});
