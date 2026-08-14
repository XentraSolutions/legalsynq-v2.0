import { render } from '@testing-library/react-native';

import { EditCasePersonalScreen } from './index';

jest.mock('@react-navigation/native', () => ({
  useNavigation: () => ({ goBack: jest.fn() }),
  useRoute: () => ({ params: { caseId: 'case-1' } }),
}));

jest.mock('@/features/cases/hooks', () => ({
  useCaseDetail: () => ({
    data: {
      clientAddress: '123 Main Street',
      clientDob: '1995-01-10',
      clientEmail: 'marcus@example.com',
      clientFirstName: 'Marcus',
      clientLastName: 'Delgado',
      clientPhone: '555-0100',
    },
    isError: false,
    isLoading: false,
  }),
  useUpdatePersonalInfo: () => ({ isPending: false, mutateAsync: jest.fn() }),
}));

jest.mock('@/shared/hooks', () => ({
  useToast: () => ({ showError: jest.fn(), showSuccess: jest.fn() }),
}));

describe('EditCasePersonalScreen', () => {
  it('hydrates the supported personal fields from the case', () => {
    const { getByDisplayValue, getByText } = render(<EditCasePersonalScreen />);

    expect(getByText('Edit Personal Information')).toBeTruthy();
    expect(getByDisplayValue('Marcus')).toBeTruthy();
    expect(getByDisplayValue('Delgado')).toBeTruthy();
    expect(getByDisplayValue('marcus@example.com')).toBeTruthy();
    expect(getByDisplayValue('123 Main Street')).toBeTruthy();
  });
});
