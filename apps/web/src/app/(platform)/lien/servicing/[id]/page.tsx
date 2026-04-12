'use client';

import { use } from 'react';
import Link from 'next/link';
import { MOCK_SERVICING_DETAILS, MOCK_SERVICING, formatDate } from '@/lib/lien-mock-data';
import { DetailHeader, DetailSection } from '@/components/lien/detail-section';
import { StatusBadge, PriorityBadge } from '@/components/lien/status-badge';
import { ActivityTimeline } from '@/components/lien/activity-timeline';

export default function ServicingDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const item = MOCK_SERVICING_DETAILS[id] ?? MOCK_SERVICING.find((s) => s.id === id);
  if (!item) return <div className="p-10 text-center text-gray-400">Servicing task not found.</div>;
  const d = { ...MOCK_SERVICING.find((s) => s.id === id), ...item } as typeof item & { notes?: string; resolution?: string; linkedCaseId?: string; linkedLienId?: string; history?: any[] };

  return (
    <div className="space-y-5">
      <DetailHeader
        title={d.taskNumber}
        subtitle={d.taskType}
        badge={<><StatusBadge status={d.status} size="md" /><PriorityBadge priority={d.priority} /></>}
        backHref="/lien/servicing"
        backLabel="Back to Servicing"
        meta={[
          { label: 'Assigned', value: d.assignedTo },
          { label: 'Due', value: formatDate(d.dueDate) },
        ]}
        actions={
          <div className="flex gap-2">
            {d.status !== 'Completed' && <button className="text-sm px-3 py-1.5 bg-emerald-600 text-white rounded-lg hover:bg-emerald-700">Mark Complete</button>}
            {d.status !== 'Escalated' && d.status !== 'Completed' && <button className="text-sm px-3 py-1.5 border border-red-200 text-red-600 rounded-lg hover:bg-red-50">Escalate</button>}
          </div>
        }
      />

      <DetailSection title="Task Details" icon="ri-tools-line" fields={[
        { label: 'Description', value: d.description },
        { label: 'Case Reference', value: d.caseNumber ? <Link href="/lien/cases" className="text-primary hover:underline">{d.caseNumber}</Link> : undefined },
        { label: 'Lien Reference', value: d.lienNumber ? <Link href="/lien/liens" className="text-primary hover:underline">{d.lienNumber}</Link> : undefined },
        { label: 'Due Date', value: formatDate(d.dueDate) },
        { label: 'Created', value: formatDate(d.createdAtUtc) },
        { label: 'Last Updated', value: formatDate(d.updatedAtUtc) },
      ]} />

      {d.notes && (
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-gray-800 mb-2">Notes</h3>
          <p className="text-sm text-gray-600">{d.notes}</p>
        </div>
      )}

      {d.history && d.history.length > 0 && (
        <ActivityTimeline events={d.history} title="Action History" />
      )}
    </div>
  );
}
