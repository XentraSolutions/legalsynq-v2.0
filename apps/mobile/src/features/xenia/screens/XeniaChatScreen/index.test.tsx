import { fireEvent, render } from '@testing-library/react-native';

import { WelcomeState, XeniaChatScreen } from './index';

describe('XeniaChatScreen', () => {
  it('exports the Xenia chat screen', () => {
    expect(typeof XeniaChatScreen).toBe('function');
  });

  it('keeps suggestion chips in an intrinsic-height horizontal row', () => {
    const onSuggestion = jest.fn();
    const { getByLabelText, getByTestId } = render(<WelcomeState onSuggestion={onSuggestion} />);

    expect(getByTestId('xenia-suggestion-row').props.style).toEqual({ flexGrow: 0, height: 60 });

    fireEvent.press(getByLabelText('Summarize my lien queue'));
    expect(onSuggestion).toHaveBeenCalledWith('Summarize my lien queue');
  });
});
