'use client';

import { useState, useEffect, useCallback, useMemo } from 'react';
import { lienTasksService } from '@/lib/liens/lien-tasks.service';
import { apiClient } from '@/lib/api-client';
import type { TaskDto, TaskStatus, TaskPriority, TasksQuery } from '@/lib/liens/lien-tasks.types';
import {
  TASK_STATUS_LABELS,
  TASK_STATUS_COLORS,
  TASK_PRIORITY_COLORS,
  TASK_PRIORITY_ICONS,
  BOARD_COLUMNS,
  ALL_TASK_STATUSES,
} from '@/lib/liens/lien-tasks.types';
import type { TenantUser } from '@/types/tenant';
import { TaskCard } from './task-card';
import { TaskDetailDrawer } from './task-detail-drawer';
import { CreateEditTaskForm } from './forms/create-edit-task-form';

type ViewMode = 'board' | 'list';

const PRIORITY_LABELS: Record<string, string> = {
  LOW: 'Low', MEDIUM: 'Medium', HIGH: 'High', URGENT: 'Urgent',
};

const AVATAR_COLORS = [
  'bg-violet-500', 'bg-blue-500', 'bg-teal-500',
  'bg-indigo-500', 'bg-pink-500', 'bg-amber-500',
];

function avatarColor(id: string): string {
  let hash = 0;
  for (let i = 0; i < id.length; i++) hash = (hash * 31 + id.charCodeAt(i)) >>> 0;
  return AVATAR_COLORS[hash % AVATAR_COLORS.length];
}

function getInitials(first: string, last: string): string {
  return `${first.charAt(0)}${last.charAt(0)}`.toUpperCase();
}

function formatDate(val?: string | null): string {
  if (!val) return '—';
  try {
    const d = new Date(val);
    return isNaN(d.getTime()) ? val : d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  } catch { return val ?? '—'; }
}

function isOverdue(dueDate?: string | null, status?: string): boolean {
  if (!dueDate || status === 'COMPLETED' || status === 'CANCELLED') return false;
  return new Date(dueDate) < new Date();
}

interface CaseTaskManagerProps {
  caseId: string;
  workflowStageId?: string;
}

export function CaseTaskManager({ caseId, workflowStageId }: CaseTaskManagerProps) {
  const [tasks, setTasks]         = useState<TaskDto[]>([]);
  const [loading, setLoading]     = useState(true);
  const [error, setError]         = useState<string | null>(null);
  const [viewMode, setViewMode]   = useState<ViewMode>('board');
  const [usersById, setUsersById] = useState<Map<string, TenantUser>>(new Map());
  const [users, setUsers]         = useState<TenantUser[]>([]);

  const [search, setSearch]               = useState('');
  const [statusFilter, setStatusFilter]   = useState<TaskStatus | ''>('');
  const [priorityFilter, setPriorityFilter] = useState<TaskPriority | ''>('');
  const [assigneeFilter, setAssigneeFilter] = useState<string>('');

  const [showCreate, setShowCreate] = useState(false);
  const [editTask, setEditTask]     = useState<TaskDto | undefined>();
  const [detailTask, setDetailTask] = useState<TaskDto | null>(null);

  useEffect(() => {
    apiClient.get<TenantUser[]>('/identity/api/users')
      .then(({ data }) => {
        const list = data ?? [];
        const map = new Map<string, TenantUser>();
        list.forEach((u) => map.set(u.id, u));
        setUsers(list);
        setUsersById(map);
      })
      .catch(() => {});
  }, []);

  const fetchTasks = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const query: TasksQuery = {
        caseId,
        search:     search || undefined,
        status:     statusFilter || undefined,
        priority:   priorityFilter || undefined,
        assignedUserId: assigneeFilter || undefined,
        pageSize: 200,
        page: 1,
      };
      const result = await lienTasksService.getTasks(query);
      setTasks(result.items);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load tasks');
    } finally {
      setLoading(false);
    }
  }, [caseId, search, statusFilter, priorityFilter, assigneeFilter]);

  useEffect(() => { fetchTasks(); }, [fetchTasks]);

  const kpis = useMemo(() => ({
    total:      tasks.length,
    inProgress: tasks.filter((t) => t.status === 'IN_PROGRESS').length,
    blocked:    tasks.filter((t) => t.status === 'WAITING_BLOCKED').length,
    overdue:    tasks.filter((t) => isOverdue(t.dueDate, t.status)).length,
  }), [tasks]);

  const boardColumns = BOARD_COLUMNS.map((status) => ({
    status,
    label:       TASK_STATUS_LABELS[status],
    borderColor: TASK_STATUS_COLORS[status].border,
    items:       tasks.filter((t) => t.status === status),
  }));

  const activeFilters = [search, statusFilter, priorityFilter, assigneeFilter].filter(Boolean).length;

  function clearFilters() {
    setSearch('');
    setStatusFilter('');
    setPriorityFilter('');
    setAssigneeFilter('');
  }

  return (
    <div className="space-y-4">

      {/* ── Toolbar ──────────────────────────────────────────────────── */}
      <div className="flex items-center justify-between gap-3 flex-wrap">
        <div className="flex items-center gap-2">
          <span className="text-sm font-semibold text-gray-700">Task Manager</span>
          <span className="text-xs bg-gray-100 text-gray-500 rounded-full px-2 py-0.5">
            {tasks.length} task{tasks.length !== 1 ? 's' : ''}
          </span>
        </div>
        <div className="flex items-center gap-2">
          {/* View toggle */}
          <div className="flex items-center border border-gray-200 rounded-lg overflow-hidden">
            <button
              onClick={() => setViewMode('board')}
              className={`px-3 py-1.5 text-xs flex items-center gap-1 ${viewMode === 'board' ? 'bg-primary text-white' : 'bg-white text-gray-600 hover:bg-gray-50'}`}
            >
              <i className="ri-layout-column-line" /> Board
            </button>
            <button
              onClick={() => setViewMode('list')}
              className={`px-3 py-1.5 text-xs flex items-center gap-1 ${viewMode === 'list' ? 'bg-primary text-white' : 'bg-white text-gray-600 hover:bg-gray-50'}`}
            >
              <i className="ri-list-unordered" /> List
            </button>
          </div>
          <button
            onClick={() => setShowCreate(true)}
            className="flex items-center gap-1.5 text-xs font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-3 py-1.5"
          >
            <i className="ri-add-line" /> New Task
          </button>
        </div>
      </div>

      {/* ── KPIs ─────────────────────────────────────────────────────── */}
      <div className="grid grid-cols-4 gap-3">
        {[
          { label: 'Total',       value: kpis.total,      icon: 'ri-task-line',          color: 'text-gray-600'  },
          { label: 'In Progress', value: kpis.inProgress,  icon: 'ri-loader-4-line',      color: 'text-blue-600'  },
          { label: 'Blocked',     value: kpis.blocked,     icon: 'ri-pause-circle-line',  color: 'text-amber-600' },
          { label: 'Overdue',     value: kpis.overdue,     icon: 'ri-alarm-warning-line', color: 'text-red-600'   },
        ].map((k) => (
          <div key={k.label} className="bg-gray-50 rounded-xl px-4 py-3 border border-gray-100">
            <div className={`flex items-center gap-1.5 text-xs font-medium ${k.color} mb-1`}>
              <i className={k.icon} /> {k.label}
            </div>
            <div className="text-2xl font-bold text-gray-800">{k.value}</div>
          </div>
        ))}
      </div>

      {/* ── Filters ──────────────────────────────────────────────────── */}
      <div className="flex items-center gap-2 flex-wrap bg-white border border-gray-100 rounded-xl px-4 py-3">
        <div className="relative flex-1 min-w-[160px]">
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
          value={assigneeFilter}
          onChange={(e) => setAssigneeFilter(e.target.value)}
          className="text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-primary/30"
        >
          <option value="">All Assignees</option>
          {users.map((u) => (
            <option key={u.id} value={u.id}>{u.firstName} {u.lastName}</option>
          ))}
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
            <option key={p} value={p}>{PRIORITY_LABELS[p]}</option>
          ))}
        </select>
        {activeFilters > 0 && (
          <button
            onClick={clearFilters}
            className="flex items-center gap-1 text-xs text-gray-500 hover:text-gray-700 border border-gray-200 rounded-lg px-3 py-2"
          >
            <i className="ri-close-line" /> Clear ({activeFilters})
          </button>
        )}
      </div>

      {/* ── Error ────────────────────────────────────────────────────── */}
      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-4 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <i className="ri-error-warning-line text-red-500" />
            <span className="text-sm text-red-700">{error}</span>
          </div>
          <button onClick={fetchTasks} className="text-sm text-red-600 hover:text-red-800 font-medium">Retry</button>
        </div>
      )}

      {/* ── Loading ──────────────────────────────────────────────────── */}
      {loading ? (
        <div className="p-10 text-center">
          <i className="ri-loader-4-line animate-spin text-2xl text-gray-400" />
          <p className="text-sm text-gray-400 mt-2">Loading tasks...</p>
        </div>

      ) : viewMode === 'board' ? (
        /* ── Board view ──────────────────────────────────────────────── */
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          {boardColumns.map((col) => (
            <div
              key={col.status}
              className={`bg-gray-50 rounded-xl border border-gray-200 border-t-4 ${col.borderColor} min-h-[200px]`}
            >
              <div className="px-4 py-3 flex items-center justify-between border-b border-gray-100">
                <span className="text-sm font-semibold text-gray-700">{col.label}</span>
                <span className="text-xs font-medium text-gray-400 bg-white border border-gray-200 rounded-full px-2 py-0.5">
                  {col.items.length}
                </span>
              </div>
              <div className="p-3 space-y-2">
                {col.items.map((task) => (
                  <TaskCard
                    key={task.id}
                    task={task}
                    onClick={(t) => setDetailTask(t)}
                    compact
                    assigneeUser={task.assignedUserId ? (usersById.get(task.assignedUserId) ?? null) : null}
                  />
                ))}
                {col.items.length === 0 && (
                  <div className="text-center py-6 text-xs text-gray-300">No tasks</div>
                )}
              </div>
            </div>
          ))}
        </div>

      ) : (
        /* ── List view ───────────────────────────────────────────────── */
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
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {tasks.map((task) => {
                  const assignee = task.assignedUserId ? usersById.get(task.assignedUserId) : undefined;
                  return (
                    <tr
                      key={task.id}
                      className="hover:bg-gray-50 cursor-pointer"
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
                          ${task.status === 'COMPLETED'       ? 'bg-green-100 text-green-700' :
                            task.status === 'CANCELLED'       ? 'bg-red-100 text-red-700' :
                            task.status === 'IN_PROGRESS'     ? 'bg-blue-100 text-blue-700' :
                            task.status === 'WAITING_BLOCKED' ? 'bg-amber-100 text-amber-700' :
                                                                'bg-gray-100 text-gray-600'}`}>
                          {TASK_STATUS_LABELS[task.status]}
                        </span>
                      </td>
                      <td className="px-4 py-3">
                        <span className={`text-xs font-medium ${TASK_PRIORITY_COLORS[task.priority]}`}>
                          {PRIORITY_LABELS[task.priority] ?? task.priority}
                        </span>
                      </td>
                      <td className="px-4 py-3">
                        {assignee ? (
                          <div className="flex items-center gap-1.5">
                            <div className={`w-6 h-6 rounded-full flex items-center justify-center text-white text-[10px] font-bold shrink-0 ${avatarColor(task.assignedUserId!)}`}>
                              {getInitials(assignee.firstName, assignee.lastName)}
                            </div>
                            <span className="text-sm text-gray-700 whitespace-nowrap">
                              {assignee.firstName} {assignee.lastName}
                            </span>
                          </div>
                        ) : task.assignedUserId ? (
                          <span className="flex items-center gap-1 text-xs text-gray-400">
                            <i className="ri-user-line" />Assigned
                          </span>
                        ) : (
                          <span className="text-gray-300 text-xs">&mdash;</span>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        {task.linkedLiens.length > 0 ? (
                          <span className="bg-purple-50 text-purple-700 text-xs rounded px-1.5 py-0.5">
                            {task.linkedLiens.length}
                          </span>
                        ) : <span className="text-gray-300 text-xs">&mdash;</span>}
                      </td>
                      <td className="px-4 py-3">
                        <span className={`text-xs ${isOverdue(task.dueDate, task.status) ? 'text-red-600 font-medium' : 'text-gray-400'}`}>
                          {formatDate(task.dueDate)}
                          {isOverdue(task.dueDate, task.status) && <i className="ri-error-warning-line ml-1" />}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-xs text-gray-400">{formatDate(task.updatedAtUtc)}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          {tasks.length === 0 && !loading && (
            <div className="p-10 text-center text-sm text-gray-400">
              {activeFilters > 0 ? 'No tasks match your filters.' : 'No tasks yet.'}
            </div>
          )}
        </div>
      )}

      {/* ── Dialogs ───────────────────────────────────────────────────── */}
      <CreateEditTaskForm
        open={showCreate}
        onClose={() => setShowCreate(false)}
        onSaved={() => { fetchTasks(); setShowCreate(false); }}
        prefillCaseId={caseId}
        prefillWorkflowStageId={workflowStageId}
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
