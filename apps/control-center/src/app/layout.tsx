import type { Metadata } from 'next';
import './globals.css';
import { AnalyticsProvider } from '@/components/analytics/analytics-provider';

export const metadata: Metadata = {
  title: 'LegalSynq Control Center',
  description: 'Platform administration for LegalSynq',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body className="antialiased bg-gray-50">
        {/*
          AnalyticsProvider is a Client Component that fires a page.view
          event on every route change via usePathname(). No visible output.
          TODO: call identifyUser() here once session is available client-side.
        */}
        <AnalyticsProvider>
          {children}
        </AnalyticsProvider>
      </body>
    </html>
  );
}
