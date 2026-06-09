export type FailureCategoryKey =
  | 'auth_config_failure'
  | 'invalid_recipient'
  | 'retryable_provider_failure'
  | 'non_retryable_failure'
  | 'provider_unavailable';

export const FAILURE_CATEGORY_LABELS: Record<FailureCategoryKey, { label: string; hint: string }> = {
  auth_config_failure: {
    label: 'Authentication configuration failure',
    hint: 'Check your SendGrid API key and provider credentials.',
  },
  invalid_recipient: {
    label: 'Invalid recipient',
    hint: 'The destination address or phone number was rejected by the provider.',
  },
  retryable_provider_failure: {
    label: 'Transient provider failure',
    hint: 'A temporary error occurred; the notification may be retried automatically.',
  },
  non_retryable_failure: {
    label: 'Non-retryable failure',
    hint: 'The provider rejected the message permanently and it will not be retried.',
  },
  provider_unavailable: {
    label: 'Provider unavailable',
    hint: 'The delivery provider could not be reached at the time of sending.',
  },
};

export function formatFailureCategory(category: string | null | undefined): string {
  if (!category) return '—';
  if (Object.hasOwn(FAILURE_CATEGORY_LABELS, category)) {
    const entry = FAILURE_CATEGORY_LABELS[category as FailureCategoryKey];
    return `${entry.label} — ${entry.hint}`;
  }
  return category;
}
