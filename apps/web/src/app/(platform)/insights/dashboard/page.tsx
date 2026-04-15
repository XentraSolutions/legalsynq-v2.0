import { redirect } from 'next/navigation';
import { requireOrg } from '@/lib/auth-guards';

export default async function Page() {
  await requireOrg();
  redirect('/insights/reports');
}
