import type { ReactNode } from 'react';
import Link from 'next/link';
import type { GroupDetail } from '@/types/control-center';
import { Routes } from '@/lib/routes';

interface GroupDetailCardProps {
  group: GroupDetail;
}

function formatDate(iso: string): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'long',
    day:   'numeric',
    year:  'numeric',
  });
}

function formatDateTime(iso: string): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleString('en-US', {
    month:  'short',
    day:    'numeric',
    year:   'numeric',
    hour:   '2-digit',
    minute: '2-digit',
  });
}

export function GroupDetailCard({ group }: GroupDetailCardProps) {
  return (
    <div className="space-y-5">

      {/* ── Group Information ───────────────────────────────────────────── */}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Group Information
          </h2>
        </div>
        <dl className="divide-y divide-gray-100">
          <InfoRow label="Name"        value={group.name} />
          <InfoRow label="Description" value={
            group.description
              ? group.description
              : <span className="text-gray-400 italic">—</span>
          } />
          <InfoRow label="Tenant ID"   value={
            <span className="font-mono text-xs bg-gray-100 px-1.5 py-0.5 rounded text-gray-600">
              {group.tenantId}
            </span>
          } />
          <InfoRow label="Status" value={
            group.isActive
              ? <ActivePill active />
              : <ActivePill active={false} />
          } />
          <InfoRow label="Members"     value={String(group.memberCount)} />
          <InfoRow label="Created"     value={formatDate(group.createdAtUtc)} />
          <InfoRow label="Updated"     value={formatDate(group.updatedAtUtc)} />
        </dl>
      </div>

      {/* ── Members ─────────────────────────────────────────────────────── */}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Members
          </h2>
          <span className="text-xs text-gray-400">{group.members.length} member{group.members.length !== 1 ? 's' : ''}</span>
        </div>

        {group.members.length === 0 ? (
          <div className="px-5 py-10 text-center">
            <p className="text-sm text-gray-400 italic">No members in this group.</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-100">
              <thead>
                <tr className="bg-gray-50">
                  <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Name</th>
                  <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Email</th>
                  <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Joined</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {group.members.map(m => (
                  <tr key={m.membershipId} className="hover:bg-gray-50 transition-colors">
                    <td className="px-4 py-2">
                      <Link
                        href={Routes.userDetail(m.userId)}
                        className="text-sm font-medium text-gray-900 hover:text-indigo-700 hover:underline transition-colors"
                      >
                        {m.firstName} {m.lastName}
                      </Link>
                    </td>
                    <td className="px-4 py-2 text-sm text-gray-500">{m.email}</td>
                    <td className="px-4 py-2 text-xs text-gray-400 whitespace-nowrap">
                      {formatDateTime(m.joinedAtUtc)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}

function InfoRow({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="px-5 py-3 flex items-center gap-4">
      <dt className="w-36 shrink-0 text-xs font-medium text-gray-500">{label}</dt>
      <dd className="text-sm text-gray-800">{value}</dd>
    </div>
  );
}

function ActivePill({ active }: { active: boolean }) {
  return active ? (
    <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-green-50 text-green-700 border-green-200">
      Active
    </span>
  ) : (
    <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-gray-100 text-gray-500 border-gray-200">
      Inactive
    </span>
  );
}
