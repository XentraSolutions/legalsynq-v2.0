import { requireOrg } from '@/lib/auth-guards';
import { BlankPage } from '@/components/ui/blank-page';

export default async function ProfilePage() {
  await requireOrg();
  return <BlankPage />;
}
