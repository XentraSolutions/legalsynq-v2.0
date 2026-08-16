import Link from 'next/link';
import type { ReactNode } from 'react';
import { notFound } from 'next/navigation';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell } from '@/components/shell/cc-shell';
import { DecisionPanel } from './decision-panel';

export const dynamic = 'force-dynamic';
export default async function ApplicationDetail({ params }: { params: Promise<{ id: string }> }) {
  const session = await requirePlatformAdmin(); const { id } = await params;
  let application; try { application = await controlCenterServerApi.tenantRegistrations.get(id); } catch { return notFound(); }
  return <CCShell userEmail={session.email}><div className="mx-auto max-w-[1160px] space-y-6">
    <div className="flex items-center gap-2 text-sm text-[#6b7280]"><Link href="/tenant-applications" className="hover:text-[#2563eb]">Tenant Applications</Link><span>›</span><span className="text-[#111827]">{application.tenantName}</span></div>
    <div className="flex flex-wrap items-start justify-between gap-4"><div><div className="flex items-center gap-3"><h1 className="text-[26px] font-semibold tracking-[-0.02em] text-[#111827]">{application.tenantName}</h1><Status value={application.registrationStatus}/></div><p className="mt-1 text-sm text-[#6b7280]">{application.tenantCode}</p></div><DecisionPanel application={application}/></div>
    <InfoCard title="Tenant Information"><div className="grid gap-5 md:grid-cols-3"><Field label="TENANT NAME" value={application.tenantName}/><Field label="TENANT CODE" value={application.tenantCode}/><Field label="ORGANIZATION TYPE" value={organizationName(application.organizationType)}/></div></InfoCard>
    <InfoCard title="Address"><div className="grid gap-5 md:grid-cols-4"><Field label="STREET NAME" value={application.addressLine1 || application.streetAddress || '—'}/><Field label="CITY" value={application.addressCity || '—'}/><Field label="STATE" value={application.addressState || '—'}/><Field label="ZIP" value={application.addressPostalCode || '—'}/></div></InfoCard>
    <InfoCard title="Admin User Information"><div className="grid gap-5 md:grid-cols-3"><Field label="FIRST NAME" value={application.adminFirstName}/><Field label="LAST NAME" value={application.adminLastName}/><Field label="EMAIL ADDRESS" value={application.adminEmail}/></div></InfoCard>
  </div></CCShell>;
}
function InfoCard({ title, children }: { title: string; children: ReactNode }) { return <section className="rounded-xl border border-[#e5e7eb] bg-white p-6"><h2 className="text-base font-semibold text-[#111827]">{title}</h2><div className="my-5 border-t border-[#e5e7eb]"/>{children}</section>; }
function Field({ label, value }: { label: string; value: string }) { return <div><div className="text-[11px] font-semibold uppercase tracking-wide text-[#6b7280]">{label}</div><div className="mt-2 text-sm text-[#111827]">{value}</div></div>; }
function organizationName(value: string) { return ({ LAW_FIRM: 'Law Firm', LIEN_OWNER: 'Lien Owner', HEALTHCARE_PROVIDER: 'Healthcare Provider', FUNDING_COMPANY: 'Funding Company' } as Record<string, string>)[value] ?? value.replaceAll('_', ' ').toLowerCase().replace(/\b\w/g, c => c.toUpperCase()); }
function Status({ value }: { value: string }) { const accepted = value === 'Approved'; const declined = value === 'Declined'; return <span className={`inline-flex rounded-full px-4 py-2 text-sm font-medium ${accepted ? 'bg-[#f0fdf4] text-[#15803d]' : declined ? 'bg-[#fef2f2] text-[#b91c1c]' : 'bg-[#fffbeb] text-[#a16207]'}`}>{accepted ? 'Accepted' : declined ? 'Declined' : 'Pending'}</span>; }
