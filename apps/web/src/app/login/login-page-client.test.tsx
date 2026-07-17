import { fireEvent, render, screen } from '@testing-library/react';
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

vi.mock('next/navigation', () => ({
  useSearchParams: () => ({
    get: (key: string) => (key === 'reason' ? 'unauthenticated' : null),
  }),
}));

describe('LoginPageClient', () => {
  test('renders the SynqLien portal login shell without calling auth APIs', () => {
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);

    render(<LoginPageClient portalProductId="synqlien" />);

    expect(screen.getByText('Sign in to your SynqLien portal')).toBeInTheDocument();
    expect(screen.getByText(/Your lien operations/)).toBeInTheDocument();
    expect(screen.getByText('Your session has ended. Please sign in to continue.')).toBeInTheDocument();
    expect(screen.queryByText('Sign in to your CareConnect portal')).not.toBeInTheDocument();

    fireEvent.change(screen.getByPlaceholderText('you@example.com'), {
      target: { value: 'seller@example.com' },
    });
    fireEvent.change(screen.getByPlaceholderText('••••••••'), {
      target: { value: 'Password123!' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(fetchMock).not.toHaveBeenCalled();
    expect(screen.getByText('SynqLien portal sign-in is not connected yet.')).toBeInTheDocument();
  });
});
