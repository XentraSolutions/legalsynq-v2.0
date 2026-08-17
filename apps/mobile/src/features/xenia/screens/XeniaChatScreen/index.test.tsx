import { fireEvent, render } from '@testing-library/react-native';

import {
  formatConversationCreatedAt,
  MessageRow,
  WelcomeState,
  XeniaChatScreen,
  XeniaResponseMarkdown,
} from './index';

describe('XeniaChatScreen', () => {
  it('exports the Xenia chat screen', () => {
    expect(typeof XeniaChatScreen).toBe('function');
  });

  it('formats conversation creation timestamps for the history list', () => {
    expect(formatConversationCreatedAt('2026-07-30T13:17:00')).toMatch(
      /^Jul 30, 2026, \d{1,2}:17 PM$/
    );
    expect(formatConversationCreatedAt('2026-07-30T13:17:00Z')).toBe(
      formatConversationCreatedAt('2026-07-30T13:17:00')
    );
    expect(formatConversationCreatedAt('not-a-date')).toBe('Date unavailable');
  });

  it('keeps suggestion chips in an intrinsic-height horizontal row', () => {
    const onSuggestion = jest.fn();
    const { getByLabelText, getByTestId } = render(<WelcomeState onSuggestion={onSuggestion} />);

    expect(getByTestId('xenia-suggestion-row').props.style).toEqual({ flexGrow: 0, height: 60 });

    fireEvent.press(getByLabelText('Summarize my lien queue'));
    expect(onSuggestion).toHaveBeenCalledWith('Summarize my lien queue');
  });

  const messageExamples = [
    ['user', 'Question'],
    ['assistant', 'Answer'],
  ] as const;

  messageExamples.forEach(([role, content]) => {
    it(`renders a creation timestamp below every ${role} message`, () => {
      const { getByText } = render(
        <MessageRow
          message={{
            id: `${role}-1`,
            conversationId: 'conversation-1',
            role,
            content,
            createdAtUtc: '2026-07-30T13:17:00Z',
            citations: [],
          }}
        />
      );

      expect(getByText(content)).toBeTruthy();
      expect(getByText(formatConversationCreatedAt('2026-07-30T13:17:00Z'))).toBeTruthy();
    });
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
