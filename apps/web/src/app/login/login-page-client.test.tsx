import { render, screen } from '@testing-library/react';
import type React from 'react';
import { describe, expect, test, vi } from 'vitest';
import { LoginPageClient } from './login-page-client';

vi.mock('next/image', () => ({
  default: ({
    priority,
    unoptimized,
    ...props
  }: React.ImgHTMLAttributes<HTMLImageElement> & {
    priority?: boolean;
    unoptimized?: boolean;
  }) => <img {...props} />,
}));

vi.mock('next/link', () => ({
  default: ({
    href,
    children,
    ...props
  }: React.AnchorHTMLAttributes<HTMLAnchorElement> & {
    href: string;
  }) => <a href={href} {...props}>{children}</a>,
}));

vi.mock('./login-form', () => ({
  LoginForm: ({ defaultReturnTo }: { defaultReturnTo?: string }) => (
    <div data-testid="login-form" data-default-return-to={defaultReturnTo ?? ''} />
  ),
}));

describe('LoginPageClient', () => {
  test('renders the SynqLien portal login shell with the funding default return target', () => {
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);

    render(
      <LoginPageClient
        portalProductId="synqlien"
        portalLandingPath="/funding/dashboard"
      />,
    );

    expect(screen.getByText('Sign in to your SynqLien portal')).toBeInTheDocument();
    expect(screen.getByText(/Your lien operations/)).toBeInTheDocument();
    expect(screen.queryByText('Sign in to your CareConnect portal')).not.toBeInTheDocument();

    expect(fetchMock).not.toHaveBeenCalled();
    expect(screen.getByTestId('login-form')).toHaveAttribute(
      'data-default-return-to',
      '/funding/dashboard',
    );
  });
});
