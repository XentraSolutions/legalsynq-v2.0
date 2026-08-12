'use client';

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, test, vi } from 'vitest';
import { LoginForm } from './login-form';

const push = vi.fn();
const refresh = vi.fn();
const getSearchParam = vi.fn((key: string) => (key === 'returnTo' ? '/careconnect/dashboard' : null));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push }),
  useSearchParams: () => ({ get: getSearchParam }),
}));

vi.mock('@/hooks/use-session', () => ({
  useSession: () => ({ refresh }),
}));

describe('LoginForm', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({
        message: 'This account is not eligible to access the CareConnect portal.',
      }),
    }));
  });

  test('shows the portal restriction message and does not redirect or refresh', async () => {
    render(<LoginForm />);

    const inputs = screen.getAllByRole('textbox');
    fireEvent.change(inputs[0], {
      target: { value: 'provider@example.com' },
    });
    fireEvent.change(screen.getByPlaceholderText('••••••••'), {
      target: { value: 'Password123!' },
    });

    fireEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(screen.getByText('This account is not eligible to access the CareConnect portal.')).toBeInTheDocument();
    });

    expect(refresh).not.toHaveBeenCalled();
    expect(push).not.toHaveBeenCalled();
  });

  test('uses the provided default return target when no returnTo is present', async () => {
    getSearchParam.mockReturnValue(null);
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({}),
    }));

    render(<LoginForm defaultReturnTo="/funding/dashboard" />);

    const inputs = screen.getAllByRole('textbox');
    fireEvent.change(inputs[0], {
      target: { value: 'buyer@example.com' },
    });
    fireEvent.change(screen.getByPlaceholderText('••••••••'), {
      target: { value: 'Password123!' },
    });

    fireEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(refresh).toHaveBeenCalled();
      expect(push).toHaveBeenCalledWith('/funding/dashboard');
    });
  });

  test('forwards tenantId from the login URL to the BFF', async () => {
    getSearchParam.mockImplementation((key: string) => {
      if (key === 'tenantId') return '019ea7f6-21e9-7421-ab54-7846cdc6bc76';
      if (key === 'returnTo') return '/funding/dashboard';
      return null;
    });
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({}),
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<LoginForm defaultReturnTo="/funding/dashboard" />);

    const inputs = screen.getAllByRole('textbox');
    fireEvent.change(inputs[0], {
      target: { value: 'buyer@example.com' },
    });
    fireEvent.change(screen.getByPlaceholderText('••••••••'), {
      target: { value: 'Password123!' },
    });

    fireEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalled();
    });

    const [, init] = fetchMock.mock.calls[0];
    expect(JSON.parse(String(init?.body))).toMatchObject({
      email: 'buyer@example.com',
      tenantId: '019ea7f6-21e9-7421-ab54-7846cdc6bc76',
    });
  });
});
