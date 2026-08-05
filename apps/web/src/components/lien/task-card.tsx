"use client";

import type { TaskDto } from "@/lib/liens/lien-tasks.types";
import {
  TASK_STATUS_LABELS,
  TASK_PRIORITY_COLORS,
  TASK_PRIORITY_ICONS,
} from "@/lib/liens/lien-tasks.types";
import { DateDisplay } from "@/components/ui/date-display";
import { useEffect, useRef, useState } from "react";
import { ConfirmDialog } from "./modal";

interface AssigneeInfo {
  firstName: string;
  lastName: string;
  email?: string;
}

interface TaskCardProps {
  task: TaskDto;
  onClick?: (task: TaskDto) => void;
  onUpdate?: (task: TaskDto) => void;
  onDelete?: (task: TaskDto) => Promise<void> | void;
  compact?: boolean;
  assigneeUser?: AssigneeInfo | null;
  isSubmitting?: boolean;
}

const PRIORITY_LABELS: Record<string, string> = {
  LOW: "Low",
  MEDIUM: "Medium",
  HIGH: "High",
  URGENT: "Urgent",
};

const PRIORITY_BG: Record<string, string> = {
  LOW: "bg-gray-100 text-gray-600",
  MEDIUM: "bg-orange-50 text-orange-600",
  HIGH: "bg-red-50 text-red-500",
  URGENT: "bg-red-50 text-red-600",
};

const AVATAR_COLORS = [
  "bg-violet-500",
  "bg-blue-500",
  "bg-teal-500",
  "bg-indigo-500",
  "bg-pink-500",
  "bg-amber-500",
];

function avatarColor(id: string): string {
  let hash = 0;
  for (let i = 0; i < id.length; i++)
    hash = (hash * 31 + id.charCodeAt(i)) >>> 0;
  return AVATAR_COLORS[hash % AVATAR_COLORS.length];
}

function getInitials(first: string, last: string): string {
  return `${first.charAt(0)}${last.charAt(0)}`.toUpperCase();
}

function isOverdue(dueDate?: string | null, status?: string): boolean {
  if (!dueDate || status === "COMPLETED" || status === "CANCELLED")
    return false;
  return new Date(dueDate) < new Date();
}

function shortCaseId(caseId: string): string {
  return caseId.length > 8
    ? caseId.slice(0, 8).toUpperCase()
    : caseId.toUpperCase();
}

export function TaskCard({
  task,
  onClick,
  onUpdate,
  onDelete,
  compact = false,
  assigneeUser,
  isSubmitting,
}: TaskCardProps) {
  const overdue = isOverdue(task.dueDate, task.status);
  const [isOpen, setIsOpen] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);

  const dropdownRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const handleClickOutside = (event: any) => {
      if (
        dropdownRef.current &&
        !dropdownRef?.current?.contains(event.target)
      ) {
        setIsOpen(false);
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const handleUpdate = (task: TaskDto) => {
    onUpdate?.(task);
    setIsOpen(false);
  };

  const handleDelete = async (task: TaskDto) => {
    if (!onDelete) {
      setIsOpen(false);
      setConfirmDelete(false);
      return;
    }

    try {
      await Promise.resolve(onDelete(task));
      setIsOpen(false);
      setConfirmDelete(false);
    } catch (err) {
      // If deletion fails, keep the confirm dialog open so the UI
      // can reflect the error/loading state (parent controls `isSubmitting`).
    }
  };
  return (
    <div
      className={`bg-white border border-gray-200 rounded-lg p-4 shadow-sm hover:shadow-md transition-shadow w-full`}
      // onClick={() => onClick?.(task)}
    >
      {/* Priority + Title */}
      <div className="flex items-start gap-2 mb-1.5">
        <div className="flex-1 min-w-0">
          <div className="inline-flex items-center justify-between w-full">
            <p
              className={`font-medium text-gray-800 leading-tight ${compact ? "text-xs" : "text-sm"} line-clamp-2`}
            >
              {task.title}
            </p>
            <div
              className="custom-dropdown"
              ref={dropdownRef}
              style={{ position: "relative", display: "inline-block" }}
            >
              {/* Dropdown Menu */}
              <div
                className="relative inline-block text-left"
                ref={dropdownRef}
              >
                {/* Toggle Button */}
                <button
                  type="button"
                  className="inline-flex cursor-pointer items-center justify-center p-2 text-gray-500 hover:text-gray-700 focus:outline-none"
                  onClick={() => setIsOpen(!isOpen)}
                  aria-expanded={isOpen}
                >
                  <i className="ri-more-2-fill fs-18"></i>
                </button>

                {/* Dropdown Menu */}
                {isOpen && (
                  <div className="absolute right-0 z-10 mt-2 w-36 origin-top-right rounded-md bg-white shadow-lg focus:outline-none">
                    <div className="py-1" role="none">
                      <button
                        type="button"
                        className="flex w-full items-center gap-2 px-4 py-2 text-sm text-gray-700 hover:bg-gray-100 cursor-pointer"
                        onClick={() => handleUpdate(task)}
                      >
                        View Details
                      </button>
                      <button
                        type="button"
                        className="flex w-full items-center gap-2 px-4 py-2 text-sm text-red-600 hover:bg-gray-100 cursor-pointer"
                        onClick={() => setConfirmDelete(true)}
                      >
                        Delete
                      </button>
                    </div>
                  </div>
                )}
              </div>
            </div>
          </div>

          {/* Priority pill */}
          <span
            className={`inline-flex items-center gap-1 text-[10px] font-semibold px-1.5 py-0.5 rounded mb-1 ${PRIORITY_BG[task.priority.toUpperCase()]}`}
          >
            <i
              className={`${TASK_PRIORITY_ICONS[task.priority]} text-[10px] ${TASK_PRIORITY_COLORS[task.priority]}`}
            />
            {PRIORITY_LABELS[task.priority] ?? task.priority}
          </span>
          {task.description && (
            <p className="text-xs mt-1 mb-2 line-clamp-2">{task.description}</p>
          )}
        </div>
      </div>

      {/* Meta row: case ID, liens, due date */}
      <div className="flex items-center gap-1.5 flex-wrap mb-1.5">
        {task?.linkedLiens?.length > 0 && (
          <span className="text-[10px] bg-purple-50 text-purple-700 rounded px-1.5 py-0.5">
            <i className="ri-stack-line mr-0.5" />
            {task.linkedLiens.length} lien
            {task.linkedLiens.length !== 1 ? "s" : ""}
          </span>
        )}
        {task.isSystemGenerated && (
          <span className="text-[10px] bg-violet-50 text-violet-700 border border-violet-200 rounded px-1.5 py-0.5 flex items-center gap-0.5">
            <i className="ri-robot-line" />
            Auto
          </span>
        )}
        {task.dueDate && (
          <span
            className={`text-[10px] flex items-center gap-0.5 ${overdue ? "text-red-600 font-medium" : "text-gray-400"}`}
          >
            <i className="ri-calendar-line" />
            <DateDisplay value={task.dueDate} format="date" />
            {overdue && <i className="ri-error-warning-line" />}
          </span>
        )}
      </div>

      {/* Footer: assignee avatar + name on left, status badge on right */}
      <div className="flex items-center justify-between w-full gap-2 pt-1 border-t border-gray-100">
        {/* Assignee */}
        {task.caseId && (
          <span className="inline-flex items-center gap-0.5 text-[10px] font-mono font-medium bg-slate-100 text-slate-600 border border-slate-200 rounded px-1.5 py-0.5">
            <i className="ri-briefcase-line text-[10px]" />
            {task.caseCode}
          </span>
        )}
        {assigneeUser ? (
          <div className="flex items-center gap-1.5 min-w-0">
            <div
              className={`w-5 h-5 rounded-full flex items-center justify-center text-white text-[9px] font-bold shrink-0 ${avatarColor(task.assignedTo ?? assigneeUser.email ?? "u")}`}
            >
              {getInitials(assigneeUser.firstName, assigneeUser.lastName)}
            </div>
            <span className="text-[11px] text-gray-600 truncate font-medium">
              {assigneeUser.firstName} {assigneeUser.lastName}
            </span>
          </div>
        ) : task.assignedTo ? (
          <div className="flex items-center gap-1.5">
            <div
              className={`w-5 h-5 rounded-full flex items-center justify-center text-white text-[9px] font-bold shrink-0 ${avatarColor(task.assignedTo)}`}
            >
              <i className="ri-user-line text-[9px]" />
            </div>
            <span className="text-[11px] text-gray-400">Assigned</span>
          </div>
        ) : (
          <span className="text-[11px] text-gray-300 flex items-center gap-0.5">
            <i className="ri-user-line text-[10px]" />
            Unassigned
          </span>
        )}
      </div>

      {confirmDelete && (
        <ConfirmDialog
          open
          onClose={() => setConfirmDelete(false)}
          onConfirm={() => handleDelete(task)}
          title="Delete Contact"
          description={`Are you sure you want to delete ${task.title}? This action cannot be undone.`}
          confirmLabel="Delete"
          confirmVariant="danger"
          loading={isSubmitting}
        />
      )}
    </div>
  );
}
