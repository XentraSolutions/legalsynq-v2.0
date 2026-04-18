'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '@/lib/api-client';
import { tasksApi, type MyTask } from '@/lib/tasks';
import { QueueTaskRow } from './queue-task-row';

/**
 * LS-FLOW-E15 — Role Queue tab. Lists open RoleQueue tasks the
 * caller is eligible to claim. The eligibility filter is enforced
 * server-side; the frontend only triggers a refetch after each
 * successful claim and on demand.
 */
export interface RoleQueueClientProps {
  /** Called when the user clicks a row to open the task-detail drawer. */
  onOpenTask: (taskId: string) => void;
  /**
   * Bumped by the parent whenever a sibling component (e.g. the
   * detail drawer's Claim/Reassign actions) wants this list to
   * refetch — common case is the drawer claiming a task that
   * originated here.
   */
  refreshKey: number;
}

export function RoleQueueClient({ onOpenTask, refreshKey }: RoleQueueClientProps) {
  const [tasks, setTasks] = useState<MyTask[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const seq = useRef(0);

  const fetchTasks = useCallback(async () => {
    const my = ++seq.current;
    setLoading(true);
    setError(null);
    try {
      const { data } = await tasksApi.listRoleQueue({ page: 1, pageSize: 50 });
      if (my !== seq.current) return;
      setTasks(data.items ?? []);
      setTotal(data.totalCount ?? 0);
    } catch (err) {
      if (my !== seq.current) return;
      if (err instanceof ApiError && err.isUnauthorized) {
        setError('Your session expired. Please sign in again.');
      } else {
        setError(err instanceof Error ? err.message : 'Could not load role queue.');
      }
      setTasks([]);
      setTotal(0);
    } finally {
      if (my === seq.current) setLoading(false);
    }
  }, []);

  useEffect(() => {
    void fetchTasks();
  }, [fetchTasks, refreshKey]);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-3 flex-wrap">
        <div>
          <h2 className="text-base font-semibold text-gray-900">Role queue</h2>
          <p className="text-sm text-gray-500">
            Open tasks waiting for any user with one of your roles.
          </p>
        </div>
        <button
          type="button"
          onClick={() => void fetchTasks()}
          disabled={loading}
          className="inline-flex items-center gap-1 px-3 py-1.5 text-xs font-medium rounded-md border border-gray-300 bg-white text-gray-700 hover:bg-gray-50 disabled:opacity-60"
        >
          <i className={`ri-refresh-line ${loading ? 'animate-spin' : ''}`} aria-hidden="true" />
          Refresh
        </button>
      </div>

      {loading && tasks.length === 0 ? (
        <SkeletonList />
      ) : error ? (
        <ErrorState message={error} onRetry={() => void fetchTasks()} />
      ) : tasks.length === 0 ? (
        <EmptyState />
      ) : (
        <>
          <p className="text-xs text-gray-500">
            Showing {tasks.length} of {total} task{total === 1 ? '' : 's'}.
          </p>
          <ul className="space-y-2">
            {tasks.map((t) => (
              <QueueTaskRow
                key={t.taskId}
                task={t}
                queueKind="role"
                onChanged={() => void fetchTasks()}
                onOpen={onOpenTask}
              />
            ))}
          </ul>
        </>
      )}
    </div>
  );
}

function SkeletonList() {
  return (
    <ul className="space-y-2" aria-label="Loading role-queue tasks">
      {Array.from({ length: 3 }).map((_, i) => (
        <li
          key={i}
          className="bg-white border border-gray-200 rounded-lg p-4 animate-pulse"
        >
          <div className="h-4 bg-gray-200 rounded w-1/3 mb-2" />
          <div className="h-3 bg-gray-100 rounded w-2/3 mb-1" />
          <div className="h-3 bg-gray-100 rounded w-1/2" />
        </li>
      ))}
    </ul>
  );
}

function EmptyState() {
  return (
    <div className="bg-white border border-dashed border-gray-300 rounded-lg p-10 text-center">
      <i className="ri-team-line text-3xl text-gray-300" aria-hidden="true" />
      <h3 className="mt-2 text-sm font-medium text-gray-900">No role-queue work</h3>
      <p className="mt-1 text-xs text-gray-500">
        No open tasks are currently waiting for your roles.
      </p>
    </div>
  );
}

function ErrorState({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div className="bg-red-50 border border-red-200 rounded-lg p-6 text-center">
      <i className="ri-error-warning-line text-2xl text-red-500" aria-hidden="true" />
      <p className="mt-2 text-sm text-red-700">{message}</p>
      <button
        type="button"
        onClick={onRetry}
        className="mt-3 px-3 py-1.5 text-xs font-medium rounded-md bg-white border border-red-300 text-red-700 hover:bg-red-100"
      >
        Try again
      </button>
    </div>
  );
}
