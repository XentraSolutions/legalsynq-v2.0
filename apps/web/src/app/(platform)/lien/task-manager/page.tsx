'use client';

import { useState, useEffect, useCallback, useMemo } from 'react';
import { lienTasksService } from '@/lib/liens/lien-tasks.service';
import type { TaskDto, TaskStatus, TaskPriority, TasksQuery } from '@/lib/liens/lien-tasks.types';
import {
  TASK_STATUS_LABELS,
  TASK_STATUS_COLORS,
  TASK_PRIORITY_COLORS,
  TASK_PRIORITY_ICONS,
  BOARD_COLUMNS,
  ALL_TASK_STATUSES,
} from '@/lib/liens/lien-tasks.types';
import { useLienStore } from '@/stores/lien-store';
import { PageHeader } from '@/components/lien/page-header';
import { CreateEditTaskForm } from '@/components/lien/forms/create-edit-task-form';
import { TaskCard } from '@/components/lien/task-card';
import { TaskDetailDrawer } from '@/components/lien/task-detail-drawer';

type ViewMode = 'board' | 'list';
type AssignmentScope = 'all' | 'me' | 'others' | 'unassigned';

function formatDate(val?: string | null): string {
  if (!val) return '\u2014';
  try {
    const d = new Date(val);
    return isNaN(d.getTime()) ? val : d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  } catch { return val ?? '\u2014'; }
}

function isOverdue(dueDate?: string | null, status?: string): boolean {
  if (!dueDate || status === 'COMPLETED' || status === 'CANCELLED') return false;
  return new Date(dueDate) < new Date();
}

export default function TaskManagerPage() {
  const addToast = useLienStore((s) => s.addToast);

  const [tasks, setTasks] = useState<TaskDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [viewMode, setViewMode] = useState<ViewMode>('board');

  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<TaskStatus | ''>('');
  const [priorityFilter, setPriorityFilter] = useState<TaskPriority | ''>('');
  const [assignmentScope, setAssignmentScope] = useState<AssignmentScope>('all');

  const [showCreate, setShowCreate] = useState(false);
  const [editTask, setEditTask] = useState<TaskDto | undefined>();
  const [detailTask, setDetailTask] = useState<TaskDto | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);

  const fetchTasks = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const query: TasksQuery = {
        search: search || undefined,
        status: statusFilter || undefined,
        priority: priorityFilter || undefined,
        assignmentScope: assignmentScope === 'all' ? undefined : assignmentScope,
        pageSize: 200,
        page: 1,
      };
      const result = await lienTasksService.getTasks(query);
      setTasks(result.items);
      setTotalCount(result.totalCount);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load tasks');
    } finally {
      setLoading(false);
    }
  }, [search, statusFilter, priorityFilter, assignmentScope]);

  useEffect(() => { fetchTasks(); }, [fetchTasks]);

  async function handleComplete(id: string) {
    setActionLoading(id);
    try {
      await lienTasksService.completeTask(id);
      addToast({ type: 'success', title: 'Task Completed' });
      fetchTasks();
    } catch {
      addToast({ type: 'error', title: 'Failed to complete task' });
    } finally {
      setActionLoading(null);
    }
  }

  async function handleCancel(id: string) {
    setActionLoading(id);
    try {
      await lienTasksService.cancelTask(id);
      addToast({ type: 'info', title: 'Task Cancelled' });
      fetchTasks();
    } catch {
      addToast({ type: 'error', title: 'Failed to cancel task' });
    } finally {
      setActionLoading(null);
    }
  }

  const kpis = useMemo(() => ({
    total:      tasks.length,
    new_:       tasks.filter((t) => t.status === 'NEW').length,
    inProgress: tasks.filter((t) => t.status === 'IN_PROGRESS').length,
    blocked:    tasks.filter((t) => t.status === 'WAITING_BLOCKED').length,
    overdue:    tasks.filter((t) => isOverdue(t.dueDate, t.status)).length,
  }), [tasks]);

  const boardColumns = BOARD_COLUMNS.map((status) => ({
    status,
    label: TASK_STATUS_LABELS[status],
    borderColor: TASK_STATUS_COLORS[status].border,
    items: tasks.filter((t) => t.status === status),
  }));

  return (
    <div className="space-y-5">
      <PageHeader
        title="Task Manager"
        subtitle={`${totalCount} task${totalCount !== 1 ? 's' : ''} total`}
        actions={
          <div className="flex items-center gap-2">
            <div className="flex items-center border border-gray-200 rounded-lg overflow-hidden">
              <button
                onClick={() => setViewMode('board')}
                className={`px-3 py-1.5 text-sm flex items-center gap-1 ${viewMode === 'board' ? 'bg-primary text-white' : 'bg-white text-gray-600 hover:bg-gray-50'}`}
              >
                <i className="ri-layout-column-line" /> Board
              </button>
              <button
                onClick={() => setViewMode('list')}
                className={`px-3 py-1.5 text-sm flex items-center gap-1 ${viewMode === 'list' ? 'bg-primary text-white' : 'bg-white text-gray-600 hover:bg-gray-50'}`}
              >
                <i className="ri-list-unordered" /> List
              </button>
            </div>
            <button
              onClick={() => setShowCreate(true)}
              className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2"
            >
              <i className="ri-add-line" /> New Task
            </button>
          </div>
        }
      />

      {/* KPIs */}
      <div className="grid grid-cols-2 sm:grid-cols-5 gap-3">
        {[
          { label: 'Total',       value: kpis.total,      icon: 'ri-task-line',             color: 'text-gray-600'  },
          { label: 'New',         value: kpis.new_,        icon: 'ri-time-line',             color: 'text-gray-500'  },
          { label: 'In Progress', value: kpis.inProgress,  icon: 'ri-loader-4-line',         color: 'text-blue-600'  },
          { label: 'Blocked',     value: kpis.blocked,     icon: 'ri-pause-circle-line',     color: 'text-amber-600' },
          { label: 'Overdue',     value: kpis.overdue,     icon: 'ri-alarm-warning-line',    color: 'text-red-600'   },
        ].map((k) => (
          <div key={k.label} className="bg-gray-50 rounded-xl px-4 py-3 border border-gray-100">
            <div className={`flex items-center gap-1.5 text-xs font-medium ${k.color} mb-1`}>
              <i className={k.icon} /> {k.label}
            </div>
            <div className="text-2xl font-bold text-gray-800">{k.value}</div>
          </div>
        ))}
      </div>

      {/* Filters */}
      <div className="flex items-center gap-3 flex-wrap bg-white border border-gray-100 rounded-xl px-4 py-3">
        <div className="relative flex-1 min-w-[180px]">
          <i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm" />
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search tasks..."
            className="w-full pl-9 pr-3 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/30"
          />
        </div>
        <select
          value={assignmentScope}
          onChange={(e) => setAssignmentScope(e.target.value as AssignmentScope)}
          className="text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-primary/30"
        >
          <option value="all">All Assignees</option>
          <option value="me">My Tasks</option>
          <option value="others">Others&apos; Tasks</option>
          <option value="unassigned">Unassigned</option>
        </select>
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value as TaskStatus | '')}
          className="text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-primary/30"
        >
          <option value="">All Statuses</option>
          {ALL_TASK_STATUSES.map((s) => (
            <option key={s} value={s}>{TASK_STATUS_LABELS[s]}</option>
          ))}
        </select>
        <select
          value={priorityFilter}
          onChange={(e) => setPriorityFilter(e.target.value as TaskPriority | '')}
          className="text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-primary/30"
        >
          <option value="">All Priorities</option>
          {(['LOW', 'MEDIUM', 'HIGH', 'URGENT'] as TaskPriority[]).map((p) => (
            <option key={p} value={p}>{p.charAt(0) + p.slice(1).toLowerCase()}</option>
          ))}
        </select>
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-4 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <i className="ri-error-warning-line text-red-500" />
            <span className="text-sm text-red-700">{error}</span>
          </div>
          <button onClick={fetchTasks} className="text-sm text-red-600 hover:text-red-800 font-medium">Retry</button>
        </div>
      )}

      {loading ? (
        <div className="p-10 text-center">
          <i className="ri-loader-4-line animate-spin text-2xl text-gray-400" />
          <p className="text-sm text-gray-400 mt-2">Loading tasks...</p>
        </div>
      ) : viewMode === 'board' ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          {boardColumns.map((col) => (
            <div key={col.status} className={`bg-gray-50 rounded-xl border border-gray-200 border-t-4 ${col.borderColor} min-h-[200px]`}>
              <div className="px-4 py-3 flex items-center justify-between border-b border-gray-100">
                <span className="text-sm font-semibold text-gray-700">{col.label}</span>
                <span className="text-xs font-medium text-gray-400 bg-white border border-gray-200 rounded-full px-2 py-0.5">
                  {col.items.length}
                </span>
              </div>
              <div className="p-3 space-y-2">
                {col.items.map((task) => (
                  <div key={task.id} className={actionLoading === task.id ? 'opacity-50' : ''}>
                    <TaskCard
                      task={task}
                      onComplete={handleComplete}
                      onCancel={handleCancel}
                      onClick={(t) => setDetailTask(t)}
                      compact
                    />
                  </div>
                ))}
                {col.items.length === 0 && (
                  <div className="text-center py-6 text-xs text-gray-300">No tasks</div>
                )}
              </div>
            </div>
          ))}
        </div>
      ) : (
        <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-100">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Title</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Priority</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Assignee</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Liens</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Due</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Updated</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {tasks.map((task) => (
                  <tr
                    key={task.id}
                    className={`hover:bg-gray-50 cursor-pointer ${actionLoading === task.id ? 'opacity-50' : ''}`}
                    onClick={() => setDetailTask(task)}
                  >
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <i className={`${TASK_PRIORITY_ICONS[task.priority]} text-sm ${TASK_PRIORITY_COLORS[task.priority]}`} />
                        <span className="text-sm font-medium text-gray-800 line-clamp-1">{task.title}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <span className={`inline-flex text-xs font-medium px-2 py-0.5 rounded-full
                        ${task.status === 'COMPLETED' ? 'bg-green-100 text-green-700' :
                          task.status === 'CANCELLED' ? 'bg-red-100 text-red-700' :
                          task.status === 'IN_PROGRESS' ? 'bg-blue-100 text-blue-700' :
                          task.status === 'WAITING_BLOCKED' ? 'bg-amber-100 text-amber-700' :
                          'bg-gray-100 text-gray-600'}`}>
                        {TASK_STATUS_LABELS[task.status]}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <span className={`text-xs font-medium ${TASK_PRIORITY_COLORS[task.priority]}`}>
                        {task.priority.charAt(0) + task.priority.slice(1).toLowerCase()}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-500">
                      {task.assignedUserId ? (
                        <span className="flex items-center gap-1 text-xs"><i className="ri-user-line" />Assigned</span>
                      ) : <span className="text-gray-300 text-xs">\u2014</span>}
                    </td>
                    <td className="px-4 py-3">
                      {task.linkedLiens.length > 0 ? (
                        <span className="bg-purple-50 text-purple-700 text-xs rounded px-1.5 py-0.5">
                          {task.linkedLiens.length}
                        </span>
                      ) : <span className="text-gray-300 text-xs">\u2014</span>}
                    </td>
                    <td className="px-4 py-3">
                      <span className={`text-xs ${isOverdue(task.dueDate, task.status) ? 'text-red-600 font-medium' : 'text-gray-400'}`}>
                        {formatDate(task.dueDate)}
                        {isOverdue(task.dueDate, task.status) && <i className="ri-error-warning-line ml-1" />}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-xs text-gray-400">{formatDate(task.updatedAtUtc)}</td>
                    <td className="px-4 py-3 text-right">
                      <div className="flex items-center justify-end gap-1">
                        {task.status !== 'COMPLETED' && task.status !== 'CANCELLED' && (
                          <>
                            <button
                              onClick={(e) => { e.stopPropagation(); handleComplete(task.id); }}
                              className="p-1.5 text-green-500 hover:text-green-700 hover:bg-green-50 rounded"
                              title="Complete"
                            >
                              <i className="ri-checkbox-circle-line" />
                            </button>
                            <button
                              onClick={(e) => { e.stopPropagation(); handleCancel(task.id); }}
                              className="p-1.5 text-red-400 hover:text-red-600 hover:bg-red-50 rounded"
                              title="Cancel"
                            >
                              <i className="ri-close-circle-line" />
                            </button>
                          </>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {tasks.length === 0 && !loading && (
            <div className="p-10 text-center text-sm text-gray-400">No tasks match your filters.</div>
          )}
        </div>
      )}

      <CreateEditTaskForm
        open={showCreate}
        onClose={() => setShowCreate(false)}
        onSaved={() => { fetchTasks(); setShowCreate(false); }}
      />

      {editTask && (
        <CreateEditTaskForm
          open
          onClose={() => setEditTask(undefined)}
          onSaved={() => { fetchTasks(); setEditTask(undefined); }}
          editTask={editTask}
        />
      )}

      <TaskDetailDrawer
        task={detailTask}
        onClose={() => setDetailTask(null)}
        onEdit={(t) => { setDetailTask(null); setEditTask(t); }}
        onStatusChange={(updated) => {
          setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
          setDetailTask(updated);
        }}
      />
    </div>
  );
}
