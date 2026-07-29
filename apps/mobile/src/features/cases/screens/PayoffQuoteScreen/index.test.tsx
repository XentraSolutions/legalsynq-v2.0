import { render } from '@testing-library/react-native';

import { PayoffQuoteScreen } from './index';

jest.mock('@react-navigation/native', () => ({
  useNavigation: () => ({ goBack: jest.fn() }),
  useRoute: () => ({ params: { caseId: 'case-1' } }),
}));

jest.mock('@/features/cases/hooks', () => ({
  useCaseDetail: () => ({ data: { caseNumber: '24-18743' } }),
  usePayoffQuote: () => ({
    data: { url: 'https://example.com/payoff.pdf' },
    isError: false,
    isLoading: false,
  }),
}));

jest.mock('@/shared/hooks', () => ({
  useToast: () => ({ showError: jest.fn() }),
}));

describe('PayoffQuoteScreen', () => {
  it('renders the available payoff document actions', () => {
    const { getAllByText, getByLabelText, getByText } = render(<PayoffQuoteScreen />);

    expect(getAllByText('Payoff Quote')).toHaveLength(2);
    expect(getByText('Open Payoff Quote')).toBeTruthy();
    expect(getByText('Share')).toBeTruthy();
    expect(getByLabelText('Share payoff quote')).toBeTruthy();
  });
});
