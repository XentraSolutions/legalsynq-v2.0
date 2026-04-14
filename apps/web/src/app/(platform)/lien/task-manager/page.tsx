'use client';

import { useState, useEffect, useCallback, useMemo } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { KpiCard } from '@/components/lien/kpi-card';
import { PageHeader } from '@/components/lien/page-header';
import { FilterToolbar } from '@/components/lien/filter-toolbar';
import { StatusBadge, PriorityBadge } from '@/components/lien/status-badge';
import { ActionMenu } from '@/components/lien/action-menu';
import { AssignTaskForm } from '@/components/lien/forms/assign-task-form';
import { ConfirmDialog } from '@/components/lien/modal';
import { useLienStore, canPerformAction } from '@/stores/lien-store';
import { servicingService } from '@/lib/servicing';
import type { ServicingListItem, PaginationMeta } from '@/lib/servicing';

function formatDate(val: string): string {
  if (!val) return '\u2014';
  try {
    const d = new Date(val);
    if (isNaN(d.getTime())) return val;
    return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  } catch {
    return val;
  }
}

const STATUS_COLUMNS = [
  { key: 'Pending',    label: 'Pending',     icon: 'ri-time-line',              color: 'border-t-gray-400' },
  { key: 'InProgress', label: 'In Progress', icon: 'ri-loader-4-line',          color: 'border-t-blue-500' },
  { key: 'Escalated',  label: 'Escalated',   icon: 'ri-alarm-warning-line',     color: 'border-t-red-500' },
  { key: 'OnHold',     label: 'On Hold',     icon: 'ri-pause-circle-line',      color: 'border-t-amber-500' },
  { key: 'Completed',  label: 'Completed',   icon: 'ri-checkbox-circle-line',   color: 'border-t-green-500' },
] as const;

type ViewMode = 'board' | 'list';

export default function TaskManagerPage() {
  const router = useRouter();
  const addToast = useLienStore((s) => s.addToast);
  const role = useLienStore((s) => s.currentRole);

  const [items, setItems] = useState<ServicingListItem[]>([]);
  const [pagination, setPagination] = useState<PaginationMeta>({ page: 1, pageSize: 100, totalCount: 0, totalPages: 0 });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState('');
  const [priorityFilter, setPriorityFilter] = useState('');
  const [assigneeFilter, setAssigneeFilter] = useState('');
  const [viewMode, setViewMode] = useState<ViewMode>('board');
  const [showCreate, setShowCreate] = useState(false);
  const [confirmAction, setConfirmAction] = useState<{ id: string; status: string; label: string } | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);

  const canEdit = canPerformAction(role, 'edit');

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await servicingService.getItems({
        search: search || undefined,
        priority: priorityFilter || undefined,
        assignedTo: assigneeFilter || undefined,
        page: 1,
        pageSize: 100,
      });
      setItems(result.items);
      setPagination(result.pagination);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load tasks');
    } finally {
      setLoading(false);
    }
  }, [search, priorityFilter, assigneeFilter]);

  useEffect(() => { fetchData(); }, [fetchData]);

  const assignees = useMemo(() => {
    const set = new Set(items.map((s) => s.assignedTo));
    return Array.from(set).sort();
  }, [items]);

  const pendingCount = items.filter((s) => s.status === 'Pending').length;
  const inProgressCount = items.filter((s) => s.status === 'InProgress').length;
  const escalatedCount = items.filter((s) => s.status === 'Escalated').length;
  const overdueCount = items.filter((s) => s.status !== 'Completed' && s.dueDate && new Date(s.dueDate) < new Date()).length;

  async function handleQuickStatus(id: string, status: string) {
    setActionLoading(id);
    try {
      await servicingService.updateStatus(id, status);
      addToast({ type: 'success', title: `Task ${status === 'Completed' ? 'Completed' : status === 'InProgress' ? 'Started' : status}` });
      await fetchData();
    } catch (err) {
      addToast({ type: 'error', title: 'Action Failed', description: err instanceof Error ? err.message : 'Unknown error' });
    } finally {
      setActionLoading(null);
    }
  }

  async function handleEscalate(id: string) {
    setActionLoading(id);
    try {
      await servicingService.updateStatus(id, 'Escalated');
      addToast({ type: 'warning', title: 'Task Escalated' });
      await fetchData();
    } catch (err) {
      addToast({ type: 'error', title: 'Escalation Failed', description: err instanceof Error ? err.message : 'Unknown error' });
    } finally {
      setActionLoading(null);
    }
  }

  function getTaskActions(task: ServicingListItem) {
    const menuItems: { label: string; icon: string; onClick: () => void; variant?: 'danger'; disabled?: boolean; divider?: boolean }[] = [
      { label: 'View Details', icon: 'ri-eye-line', onClick: () => router.push(`/lien/servicing/${task.id}`) },
    ];
    if (canEdit && task.status !== 'Completed') {
      menuItems.push(
        { label: 'Start Work', icon: 'ri-play-line', onClick: () => handleQuickStatus(task.id, 'InProgress'), disabled: task.status === 'InProgress' },
        { label: 'Mark Complete', icon: 'ri-checkbox-circle-line', onClick: () => setConfirmAction({ id: task.id, status: 'Completed', label: 'Complete Task' }) },
        { label: 'Put On Hold', icon: 'ri-pause-circle-line', onClick: () => handleQuickStatus(task.id, 'OnHold'), disabled: task.status === 'OnHold' },
        { label: 'Escalate', icon: 'ri-alarm-warning-line', onClick: () => handleEscalate(task.id), variant: 'danger' as const, divider: true },
      );
    }
    return menuItems;
  }

  const isOverdue = (dueDate: string) => dueDate && new Date(dueDate) < new Date();

  return (
    <div className="space-y-5">
      <PageHeader title="Task Manager" subtitle={`${pagination.totalCount} tasks across ${assignees.length} team members`}
        actions={
          <div className="flex items-center gap-2">
            <div className="flex items-center border border-gray-200 rounded-lg overflow-hidden">
              <button onClick={() => setViewMode('board')} className={`px-3 py-1.5 text-sm ${viewMode === 'board' ? 'bg-primary text-white' : 'bg-white text-gray-600 hover:bg-gray-50'}`}>
                <i className="ri-layout-column-line mr-1" />Board
              </button>
              <button onClick={() => setViewMode('list')} className={`px-3 py-1.5 text-sm ${viewMode === 'list' ? 'bg-primary text-white' : 'bg-white text-gray-600 hover:bg-gray-50'}`}>
                <i className="ri-list-unordered mr-1" />List
              </button>
            </div>
            {canPerformAction(role, 'create') && (
              <button onClick={() => setShowCreate(true)} className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors">
                <i className="ri-add-line text-base" />New Task
              </button>
            )}
          </div>
        }
      />

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <KpiCard title="Pending" value={pendingCount} icon="ri-time-line" iconColor="text-gray-600" />
        <KpiCard title="In Progress" value={inProgressCount} icon="ri-loader-4-line" iconColor="text-blue-600" />
        <KpiCard title="Escalated" value={escalatedCount} change={escalatedCount > 0 ? 'Needs attention' : 'None'} changeType={escalatedCount > 0 ? 'down' : 'up'} icon="ri-alarm-warning-line" iconColor="text-red-600" />
        <KpiCard title="Overdue" value={overdueCount} change={overdueCount > 0 ? 'Past due date' : 'All on track'} changeType={overdueCount > 0 ? 'down' : 'up'} icon="ri-calendar-close-line" iconColor="text-amber-600" />
      </div>

      <FilterToolbar searchPlaceholder="Search tasks, descriptions, assignees..." onSearch={setSearch} filters={[
        { label: 'All Priorities', value: priorityFilter, onChange: setPriorityFilter, options: [{ value: 'Low', label: 'Low' }, { value: 'Normal', label: 'Normal' }, { value: 'High', label: 'High' }, { value: 'Urgent', label: 'Urgent' }] },
        { label: 'All Assignees', value: assigneeFilter, onChange: setAssigneeFilter, options: assignees.map((a) => ({ value: a, label: a })) },
      ]} />

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-4 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <i className="ri-error-warning-line text-red-500" />
            <span className="text-sm text-red-700">{error}</span>
          </div>
          <button onClick={fetchData} className="text-sm text-red-600 hover:text-red-800 font-medium">Retry</button>
        </div>
      )}

      {loading ? (
        <div className="p-10 text-center">
          <i className="ri-loader-4-line animate-spin text-2xl text-gray-400" />
          <p className="text-sm text-gray-400 mt-2">Loading tasks...</p>
        </div>
      ) : viewMode === 'board' ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5 gap-4">
          {STATUS_COLUMNS.map((col) => {
            const tasks = items.filter((t) => t.status === col.key);
            return (
              <div key={col.key} className={`bg-gray-50 rounded-xl border border-gray-200 border-t-4 ${col.color} min-h-[200px]`}>
                <div className="px-4 py-3 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <i className={`${col.icon} text-sm text-gray-500`} />
                    <span className="text-sm font-semibold text-gray-700">{col.label}</span>
                  </div>
                  <span className="text-xs font-medium text-gray-400 bg-white border border-gray-200 rounded-full px-2 py-0.5">{tasks.length}</span>
                </div>
                <div className="px-3 pb-3 space-y-2">
                  {tasks.map((task) => (
                    <div key={task.id} className={`bg-white border border-gray-200 rounded-lg p-3 hover:shadow-sm transition-shadow ${actionLoading === task.id ? 'opacity-50' : ''}`}>
                      <div className="flex items-start justify-between mb-2">
                        <Link href={`/lien/servicing/${task.id}`} className="text-xs font-mono text-primary hover:underline">{task.taskNumber}</Link>
                        <ActionMenu items={getTaskActions(task)} />
                      </div>
                      <p className="text-sm text-gray-700 line-clamp-2 mb-2">{task.description}</p>
                      <div className="flex items-center gap-2 mb-2">
                        <PriorityBadge priority={task.priority} />
                      </div>
                      <div className="flex items-center justify-between text-xs text-gray-400">
                        <span className="flex items-center gap-1">
                          <i className="ri-user-line" />{task.assignedTo.split(' ')[0]}
                        </span>
                        <span className={`flex items-center gap-1 ${isOverdue(task.dueDate) && task.status !== 'Completed' ? 'text-red-500 font-medium' : ''}`}>
                          <i className="ri-calendar-line" />{formatDate(task.dueDate)}
                          {isOverdue(task.dueDate) && task.status !== 'Completed' && <i className="ri-error-warning-line" />}
                        </span>
                      </div>
                    </div>
                  ))}
                  {tasks.length === 0 && (
                    <div className="text-center py-6 text-xs text-gray-400">No tasks</div>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      ) : (
        <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-100">
              <thead><tr className="bg-gray-50">
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Task #</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Type</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Description</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Assigned</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Priority</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Due</th>
                <th className="px-4 py-3" />
              </tr></thead>
              <tbody className="divide-y divide-gray-100">
                {items.map((task) => (
                  <tr key={task.id} className={`hover:bg-gray-50 transition-colors ${actionLoading === task.id ? 'opacity-50' : ''}`}>
                    <td className="px-4 py-3"><Link href={`/lien/servicing/${task.id}`} className="text-xs font-mono text-primary hover:underline">{task.taskNumber}</Link></td>
                    <td className="px-4 py-3 text-sm text-gray-700">{task.taskType}</td>
                    <td className="px-4 py-3 text-sm text-gray-600 max-w-xs truncate">{task.description}</td>
                    <td className="px-4 py-3 text-sm text-gray-500">{task.assignedTo}</td>
                    <td className="px-4 py-3"><PriorityBadge priority={task.priority} /></td>
                    <td className="px-4 py-3"><StatusBadge status={task.status} /></td>
                    <td className="px-4 py-3">
                      <span className={`text-xs whitespace-nowrap ${isOverdue(task.dueDate) && task.status !== 'Completed' ? 'text-red-500 font-medium' : 'text-gray-400'}`}>
                        {formatDate(task.dueDate)}
                        {isOverdue(task.dueDate) && task.status !== 'Completed' && <i className="ri-error-warning-line ml-1" />}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-right">
                      <ActionMenu items={getTaskActions(task)} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {items.length === 0 && !loading && <div className="p-10 text-center text-sm text-gray-400">No tasks match your filters.</div>}
        </div>
      )}

      <AssignTaskForm open={showCreate} onClose={() => setShowCreate(false)} onCreated={fetchData} />

      {confirmAction && (
        <ConfirmDialog open onClose={() => setConfirmAction(null)}
          onConfirm={async () => { await handleQuickStatus(confirmAction.id, confirmAction.status); setConfirmAction(null); }}
          title={confirmAction.label} description={`Mark this task as ${confirmAction.status.toLowerCase()}?`} confirmLabel={confirmAction.label}
        />
      )}
    </div>
  );
}
