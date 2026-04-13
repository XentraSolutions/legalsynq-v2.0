'use client';

import { use, useState } from 'react';
import Link from 'next/link';
import { useLienStore, canPerformAction } from '@/stores/lien-store';
import { formatDate } from '@/lib/lien-mock-data';
import { DetailHeader, DetailSection } from '@/components/lien/detail-section';
import { StatusBadge, PriorityBadge } from '@/components/lien/status-badge';
import { StatusProgress } from '@/components/lien/status-progress';
import { ConfirmDialog } from '@/components/lien/modal';
import { ActivityTimeline } from '@/components/lien/activity-timeline';

const TASK_STEPS = ['Pending', 'In Progress', 'Completed'];

export default function ServicingDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const servicing = useLienStore((s) => s.servicing);
  const servicingDetails = useLienStore((s) => s.servicingDetails);
  const updateServicing = useLienStore((s) => s.updateServicing);
  const addToast = useLienStore((s) => s.addToast);
  const role = useLienStore((s) => s.currentRole);
  const [confirmAction, setConfirmAction] = useState<{ status: string; label: string } | null>(null);

  const summary = servicing.find((s) => s.id === id);
  const detail = servicingDetails[id];
  const item = detail ? { ...summary, ...detail } : summary;
  if (!item) return <div className="p-10 text-center text-gray-400">Servicing task not found.</div>;
  const d = item as any;
  const canEdit = canPerformAction(role, 'edit');
  const statusStep = d.status === 'InProgress' ? 'In Progress' : d.status === 'Escalated' ? 'In Progress' : d.status;

  return (
    <div className="space-y-5">
      <DetailHeader title={d.taskNumber} subtitle={d.taskType}
        badge={<><StatusBadge status={d.status} size="md" /><PriorityBadge priority={d.priority} /></>}
        backHref="/lien/servicing" backLabel="Back to Servicing"
        meta={[
          { label: 'Assigned', value: d.assignedTo },
          { label: 'Due', value: formatDate(d.dueDate) },
        ]}
        actions={canEdit ? (
          <div className="flex gap-2">
            {d.status === 'Pending' && <button onClick={() => { updateServicing(id, { status: 'InProgress' }); addToast({ type: 'success', title: 'Task Started' }); }} className="text-sm px-3 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90">Start Work</button>}
            {d.status !== 'Completed' && <button onClick={() => setConfirmAction({ status: 'Completed', label: 'Mark Complete' })} className="text-sm px-3 py-1.5 bg-emerald-600 text-white rounded-lg hover:bg-emerald-700">Mark Complete</button>}
            {d.status !== 'Escalated' && d.status !== 'Completed' && <button onClick={() => { updateServicing(id, { status: 'Escalated', priority: 'Urgent' }); addToast({ type: 'warning', title: 'Task Escalated' }); }} className="text-sm px-3 py-1.5 border border-red-200 text-red-600 rounded-lg hover:bg-red-50">Escalate</button>}
            {d.status !== 'Completed' && <button onClick={() => {
              const assignees = ['Sarah Chen', 'Michael Park', 'Lisa Wang'];
              const next = assignees.find((a) => a !== d.assignedTo) || assignees[0];
              updateServicing(id, { assignedTo: next });
              addToast({ type: 'success', title: 'Task Reassigned', description: `Now assigned to ${next}` });
            }} className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">Reassign</button>}
          </div>
        ) : undefined}
      />

      <div className="bg-white border border-gray-200 rounded-xl p-5">
        <h3 className="text-sm font-semibold text-gray-800 mb-4">Task Progress</h3>
        <StatusProgress steps={TASK_STEPS} currentStep={statusStep} />
      </div>

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

      {d.history && d.history.length > 0 && <ActivityTimeline events={d.history} title="Action History" />}

      {confirmAction && (
        <ConfirmDialog open onClose={() => setConfirmAction(null)}
          onConfirm={() => { updateServicing(id, { status: confirmAction.status }); addToast({ type: 'success', title: confirmAction.label }); setConfirmAction(null); }}
          title={confirmAction.label} description={`Mark task ${d.taskNumber} as ${confirmAction.status.toLowerCase()}?`} confirmLabel={confirmAction.label}
        />
      )}
    </div>
  );
}
