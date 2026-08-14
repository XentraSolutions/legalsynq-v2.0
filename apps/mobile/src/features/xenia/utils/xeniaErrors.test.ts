import { getXeniaErrorMessage, XENIA_TIMEOUT_MESSAGE } from './xeniaErrors';

describe('getXeniaErrorMessage', () => {
  it('maps Axios timeout errors to a user-friendly message', () => {
    expect(getXeniaErrorMessage(new Error('Timeout of 30000ms exceeded'), 'Fallback')).toBe(
      XENIA_TIMEOUT_MESSAGE
    );
  });

  it('maps generic timed-out errors to a user-friendly message', () => {
    expect(getXeniaErrorMessage(new Error('The request timed out'), 'Fallback')).toBe(
      XENIA_TIMEOUT_MESSAGE
    );
  });

  it('preserves a useful non-timeout error', () => {
    expect(getXeniaErrorMessage(new Error('Xenia is unavailable.'), 'Fallback')).toBe(
      'Xenia is unavailable.'
    );
  });

  it('uses the fallback for non-error values', () => {
    expect(getXeniaErrorMessage(undefined, 'Xenia could not send the message.')).toBe(
      'Xenia could not send the message.'
    );
  });
});
