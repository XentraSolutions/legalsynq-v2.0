import Link from 'next/link';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell } from '@/components/shell/cc-shell';

export const dynamic = 'force-dynamic';
export default async function TenantApplicationsPage({ searchParams }: { searchParams: Promise<Record<string,string|undefined>> }) {
  const session = await requirePlatformAdmin(); const p = await searchParams;
  const page = Math.max(1, Number(p.page) || 1); const registrationStatus = p.registrationStatus ?? 'PendingReview';
  let result; let error: string | undefined;
  try { result = await controlCenterServerApi.tenantRegistrations.list({ page, pageSize: 20, search: p.search, registrationStatus, provisioningStatus: p.provisioningStatus }); }
  catch (e) { error = e instanceof Error ? e.message : 'Failed to load tenant applications.'; }
  return <CCShell userEmail={session.email}><div className="space-y-5">
    <div><h1 className="text-xl font-semibold text-gray-900">Tenant Applications</h1><p className="mt-1 text-sm text-gray-500">Review self-registration requests and track provisioning separately.</p></div>
    <form className="flex flex-wrap gap-2 rounded-lg border border-gray-200 bg-white p-3"><input name="search" defaultValue={p.search} placeholder="Search tenant or administrator" className="min-w-64 flex-1 rounded-md border border-gray-200 px-3 py-2 text-sm" />
      <select name="registrationStatus" defaultValue={registrationStatus} className="rounded-md border border-gray-200 px-3 py-2 text-sm"><option value="">All decisions</option><option>PendingReview</option><option>Approved</option><option>Declined</option></select>
      <select name="provisioningStatus" defaultValue={p.provisioningStatus} className="rounded-md border border-gray-200 px-3 py-2 text-sm"><option value="">All provisioning</option><option>NotStarted</option><option>InProgress</option><option>Provisioned</option><option>Failed</option></select>
      <button className="rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white">Filter</button></form>
    {error && <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">{error}</div>}
    {result && <div className="overflow-hidden rounded-lg border border-gray-200 bg-white"><div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead className="bg-gray-50 text-xs uppercase text-gray-500"><tr>{['Tenant','Organization','Administrator','Submitted','Decision','Provisioning','Actions'].map(h=><th key={h} className="px-4 py-3">{h}</th>)}</tr></thead><tbody className="divide-y divide-gray-100">{result.items.map(a=><tr key={a.id} className="hover:bg-gray-50"><td className="px-4 py-3"><div className="font-medium text-gray-900">{a.tenantName}</div><div className="text-xs text-gray-500">{a.tenantCode}</div></td><td className="px-4 py-3">{a.organizationType}</td><td className="px-4 py-3"><div>{a.adminFirstName} {a.adminLastName}</div><div className="text-xs text-gray-500">{a.adminEmail}</div></td><td className="px-4 py-3 text-gray-500">{new Date(a.createdAtUtc).toLocaleDateString()}</td><td className="px-4 py-3"><Badge value={a.registrationStatus}/></td><td className="px-4 py-3"><Badge value={a.provisioningStatus}/></td><td className="px-4 py-3"><Link className="font-medium text-[#d95f24] hover:underline" href={`/tenant-applications/${a.id}`}>Review</Link></td></tr>)}</tbody></table></div>
      {result.items.length===0&&<div className="p-10 text-center text-sm text-gray-500">No applications match these filters.</div>}</div>}
  </div></CCShell>;
}
function Badge({value}:{value:string}) { const tone=value==='Approved'||value==='Provisioned'?'bg-emerald-100 text-emerald-700':value==='Declined'||value==='Failed'?'bg-red-100 text-red-700':value==='InProgress'?'bg-blue-100 text-blue-700':'bg-amber-100 text-amber-700'; return <span className={`rounded-full px-2 py-1 text-xs font-medium ${tone}`}>{value.replace(/([a-z])([A-Z])/g,'$1 $2')}</span>; }
