import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, test, vi } from 'vitest';
import { ResetPasswordForm } from './reset-password-form';

const getSearchParam = vi.fn((key: string) => (key === 'token' ? 'valid-token' : null));

vi.mock('next/navigation', () => ({
  useSearchParams: () => ({ get: getSearchParam }),
}));

describe('ResetPasswordForm', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  test('links back to /login when loginHref is the default', () => {
    render(<ResetPasswordForm loginHref="/login" />);

    expect(screen.getByRole('link', { name: 'Back to sign in' })).toHaveAttribute('href', '/login');
  });

  test('links to the CareConnect login URL when loginHref points to the common portal', () => {
    const careConnectLoginUrl = 'https://careconnect.legalsynq.com/login?returnTo=%2Fcareconnect%2Fdashboard&reason=referral-portal';

    render(<ResetPasswordForm loginHref={careConnectLoginUrl} />);

    expect(screen.getByRole('link', { name: 'Back to sign in' })).toHaveAttribute('href', careConnectLoginUrl);
  });

  test('"Sign in" success-state link uses the CareConnect login URL after a successful reset', async () => {
    const careConnectLoginUrl = 'https://careconnect.legalsynq.com/login?returnTo=%2Fcareconnect%2Fdashboard&reason=referral-portal';
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ message: 'Password updated successfully.' }),
    }));

    render(<ResetPasswordForm loginHref={careConnectLoginUrl} />);

    fireEvent.change(screen.getByPlaceholderText('At least 8 characters'), {
      target: { value: 'NewPassword123!' },
    });
    fireEvent.change(screen.getByPlaceholderText('Re-enter your new password'), {
      target: { value: 'NewPassword123!' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Set new password' }));

    await waitFor(() => {
      expect(screen.getByRole('link', { name: 'Sign in' })).toHaveAttribute('href', careConnectLoginUrl);
    });
  });
});
