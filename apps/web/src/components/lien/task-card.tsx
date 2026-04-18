'use client';

import type { TaskDto } from '@/lib/liens/lien-tasks.types';
import {
  TASK_STATUS_LABELS,
  TASK_PRIORITY_COLORS,
  TASK_PRIORITY_ICONS,
} from '@/lib/liens/lien-tasks.types';

interface TaskCardProps {
  task: TaskDto;
  onComplete?: (id: string) => void;
  onCancel?: (id: string) => void;
  onClick?: (task: TaskDto) => void;
  compact?: boolean;
}

function formatDate(val?: string | null): string {
  if (!val) return '—';
  try {
    const d = new Date(val);
    if (isNaN(d.getTime())) return val;
    return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  } catch {
    return val;
  }
}

function isOverdue(dueDate?: string | null, status?: string): boolean {
  if (!dueDate || status === 'COMPLETED' || status === 'CANCELLED') return false;
  return new Date(dueDate) < new Date();
}

export function TaskCard({ task, onComplete, onCancel, onClick, compact = false }: TaskCardProps) {
  const overdue = isOverdue(task.dueDate, task.status);

  return (
    <div
      className={`bg-white border border-gray-200 rounded-lg p-3 shadow-sm hover:shadow-md transition-shadow ${onClick ? 'cursor-pointer' : ''}`}
      onClick={() => onClick?.(task)}
    >
      <div className="flex items-start justify-between gap-2">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-1.5 mb-1">
            <i className={`${TASK_PRIORITY_ICONS[task.priority]} text-sm ${TASK_PRIORITY_COLORS[task.priority]}`} />
            <span className="text-xs text-gray-400 font-medium">{task.priority}</span>
          </div>
          <p className={`font-medium text-gray-800 leading-tight ${compact ? 'text-xs' : 'text-sm'} line-clamp-2`}>
            {task.title}
          </p>
          {!compact && task.description && (
            <p className="text-xs text-gray-500 mt-1 line-clamp-2">{task.description}</p>
          )}
        </div>
        {(onComplete || onCancel) && (
          <div className="flex flex-col gap-1 shrink-0">
            {onComplete && task.status !== 'COMPLETED' && task.status !== 'CANCELLED' && (
              <button
                onClick={(e) => { e.stopPropagation(); onComplete(task.id); }}
                className="text-green-600 hover:text-green-800 p-0.5"
                title="Complete"
              >
                <i className="ri-checkbox-circle-line text-base" />
              </button>
            )}
            {onCancel && task.status !== 'COMPLETED' && task.status !== 'CANCELLED' && (
              <button
                onClick={(e) => { e.stopPropagation(); onCancel(task.id); }}
                className="text-red-400 hover:text-red-600 p-0.5"
                title="Cancel"
              >
                <i className="ri-close-circle-line text-base" />
              </button>
            )}
          </div>
        )}
      </div>

      <div className="mt-2 flex items-center gap-2 flex-wrap">
        {task.linkedLiens.length > 0 && (
          <span className="text-xs bg-purple-50 text-purple-700 rounded px-1.5 py-0.5">
            <i className="ri-stack-line mr-0.5" />{task.linkedLiens.length} lien{task.linkedLiens.length !== 1 ? 's' : ''}
          </span>
        )}
        {task.dueDate && (
          <span className={`text-xs flex items-center gap-0.5 ${overdue ? 'text-red-600 font-medium' : 'text-gray-400'}`}>
            <i className="ri-calendar-line" />{formatDate(task.dueDate)}
            {overdue && <i className="ri-error-warning-line" />}
          </span>
        )}
        {task.assignedUserId && (
          <span className="text-xs text-gray-400 flex items-center gap-0.5">
            <i className="ri-user-line" />Assigned
          </span>
        )}
      </div>

      <div className="mt-2">
        <span className={`inline-flex items-center text-xs font-medium px-2 py-0.5 rounded-full
          ${task.status === 'COMPLETED' ? 'bg-green-100 text-green-700' :
            task.status === 'CANCELLED' ? 'bg-red-100 text-red-700' :
            task.status === 'IN_PROGRESS' ? 'bg-blue-100 text-blue-700' :
            task.status === 'WAITING_BLOCKED' ? 'bg-amber-100 text-amber-700' :
            'bg-gray-100 text-gray-600'}`}>
          {TASK_STATUS_LABELS[task.status]}
        </span>
      </div>
    </div>
  );
}
