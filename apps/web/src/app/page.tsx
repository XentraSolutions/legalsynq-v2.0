import { redirect } from 'next/navigation';

/**
 * Root route — always redirect to /dashboard.
 * The dashboard page (and platform layout) will handle the auth check.
 */
export default function RootPage() {
  redirect('/dashboard');
}
