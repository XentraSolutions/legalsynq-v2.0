'use client';

import { act, render, screen } from '@testing-library/react';
import { describe, expect, test, vi } from 'vitest';
import { ForgotPasswordForm } from './forgot-password-form';

// Suppress the rate-limit console.warn from route.ts if the module gets pulled in.
vi.stubEnv('NODE_ENV', 'test');

describe('ForgotPasswordForm — isPortal prop', () => {
  test('hides Tenant Code field when isPortal=true', async () => {
    await act(async () => {
      render(<ForgotPasswordForm isPortal={true} />);
    });
    expect(screen.queryByText('Tenant Code')).toBeNull();
  });

  test('shows Tenant Code field when isPortal=false (JSDOM hostname has no subdomain)', async () => {
    // JSDOM window.location.hostname defaults to 'localhost' — no subdomain,
    // so showTenantField should be true after mount when isPortal=false.
    await act(async () => {
      render(<ForgotPasswordForm isPortal={false} />);
    });
    expect(screen.getByText('Tenant Code')).toBeTruthy();
  });

  test('hides Tenant Code field when isPortal is undefined and hostname has subdomain', async () => {
    // Simulate a subdomain environment to exercise the client-side hostname fallback.
    Object.defineProperty(window, 'location', {
      value: { hostname: 'acme.legalsynq.com' },
      writable: true,
    });

    await act(async () => {
      render(<ForgotPasswordForm />);
    });
    expect(screen.queryByText('Tenant Code')).toBeNull();

    // Restore JSDOM default
    Object.defineProperty(window, 'location', {
      value: { hostname: 'localhost' },
      writable: true,
    });
  });

  test('always shows Email address field regardless of isPortal', async () => {
    await act(async () => {
      render(<ForgotPasswordForm isPortal={true} />);
    });
    expect(screen.getByText('Email address')).toBeTruthy();
    expect(screen.getByPlaceholderText('you@example.com')).toBeTruthy();
  });

  test('shows Send reset link button', async () => {
    await act(async () => {
      render(<ForgotPasswordForm isPortal={true} />);
    });
    expect(screen.getByRole('button', { name: /send reset link/i })).toBeTruthy();
  });
});
