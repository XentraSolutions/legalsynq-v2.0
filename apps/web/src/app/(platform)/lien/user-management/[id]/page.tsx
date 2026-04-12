'use client';

import { use } from 'react';
import { MOCK_USER_DETAILS, MOCK_USERS, formatDate, formatDateTime } from '@/lib/lien-mock-data';
import { DetailHeader, DetailSection } from '@/components/lien/detail-section';
import { StatusBadge } from '@/components/lien/status-badge';

export default function UserDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const user = MOCK_USER_DETAILS[id] ?? MOCK_USERS.find((u) => u.id === id);
  if (!user) return <div className="p-10 text-center text-gray-400">User not found.</div>;
  const d = { ...MOCK_USERS.find((u) => u.id === id), ...user } as typeof user & { phone?: string; title?: string; permissions?: string[]; activityLog?: any[] };

  return (
    <div className="space-y-5">
      <DetailHeader
        title={d.name}
        subtitle={d.email}
        badge={<StatusBadge status={d.status} size="md" />}
        backHref="/lien/user-management"
        backLabel="Back to Users"
        meta={[
          { label: 'Role', value: d.role },
          { label: 'Department', value: d.department },
          { label: 'Joined', value: formatDate(d.createdAtUtc) },
        ]}
        actions={
          <div className="flex gap-2">
            <button className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">Edit</button>
            {d.status === 'Locked' && <button className="text-sm px-3 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90">Unlock</button>}
            {d.status === 'Active' && <button className="text-sm px-3 py-1.5 border border-red-200 text-red-600 rounded-lg hover:bg-red-50">Deactivate</button>}
          </div>
        }
      />

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <DetailSection title="User Information" icon="ri-user-3-line" fields={[
          { label: 'Name', value: d.name },
          { label: 'Email', value: d.email },
          { label: 'Phone', value: d.phone },
          { label: 'Title', value: d.title },
          { label: 'Department', value: d.department },
          { label: 'Last Login', value: d.lastLoginAtUtc ? formatDateTime(d.lastLoginAtUtc) : 'Never' },
        ]} />
        <DetailSection title="Role & Access" icon="ri-shield-user-line" fields={[
          { label: 'Role', value: d.role },
          { label: 'Status', value: <StatusBadge status={d.status} /> },
        ]} />
      </div>

      {d.permissions && d.permissions.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-gray-800 mb-3">Permissions ({d.permissions.length})</h3>
          <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-2">
            {d.permissions.map((p) => (
              <div key={p} className="flex items-center gap-2 text-xs text-gray-600 bg-gray-50 rounded-lg px-3 py-2">
                <i className="ri-checkbox-circle-line text-green-500" />
                {p}
              </div>
            ))}
          </div>
        </div>
      )}

      {d.activityLog && d.activityLog.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-gray-800 mb-4">Recent Activity</h3>
          <div className="space-y-3">
            {d.activityLog.map((log: any, i: number) => (
              <div key={i} className="flex items-center justify-between py-2 border-b border-gray-50 last:border-0">
                <span className="text-sm text-gray-700">{log.action}</span>
                <span className="text-xs text-gray-400">{formatDateTime(log.timestamp)}</span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
