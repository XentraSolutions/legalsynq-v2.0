import { requireOrg } from '@/lib/auth-guards';
import { ReportsCatalogClient } from './reports-catalog-client';

export default async function ReportsCatalogPage() {
  await requireOrg();
  return <ReportsCatalogClient />;
}
