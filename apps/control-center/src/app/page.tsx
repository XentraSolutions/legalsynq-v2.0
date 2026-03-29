import { redirect } from 'next/navigation';

/**
 * Root redirect — send all traffic to /tenants (the default CC landing page).
 */
export default function RootPage() {
  redirect('/tenants');
}
