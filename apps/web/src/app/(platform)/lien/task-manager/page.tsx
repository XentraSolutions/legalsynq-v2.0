"use client";

import { useState, useEffect, useCallback, useMemo } from "react";
import type { ColumnDef } from "@tanstack/react-table";
import { BaseTable } from "@/components/ui/base-table";
import { lienTasksService } from "@/lib/liens/lien-tasks.service";
import { apiClient } from "@/lib/api-client";
import type {
  TaskDto,
  TaskStatus,
  TaskPriority,
  TasksQuery,
} from "@/lib/liens/lien-tasks.types";
import {
  TASK_STATUS_LABELS,
  TASK_STATUS_COLORS,
  TASK_PRIORITY_COLORS,
  TASK_PRIORITY_ICONS,
  BOARD_COLUMNS,
  AVATAR_COLORS,
  PRIORITY_LABELS,
} from "@/lib/liens/lien-tasks.types";

import type { TenantUser } from "@/types/tenant";
import { useLienStore } from "@/stores/lien-store";
import { CreateEditTaskForm } from "@/components/lien/forms/create-edit-task-form";
import { TaskDetailDrawer } from "@/components/lien/task-detail-drawer";
import { TaskManagerHeader } from "@/components/lien/task-manager-header";
import { TaskManagerToolbar } from "@/components/lien/task-manager-toolbar";
import { TaskBoard } from "@/components/lien/task-board";
import { DateDisplay } from "@/components/ui/date-display";
import { KpiCard } from "@/components/lien/kpi-card";
import { lookupService } from "@/lib/lookup/lookup.service";
import { MetricCard } from "@/components/selling/dashboard/metric-card";

export const dynamic = "force-dynamic";

type ViewMode = "board" | "list";
type AssignmentScope = "all" | "me" | "others" | "unassigned";

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

export default function TaskManagerPage() {
  const addToast = useLienStore((s) => s.addToast);

  const [tasks, setTasks] = useState<TaskDto[]>([]);
  const [totals, setTotals] = useState<{
    completedTasks: number;
    inProgressTasks: number;
    inReviewTasks: number;
    totalTasks: number;
    upcomingTasks: number;
  }>();
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);

  const [error, setError] = useState<string | null>(null);
  const [viewMode, setViewMode] = useState<ViewMode>("board");
  const [usersById, setUsersById] = useState<Map<string, TenantUser>>(
    new Map(),
  );

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<TaskStatus | "">("");
  const [priorityFilter, setPriorityFilter] = useState<TaskPriority | "">("");
  const [assignmentScope, setAssignmentScope] =
    useState<AssignmentScope>("all");

  const [showCreate, setShowCreate] = useState(false);
  const [editTask, setEditTask] = useState<TaskDto | undefined>();
  const [detailTask, setDetailTask] = useState<TaskDto | null>(null);

  useEffect(() => {
    apiClient
      .get<TenantUser[]>("/identity/api/users")
      .then(({ data }) => {
        const map = new Map<string, TenantUser>();
        (data ?? []).forEach((u) => map.set(u.id, u));
        setUsersById(map);
      })
      .catch(() => {});
  }, []);

  const fetchTasks = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const query: TasksQuery = {
        search: search || undefined,
        status: statusFilter || undefined,
        priority: priorityFilter || undefined,
        assignmentScope:
          assignmentScope === "all" ? undefined : assignmentScope,
        pageSize: 200,
        page: 1,
      };
      const result = await lookupService.getTasks();
      setTasks(result.tasks);
      setTotals(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load tasks");
    } finally {
      setLoading(false);
    }
  }, [search, statusFilter, priorityFilter, assignmentScope]);

  const deleteTask = useCallback(
    async (task: TaskDto) => {
      setSubmitting(true);
      try {
        await lienTasksService.deleteTask(task.id);
        addToast({
          type: "success",
          title: "Deleted Task Successfully",
          description: "",
        });
        await fetchTasks();
      } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);

        addToast({
          type: "error",
          title: "Deleted Task Failed",
          description: msg,
        });
      } finally {
        setSubmitting(false);
      }
    },
    [submitting],
  );

  useEffect(() => {
    fetchTasks();
  }, [fetchTasks]);

  const kpis = useMemo(
    () => ({
      completedTasks: totals?.completedTasks,
      inProgressTasks: totals?.inProgressTasks,
      inReviewTasks: totals?.inReviewTasks,
      totalTasks: totals?.totalTasks,
      upcomingTasks: totals?.upcomingTasks,
    }),
    [tasks],
  );

  const boardColumns = BOARD_COLUMNS.map((status) => ({
    status,
    label: TASK_STATUS_LABELS[status],
    borderColor: TASK_STATUS_COLORS[status].border,
    items: tasks.filter((t) => t.status.toUpperCase() === status),
  }));

  const columns = useMemo<ColumnDef<TaskDto, any>[]>(
    () => [
      {
        id: "title",
        header: "Title",
        cell: ({ row }) => (
          <div className="flex items-center gap-2">
            <i
              className={`${TASK_PRIORITY_ICONS[row.original.priority]} text-xs ${TASK_PRIORITY_COLORS[row.original.priority]}`}
            />
            <span className="text-xs font-medium text-gray-800 line-clamp-1">
              {row.original.title}
            </span>
          </div>
        ),
      },
      {
        id: "status",
        header: "Status",
        cell: ({ row }) => {
          const task = row.original;
          return (
            <span
              className={`inline-flex text-[10px] font-medium px-1.5 py-0.5 rounded-full
              ${
                task.status === "COMPLETED"
                  ? "bg-green-100 text-green-700"
                  : task.status === "CANCELLED"
                    ? "bg-red-100 text-red-700"
                    : task.status === "INPROGRESS"
                      ? "bg-blue-100 text-blue-700"
                      : task.status === "INREVIEW"
                        ? "bg-amber-100 text-amber-700"
                        : "bg-gray-100 text-gray-600"
              }`}
            >
              {TASK_STATUS_LABELS[task.status]}
            </span>
          );
        },
      },
      {
        id: "priority",
        header: "Priority",
        cell: ({ row }) => (
          <span
            className={`text-[10px] font-medium ${TASK_PRIORITY_COLORS[row.original.priority]}`}
          >
            {PRIORITY_LABELS[row.original.priority] ?? row.original.priority}
          </span>
        ),
      },
      {
        id: "assignee",
        header: "Assignee",
        cell: ({ row }) => {
          const task = row.original;
          const assignee = task.assignedTo
            ? usersById.get(task.assignedTo)
            : undefined;
          return assignee ? (
            <div className="flex items-center gap-1.5">
              <div
                className={`w-5 h-5 rounded-full flex items-center justify-center text-white text-[9px] font-bold shrink-0 ${avatarColor(task.assignedTo!)}`}
              >
                {getInitials(assignee.firstName, assignee.lastName)}
              </div>
              <span className="text-xs text-gray-700 whitespace-nowrap">
                {assignee.firstName} {assignee.lastName}
              </span>
            </div>
          ) : task.assignedTo ? (
            <span className="flex items-center gap-1 text-[10px] text-gray-400">
              <i className="ri-user-line" />
              Assigned
            </span>
          ) : (
            <span className="text-gray-300 text-[10px]">&mdash;</span>
          );
        },
      },
      {
        id: "case",
        header: "Case",
        cell: ({ row }) =>
          row.original.caseId ? (
            <span className="inline-flex items-center gap-0.5 text-[10px] font-mono font-medium bg-slate-100 text-slate-600 border border-slate-200 rounded px-1.5 py-0.5">
              <i className="ri-briefcase-line text-[10px]" />
              {shortCaseId(row.original.caseId)}
            </span>
          ) : (
            <span className="text-gray-300 text-[10px]">&mdash;</span>
          ),
      },
      {
        id: "liens",
        header: "Liens",
        cell: ({ row }) =>
          row.original.linkedLiens.length > 0 ? (
            <span className="bg-purple-50 text-purple-700 text-[10px] rounded px-1.5 py-0.5">
              {row.original.linkedLiens.length}
            </span>
          ) : (
            <span className="text-gray-300 text-[10px]">&mdash;</span>
          ),
      },
      {
        id: "due",
        header: "Due",
        cell: ({ row }) => {
          const task = row.original;
          const overdue = isOverdue(task.dueDate, task.status);
          return (
            <span
              className={`text-[10px] ${overdue ? "text-red-600 font-medium" : "text-gray-400"}`}
            >
              <DateDisplay value={task.dueDate} format="date" />
              {overdue && <i className="ri-error-warning-line ml-1" />}
            </span>
          );
        },
      },
      {
        id: "updated",
        header: "Updated",
        cell: ({ row }) => (
          <span className="text-[10px] text-gray-400">
            <DateDisplay value={row.original.updatedAtUtc} format="date" />
          </span>
        ),
      },
    ],
    [usersById],
  );

  const activeFilterCount = [
    search,
    statusFilter,
    priorityFilter,
    assignmentScope !== "all" ? assignmentScope : "",
  ].filter(Boolean).length;

  function clearFilters() {
    setSearch("");
    setStatusFilter("");
    setPriorityFilter("");
    setAssignmentScope("all");
  }

  const metrics = [
    {
      label: "Total",
      value: kpis.totalTasks,
    },
    {
      label: "Upcoming",
      value: kpis.upcomingTasks,
    },
    {
      label: "In Progress",
      value: kpis.inProgressTasks,
    },
    {
      label: "In Review",
      value: kpis.inReviewTasks,
    },
    {
      label: "Completed",
      value: kpis.completedTasks,
    },
  ];

  return (
    <div className="space-y-3">
      <div className="grid grid-cols-2 md:grid-cols-5 w-full gap-4">
        {metrics.map((m) => (
          <div
            key={m.label}
            className="border border-gray-200 rounded-xl px-4 py-2 hover:shadow-sm break-words"
          >
            <p className="text-xs text-gray-500">{m.label}</p>
            <p className="text-lg font-semibold text-right py-3">{m.value}</p>
          </div>
        ))}
      </div>
      {/* <MetricCard
          label="Total"
          value={kpis.totalTasks ?? 0}
          formatAsCurrency={false}
        />

        <MetricCard
          label="Upcoming"
          value={kpis.upcomingTasks ?? 0}
          formatAsCurrency={false}
        />

        <MetricCard
          label="In Progress"
          value={kpis.inProgressTasks ?? 0}
          formatAsCurrency={false}
        />

        <MetricCard
          label="In Review"
          value={kpis.inReviewTasks ?? 0}
          formatAsCurrency={false}
        />

        <MetricCard
          label="Completed"
          value={kpis.completedTasks ?? 0}
          formatAsCurrency={false}
        />
       */}

      {/* Row 4 — Board / List */}
      {loading ? (
        <div className="flex items-center justify-center py-8 gap-2 text-gray-400">
          <i className="ri-loader-4-line animate-spin text-lg" />
          <span className="text-xs">Loading tasks...</span>
        </div>
      ) : viewMode === "board" ? (
        <>
          <div className="cursor-pointer">
            <TaskBoard
              columns={boardColumns}
              usersById={usersById}
              onTaskUpdate={setEditTask}
              isSubmitting={submitting}
              onTaskDelete={deleteTask}
              onNewTask={() => setShowCreate(true)}
            />
          </div>
          <CreateEditTaskForm
            open={showCreate}
            onClose={() => setShowCreate(false)}
            onSaved={() => {
              fetchTasks();
              setShowCreate(false);
            }}
          />

          {editTask && (
            <CreateEditTaskForm
              open
              onClose={() => setEditTask(undefined)}
              onSaved={() => {
                fetchTasks();
                setEditTask(undefined);
              }}
              editTask={editTask}
            />
          )}
        </>
      ) : (
        <></>
      )}
    </div>
  );
}
