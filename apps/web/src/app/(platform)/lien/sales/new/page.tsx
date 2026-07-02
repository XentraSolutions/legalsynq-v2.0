import { redirect } from 'next/navigation';
import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { NewLienSaleClient } from './new-sale-client';

export const dynamic = 'force-dynamic';

export default async function NewLienSalePage() {
  const session = await requireOrg();
  if (!session.productRoles.includes(ProductRole.SynqLienSeller)) redirect('/dashboard');
  return <NewLienSaleClient />;
}
