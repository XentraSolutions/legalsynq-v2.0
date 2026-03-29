import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'LegalSynq Control Center',
  description: 'Platform administration for LegalSynq',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body className="antialiased bg-gray-50">
        {children}
      </body>
    </html>
  );
}
