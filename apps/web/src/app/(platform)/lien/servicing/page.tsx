'use client';

import { useState } from 'react';
import Link from 'next/link';
import { PageHeader } from '@/components/lien/page-header';
import { FilterToolbar } from '@/components/lien/filter-toolbar';
import { StatusBadge, PriorityBadge } from '@/components/lien/status-badge';
import { MOCK_SERVICING, formatDate } from '@/lib/lien-mock-data';

export default function ServicingPage() {
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [priorityFilter, setPriorityFilter] = useState('');

  const filtered = MOCK_SERVICING.filter((s) => {
    if (search && !s.taskNumber.toLowerCase().includes(search.toLowerCase()) && !s.description.toLowerCase().includes(search.toLowerCase())) return false;
    if (statusFilter && s.status !== statusFilter) return false;
    if (priorityFilter && s.priority !== priorityFilter) return false;
    return true;
  });

  return (
    <div className="space-y-5">
      <PageHeader title="Servicing" subtitle={`${filtered.length} tasks`} />
      <FilterToolbar searchPlaceholder="Search tasks..." onSearch={setSearch} filters={[
        { label: 'All Statuses', value: statusFilter, onChange: setStatusFilter, options: [{ value: 'Pending', label: 'Pending' }, { value: 'InProgress', label: 'In Progress' }, { value: 'Completed', label: 'Completed' }, { value: 'Escalated', label: 'Escalated' }, { value: 'OnHold', label: 'On Hold' }] },
        { label: 'All Priorities', value: priorityFilter, onChange: setPriorityFilter, options: [{ value: 'Low', label: 'Low' }, { value: 'Normal', label: 'Normal' }, { value: 'High', label: 'High' }, { value: 'Urgent', label: 'Urgent' }] },
      ]} />
      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-gray-100">
            <thead>
              <tr className="bg-gray-50">
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Task #</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Type</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Description</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Case / Lien</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Assigned</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Priority</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Due</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
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
                  <td className="px-4 py-3 text-right"><Link href={`/lien/servicing/${s.id}`} className="text-xs text-primary font-medium hover:underline">View &rarr;</Link></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {filtered.length === 0 && <div className="p-10 text-center text-sm text-gray-400">No tasks match your filters.</div>}
      </div>
    </div>
  );
}
