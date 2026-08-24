import { fireEvent, render } from '@testing-library/react-native';

import { ServicingCaseCard } from './index';

describe('ServicingCaseCard', () => {
  it('renders the servicing values and opens the selected case', () => {
    const onPress = jest.fn();
    const screen = render(
      <ServicingCaseCard
        caseItem={{
          billingAmount: 38750,
          caseId: 'case-1',
          caseNumber: '24-18743',
          clientName: 'Marcus Delgado',
          lawFirm: 'Morrison & Patel LLP',
          purchaseAmount: 42500,
          status: 'PreDemand',
        }}
        onPress={onPress}
      />
    );

    expect(screen.getByText('Marcus Delgado')).toBeTruthy();
    expect(screen.getByText('Case ID: 24-18743')).toBeTruthy();
    expect(screen.getByText('Pre-demand')).toBeTruthy();
    expect(screen.getByText('Morrison & Patel LLP')).toBeTruthy();
    expect(screen.getByText('$42,500')).toBeTruthy();
    expect(screen.getByText('$38,750')).toBeTruthy();

    fireEvent.press(screen.getByLabelText('View servicing for 24-18743'));
    expect(onPress).toHaveBeenCalledTimes(1);
  });
});
