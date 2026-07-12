import { redirect } from 'next/navigation';

export const dynamic = 'force-dynamic';

export default function AiDashboardCompatibilityRedirectPage() {
  redirect('/xenia/dashboard');
}
