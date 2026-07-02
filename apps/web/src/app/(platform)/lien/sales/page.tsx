import { redirect } from 'next/navigation';
import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { LienSalesClient } from './sales-client';

export const dynamic = 'force-dynamic';

export default async function LienSalesPage() {
  const session = await requireOrg();
  if (!session.productRoles.includes(ProductRole.SynqLienSeller)) redirect('/dashboard');
  return <LienSalesClient />;
}
