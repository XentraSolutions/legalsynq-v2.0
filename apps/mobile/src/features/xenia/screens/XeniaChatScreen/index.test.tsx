import { fireEvent, render } from '@testing-library/react-native';

import { WelcomeState, XeniaChatScreen, XeniaResponseMarkdown } from './index';

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

  it('renders Xenia responses as Markdown', () => {
    const { getByText, queryByText } = render(
      <XeniaResponseMarkdown content={'## Summary\n\n- **First** item\n- Second item'} />
    );

    expect(getByText('Summary')).toBeTruthy();
    expect(getByText('First')).toBeTruthy();
    expect(getByText('Second item')).toBeTruthy();
    expect(queryByText('## Summary')).toBeNull();
  });

  it('renders Markdown tables inside a horizontal scroller', () => {
    const { getByTestId, getByText } = render(
      <XeniaResponseMarkdown
        content={'| Case | Status | Balance |\n| --- | --- | --- |\n| C-100 | Active | $1,250 |'}
      />
    );

    expect(getByTestId('xenia-markdown-table-scroll').props.horizontal).toBe(true);
    expect(getByText('C-100')).toBeTruthy();
    expect(getByText('$1,250')).toBeTruthy();
  });
});
