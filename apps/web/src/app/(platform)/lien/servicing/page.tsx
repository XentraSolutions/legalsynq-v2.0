'use client';

import { useState } from 'react';
import Link from 'next/link';
import { PageHeader } from '@/components/lien/page-header';
import { FilterToolbar } from '@/components/lien/filter-toolbar';
import { StatusBadge, PriorityBadge } from '@/components/lien/status-badge';
import { ActionMenu } from '@/components/lien/action-menu';
import { AssignTaskForm } from '@/components/lien/forms/assign-task-form';
import { ConfirmDialog } from '@/components/lien/modal';
import { useLienStore, canPerformAction } from '@/stores/lien-store';
import { formatDate } from '@/lib/lien-mock-data';

export default function ServicingPage() {
  const servicing = useLienStore((s) => s.servicing);
  const updateServicing = useLienStore((s) => s.updateServicing);
  const addToast = useLienStore((s) => s.addToast);
  const role = useLienStore((s) => s.currentRole);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [priorityFilter, setPriorityFilter] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [confirmAction, setConfirmAction] = useState<{ id: string; status: string; label: string } | null>(null);

  const filtered = servicing.filter((s) => {
    if (search && !s.taskNumber.toLowerCase().includes(search.toLowerCase()) && !s.description.toLowerCase().includes(search.toLowerCase())) return false;
    if (statusFilter && s.status !== statusFilter) return false;
    if (priorityFilter && s.priority !== priorityFilter) return false;
    return true;
  });

  const canEdit = canPerformAction(role, 'edit');

  return (
    <div className="space-y-5">
      <PageHeader title="Servicing" subtitle={`${filtered.length} tasks`}
        actions={canPerformAction(role, 'create') ? (
          <button onClick={() => setShowCreate(true)} className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors">
            <i className="ri-add-line text-base" />Assign Task
          </button>
        ) : undefined}
      />
      <FilterToolbar searchPlaceholder="Search tasks..." onSearch={setSearch} filters={[
        { label: 'All Statuses', value: statusFilter, onChange: setStatusFilter, options: [{ value: 'Pending', label: 'Pending' }, { value: 'InProgress', label: 'In Progress' }, { value: 'Completed', label: 'Completed' }, { value: 'Escalated', label: 'Escalated' }, { value: 'OnHold', label: 'On Hold' }] },
        { label: 'All Priorities', value: priorityFilter, onChange: setPriorityFilter, options: [{ value: 'Low', label: 'Low' }, { value: 'Normal', label: 'Normal' }, { value: 'High', label: 'High' }, { value: 'Urgent', label: 'Urgent' }] },
      ]} />
      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-gray-100">
            <thead><tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Task #</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Type</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Description</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Case / Lien</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Assigned</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Priority</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Due</th>
              <th className="px-4 py-3" />
            </tr></thead>
            <tbody className="divide-y divide-gray-100">
              {filtered.map((s) => (
                <tr key={s.id} className="hover:bg-gray-50 transition-colors">
                  <td className="px-4 py-3"><Link href={`/lien/servicing/${s.id}`} className="text-xs font-mono text-primary hover:underline">{s.taskNumber}</Link></td>
                  <td className="px-4 py-3 text-sm text-gray-700">{s.taskType}</td>
                  <td className="px-4 py-3 text-sm text-gray-600 max-w-xs truncate">{s.description}</td>
                  <td className="px-4 py-3 text-xs font-mono text-gray-500">{[s.caseNumber, s.lienNumber].filter(Boolean).join(' / ') || '\u2014'}</td>
                  <td className="px-4 py-3 text-sm text-gray-500">{s.assignedTo}</td>
                  <td className="px-4 py-3"><PriorityBadge priority={s.priority} /></td>
                  <td className="px-4 py-3"><StatusBadge status={s.status} /></td>
                  <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap">{formatDate(s.dueDate)}</td>
                  <td className="px-4 py-3 text-right">
                    <ActionMenu items={[
                      { label: 'View Details', icon: 'ri-eye-line', onClick: () => {} },
                      ...(canEdit && s.status !== 'Completed' ? [
                        { label: 'Start Work', icon: 'ri-play-line', onClick: () => { updateServicing(s.id, { status: 'InProgress' }); addToast({ type: 'success', title: 'Task Started' }); }, disabled: s.status === 'InProgress' },
                        { label: 'Mark Complete', icon: 'ri-checkbox-circle-line', onClick: () => setConfirmAction({ id: s.id, status: 'Completed', label: 'Complete Task' }) },
                        { label: 'Escalate', icon: 'ri-alarm-warning-line', onClick: () => { updateServicing(s.id, { status: 'Escalated', priority: 'Urgent' }); addToast({ type: 'warning', title: 'Task Escalated' }); }, variant: 'danger' as const, divider: true },
                      ] : []),
                      ...(canEdit && s.status !== 'Completed' ? [{ label: 'Reassign', icon: 'ri-user-shared-line', onClick: () => {
                        const assignees = ['Sarah Chen', 'Michael Park', 'Lisa Wang'];
                        const next = assignees.find((a) => a !== s.assignedTo) || assignees[0];
                        updateServicing(s.id, { assignedTo: next });
                        addToast({ type: 'success', title: 'Task Reassigned', description: `Now assigned to ${next}` });
                      }}] : []),
                    ]} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {filtered.length === 0 && <div className="p-10 text-center text-sm text-gray-400">No tasks match your filters.</div>}
      </div>

      <AssignTaskForm open={showCreate} onClose={() => setShowCreate(false)} />

      {confirmAction && (
        <ConfirmDialog open onClose={() => setConfirmAction(null)}
          onConfirm={() => { updateServicing(confirmAction.id, { status: confirmAction.status }); addToast({ type: 'success', title: confirmAction.label }); setConfirmAction(null); }}
          title={confirmAction.label} description={`Mark this task as ${confirmAction.status.toLowerCase()}?`} confirmLabel={confirmAction.label}
        />
      )}
    </div>
  );
}
