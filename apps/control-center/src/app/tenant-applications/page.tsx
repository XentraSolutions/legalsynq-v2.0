import { requirePlatformAdmin } from '@/lib/auth-guards';
import { controlCenterServerApi, type TenantRegistration } from '@/lib/control-center-api';
import { CCShell } from '@/components/shell/cc-shell';
import { TenantApplicationsTable } from './tenant-applications-table';

export const dynamic = 'force-dynamic';

export default async function TenantApplicationsPage({ searchParams }: { searchParams: Promise<Record<string, string | undefined>> }) {
  const session = await requirePlatformAdmin();
  const params = await searchParams;
  const page = Math.max(1, Number(params.page) || 1);
  const status = params.registrationStatus ?? '';
  let result: { items: TenantRegistration[]; totalCount: number; page: number; pageSize: number } | undefined;
  let error: string | undefined;
  try {
    result = await controlCenterServerApi.tenantRegistrations.list({ page, pageSize: 10, search: params.search, registrationStatus: status, provisioningStatus: params.provisioningStatus });
  } catch (e) { error = e instanceof Error ? e.message : 'Failed to load tenant applications.'; }

  return <CCShell userEmail={session.email}><div className="mx-auto max-w-[1320px] space-y-7">
    <div><h1 className="text-[26px] font-semibold tracking-[-0.02em] text-[#111827]">Tenant Applications</h1><p className="mt-1 text-sm text-[#6b7280]">Monitor tenant registration applications and take the appropriate action.</p></div>
    {error && <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">{error}</div>}
    {result && <TenantApplicationsTable items={result.items} totalCount={result.totalCount} page={result.page} pageSize={result.pageSize} search={params.search ?? ''} activeStatus={status} />}
  </div></CCShell>;
}
