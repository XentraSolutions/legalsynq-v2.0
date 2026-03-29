import { requireOrg } from '@/lib/auth-guards';
import { BlankPage } from '@/components/ui/blank-page';

export default async function Page() {
  await requireOrg();
  return <BlankPage />;
}
