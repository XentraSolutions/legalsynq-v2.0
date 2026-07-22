import { fireEvent, render } from '@testing-library/react-native';

import { CaseDetailHeader } from './index';

describe('CaseDetailHeader', () => {
  it('renders case identity and invokes both actions', () => {
    const onBack = jest.fn();
    const onMore = jest.fn();
    const { getByLabelText, getByText } = render(
      <CaseDetailHeader
        subtitle="Case ID: 24-18743"
        title="Marcus Delgado"
        onBack={onBack}
        onMore={onMore}
      />
    );

    expect(getByText('Marcus Delgado')).toBeTruthy();
    expect(getByText('Case ID: 24-18743')).toBeTruthy();
    fireEvent.press(getByLabelText('Go back'));
    fireEvent.press(getByLabelText('Add case note'));
    expect(onBack).toHaveBeenCalledTimes(1);
    expect(onMore).toHaveBeenCalledTimes(1);
  });
});
