import { redirect } from 'next/navigation';
import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { LienSaleDetailClient } from './sale-detail-client';

export const dynamic = 'force-dynamic';

export default async function LienSaleDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const session = await requireOrg();
  if (!session.productRoles.includes(ProductRole.SynqLienSeller)) redirect('/dashboard');
  return <LienSaleDetailClient id={(await params).id} />;
}
