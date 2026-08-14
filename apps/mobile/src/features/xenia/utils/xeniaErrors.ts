const TIMEOUT_PATTERN = /\b(?:timeout|timed out)\b/i;

export const XENIA_TIMEOUT_MESSAGE =
  'This request is taking longer than expected. Please try again.';

export function getXeniaErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof Error)) return fallback;
  return TIMEOUT_PATTERN.test(error.message) ? XENIA_TIMEOUT_MESSAGE : error.message;
}
