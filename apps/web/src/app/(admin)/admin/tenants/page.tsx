import { requireAdmin } from '@/lib/auth-guards';
import { BlankPage } from '@/components/ui/blank-page';

export default async function AdminTenantsPage() {
  await requireAdmin();
  return <BlankPage />;
}
