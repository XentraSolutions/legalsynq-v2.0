import type { Metadata } from 'next';
import './globals.css';
import { TenantBrandingProvider } from '@/providers/tenant-branding-provider';
import { SessionProvider } from '@/providers/session-provider';

export const metadata: Metadata = {
  title: 'LegalSynq',
  description: 'LegalSynq Platform',
};

/**
 * Root layout — wraps the entire app in:
 *   1. TenantBrandingProvider (anonymous, loaded before auth)
 *   2. SessionProvider       (fetches /auth/me on mount)
 *
 * Provider order matters: branding must load first so the login page
 * shows the correct tenant logo before the user is authenticated.
 */
export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body className="antialiased">
        <TenantBrandingProvider>
          <SessionProvider>
            {children}
          </SessionProvider>
        </TenantBrandingProvider>
      </body>
    </html>
  );
}
