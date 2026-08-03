import { fireEvent, render } from '@testing-library/react-native';

import { LienConfirmationModal } from './index';

describe('LienConfirmationModal', () => {
  it('does not confirm until the user presses the confirmation action', () => {
    const onConfirm = jest.fn();
    const screen = render(
      <LienConfirmationModal
        confirmLabel="Yes, Export"
        description="Generate the CSV?"
        title="Export All Liens?"
        visible
        onCancel={jest.fn()}
        onConfirm={onConfirm}
      />
    );

    expect(onConfirm).not.toHaveBeenCalled();
    fireEvent.press(screen.getByText('Yes, Export'));
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });
});
