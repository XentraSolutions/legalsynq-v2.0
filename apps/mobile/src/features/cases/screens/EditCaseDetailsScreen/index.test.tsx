import { render } from '@testing-library/react-native';

import { EditCaseDetailsScreen } from './index';

jest.mock('@react-navigation/native', () => ({
  useNavigation: () => ({ goBack: jest.fn() }),
  useRoute: () => ({ params: { caseId: 'case-1' } }),
}));

jest.mock('@/features/cases/hooks', () => ({
  useCaseDetail: () => ({
    data: {
      dateOfIncident: '2025-03-14',
      notes: 'Follow up next week',
      status: 'PreDemand',
    },
    isError: false,
    isLoading: false,
  }),
  useUpdateCaseDetails: () => ({ isPending: false, mutateAsync: jest.fn() }),
}));

jest.mock('@/shared/hooks', () => ({
  useToast: () => ({ showError: jest.fn(), showSuccess: jest.fn() }),
}));

describe('EditCaseDetailsScreen', () => {
  it('renders only the supported case detail fields', () => {
    const { getByDisplayValue, getByText } = render(<EditCaseDetailsScreen />);

    expect(getByText('Edit Case Details')).toBeTruthy();
    expect(getByText('Pre-demand')).toBeTruthy();
    expect(getByDisplayValue('2025-03-14')).toBeTruthy();
    expect(getByDisplayValue('Follow up next week')).toBeTruthy();
  });
});
