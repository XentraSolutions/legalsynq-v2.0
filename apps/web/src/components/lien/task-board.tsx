"use client";

import type { TaskDto } from "@/lib/liens/lien-tasks.types";
import type { TenantUser } from "@/types/tenant";
import { TaskCard } from "./task-card";

export interface BoardColumn {
  status: string;
  label: string;
  borderColor: string;
  items: TaskDto[];
}

interface TaskBoardProps {
  columns: BoardColumn[];
  usersById: Map<string, TenantUser>;
  onTaskClick?: (task: TaskDto) => void;
  onTaskUpdate?: (task: TaskDto) => void;
  onTaskDelete?: (task: TaskDto) => void;
  isSubmitting?: boolean;
  onNewTask?: () => void;
  canAdd?: boolean;
}

export function TaskBoard({
  columns,
  usersById,
  onTaskClick,
  onTaskUpdate,
  onTaskDelete,
  isSubmitting,
  onNewTask,
  canAdd,
}: TaskBoardProps) {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
      {columns.map((col) => (
        <div
          key={col.status}
          className={`bg-gray-50 rounded-lg border border-gray-200  flex flex-col min-h-[120px]`}
        >
          <div className="px-3 py-2 flex items-center justify-between border-b border-gray-100 shrink-0">
            <span className="text-xs font-semibold text-gray-700">
              {col.label}
            </span>
            <span className="text-[10px] font-medium text-gray-400 bg-white border border-gray-200 rounded-full px-1.5 py-0.5 leading-none tabular-nums">
              {col.items.length}
            </span>
          </div>

          {canAdd && (
            <div className="p-2 space-y-1.5 flex-1">
              <button
                onClick={onNewTask}
                className="text-sm bg-primary text-white p-2 px-4 w-full text-center cursor-pointer flex justify-center items-center gap-1 transition-colors"
              >
                <i className="ri-add-line" /> Add task
              </button>
            </div>
          )}

          <div className="flex flex-col items-center justify-center w-full py-4 px-2 gap-1">
            {col.items.map((task) => (
              <TaskCard
                key={task.id}
                task={task}
                onClick={onTaskClick}
                onUpdate={onTaskUpdate}
                onDelete={onTaskDelete}
                isSubmitting={isSubmitting}
                compact
                assigneeUser={
                  task.assignedTo
                    ? (usersById.get(task.assignedTo) ?? null)
                    : null
                }
              />
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}
