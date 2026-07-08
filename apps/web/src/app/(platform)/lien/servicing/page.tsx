"use client";

import { useState, useEffect, useCallback } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { PageHeader } from "@/components/lien/page-header";
import { FilterToolbar } from "@/components/lien/filter-toolbar";
import { StatusBadge, PriorityBadge } from "@/components/lien/status-badge";
import { ActionMenu } from "@/components/lien/action-menu";
import { AssignTaskForm } from "@/components/lien/forms/assign-task-form";
import { ConfirmDialog } from "@/components/lien/modal";
import { BulkActionBar } from "@/components/lien/bulk-action-bar";
import { BulkConfirmModal } from "@/components/lien/bulk-confirm-modal";
import { BulkResultBanner } from "@/components/lien/bulk-result-banner";
import { useLienStore } from "@/stores/lien-store";
import { useRoleAccess } from "@/hooks/use-role-access";
import { useSelectionState } from "@/hooks/use-selection-state";
import { useTimezone } from "@/lib/use-timezone";
import { servicingService } from "@/lib/servicing";
import type { ServicingListItem, PaginationMeta } from "@/lib/servicing";
import {
  executeBulk,
  type BulkActionConfig,
  type BulkOperationResult,
} from "@/lib/bulk-operations";

export const dynamic = "force-dynamic";

function formatDate(val: string, timezone: string): string {
  if (!val) return "\u2014";
  try {
    const d = new Date(val);
    if (isNaN(d.getTime())) return val;
    return d.toLocaleDateString("en-US", {
      month: "short",
      day: "numeric",
      year: "numeric",
      timeZone: timezone,
    });
  } catch {
    return val;
  }
}

const BULK_ACTIONS: BulkActionConfig[] = [
  {
    key: "complete",
    label: "Mark Complete",
    icon: "ri-checkbox-circle-line",
    variant: "primary",
    confirmTitle: "Complete Tasks",
    confirmDescription: (count) =>
      `This will mark ${count} task${count !== 1 ? "s" : ""} as completed. Tasks already completed will be skipped.`,
  },
];

export default function ServicingPage() {
  const router = useRouter();
  const addToast = useLienStore((s) => s.addToast);
  const ra = useRoleAccess();
  const selection = useSelectionState();
  const timezone = useTimezone();

  const [items, setItems] = useState<ServicingListItem[]>([]);
  const [pagination, setPagination] = useState<PaginationMeta>({
    page: 1,
    pageSize: 20,
    totalCount: 0,
    totalPages: 0,
  });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [priorityFilter, setPriorityFilter] = useState("");
  const [showCreate, setShowCreate] = useState(false);
  const [confirmAction, setConfirmAction] = useState<{
    id: string;
    status: string;
    label: string;
  } | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);

  const [bulkAction, setBulkAction] = useState<BulkActionConfig | null>(null);
  const [bulkLoading, setBulkLoading] = useState(false);
  const [bulkResult, setBulkResult] = useState<BulkOperationResult | null>(
    null,
  );

  const fetchData = useCallback(
    async (page = 1) => {
      setLoading(true);
      setError(null);
      try {
        const result = await servicingService.getItems({
          search: search || undefined,
          status: statusFilter || undefined,
          priority: priorityFilter || undefined,
          page,
          pageSize: 20,
        });
        setItems(result.items);
        setPagination(result.pagination);
      } catch (err) {
        setError(
          err instanceof Error ? err.message : "Failed to load servicing tasks",
        );
      } finally {
        setLoading(false);
      }
    },
    [search, statusFilter, priorityFilter],
  );

  useEffect(() => {
    fetchData(1);
  }, [fetchData]);

  const canEdit = ra.can("servicing:edit");

  async function handleStatusUpdate(id: string, status: string) {
    setActionLoading(id);
    try {
      await servicingService.updateStatus(id, status);
      addToast({
        type: "success",
        title: `Task ${status === "Completed" ? "Completed" : status === "InProgress" ? "Started" : status}`,
      });
      await fetchData(pagination.page);
    } catch (err) {
      addToast({
        type: "error",
        title: "Action Failed",
        description: err instanceof Error ? err.message : "Unknown error",
      });
    } finally {
      setActionLoading(null);
    }
  }

  async function handleEscalate(id: string) {
    setActionLoading(id);
    try {
      await servicingService.updateStatus(id, "Escalated");
      addToast({ type: "warning", title: "Task Escalated" });
      await fetchData(pagination.page);
    } catch (err) {
      addToast({
        type: "error",
        title: "Escalation Failed",
        description: err instanceof Error ? err.message : "Unknown error",
      });
    } finally {
      setActionLoading(null);
    }
  }

  const handleBulkAction = (actionKey: string) => {
    const action = BULK_ACTIONS.find((a) => a.key === actionKey);
    if (action) setBulkAction(action);
  };

  const executeBulkAction = async () => {
    if (!bulkAction) return;
    setBulkLoading(true);
    const result = await executeBulk(selection.ids, async (id) => {
      const item = items.find((s) => s.id === id);
      if (!item) throw new Error("Task not found in current list");
      if (item.currentStatus === "Completed")
        throw new Error("Task is already completed");
      await servicingService.updateStatus(id, "Completed");
    });
    setBulkLoading(false);
    setBulkAction(null);
    setBulkResult(result);
    selection.clear();
    fetchData(pagination.page);
  };

  const exportServicing = async () => {
    const response = await servicingService.export();

    const src = `data:text/${response.data[0]?.export_format};base64,${response.data[0]?.base64}`;
    const link = document.createElement("a");
    link.href = src;
    link.download = response.data[0]?.filename;
    link.click();
    link.remove();
  };

  const tableHeaders = [
    "Case number",
    "Plaintiff Name",
    "Current Law Firm",
    "Current Status",
    "Settlement Status",
    "Billing Amount",
    "Purchase Amount",
    "Amount Settled",
    "Settled Date",
  ];

  return (
    <div className="space-y-5">
      <PageHeader
        title="Servicing"
        subtitle={`${pagination.totalCount} tasks`}
        actions={
          ra.can("servicing:assign") ? (
            <button
              onClick={() => exportServicing()}
              className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors"
            >
              Export
            </button>
          ) : undefined
        }
      />
      <FilterToolbar
        searchPlaceholder="Search tasks..."
        onSearch={setSearch}
        filters={[
          {
            label: "All Statuses",
            value: statusFilter,
            onChange: setStatusFilter,
            options: [
              { value: "Pending", label: "Pending" },
              { value: "InProgress", label: "In Progress" },
              { value: "Completed", label: "Completed" },
              { value: "Escalated", label: "Escalated" },
              { value: "OnHold", label: "On Hold" },
            ],
          },
          {
            label: "All Priorities",
            value: priorityFilter,
            onChange: setPriorityFilter,
            options: [
              { value: "Low", label: "Low" },
              { value: "Normal", label: "Normal" },
              { value: "High", label: "High" },
              { value: "Urgent", label: "Urgent" },
            ],
          },
        ]}
      />

      <BulkResultBanner
        result={bulkResult}
        onDismiss={() => setBulkResult(null)}
        entityLabel="tasks"
      />

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-4 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <i className="ri-error-warning-line text-red-500" />
            <span className="text-sm text-red-700">{error}</span>
          </div>
          <button
            onClick={() => fetchData(pagination.page)}
            className="text-sm text-red-600 hover:text-red-800 font-medium"
          >
            Retry
          </button>
        </div>
      )}

      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        {loading ? (
          <div className="p-10 text-center">
            <i className="ri-loader-4-line animate-spin text-2xl text-gray-400" />
            <p className="text-sm text-gray-400 mt-2">Loading tasks...</p>
          </div>
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-100">
                <thead>
                  <tr className="bg-gray-50">
                    {tableHeaders.map((h) => (
                      <th
                        key={h}
                        className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide"
                      >
                        {h}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {items.map((s) => (
                    <tr
                      key={s.caseId}
                      className={`hover:bg-gray-50 transition-colors ${actionLoading === (s.id ?? s.caseId) ? "opacity-50" : ""} ${selection.isSelected(s.id ?? s.caseId) ? "bg-primary/5" : ""}`}
                    >
                      <td className="px-4 py-3">
                        <Link
                          href={`/lien/cases/${s.caseId}?=servicing`}
                          className="text-xs font-mono text-primary hover:underline"
                        >
                          {s.caseCode}
                        </Link>
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-700">
                        {s.name}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-600 max-w-xs truncate">
                        {s.lawfirm}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-500">
                        {s.currentStatus}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-500">
                        {s.settlementStatus}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-500">
                        {s.billingAmount}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-500">
                        {s.purchaseAmount}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-500">
                        {s.settlementAmount}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-500">
                        {s.settlementDate}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {items.length === 0 && !loading && (
              <div className="p-10 text-center text-sm text-gray-400">
                No tasks match your filters.
              </div>
            )}

            {pagination.totalPages > 1 && (
              <div className="flex items-center justify-between px-4 py-3 border-t border-gray-100">
                <span className="text-xs text-gray-500">
                  Page {pagination.page} of {pagination.totalPages} (
                  {pagination.totalCount} total)
                </span>
                <div className="flex gap-1">
                  <button
                    disabled={pagination.page <= 1}
                    onClick={() => fetchData(pagination.page - 1)}
                    className="px-3 py-1 text-xs border border-gray-200 rounded-lg hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed"
                  >
                    Prev
                  </button>
                  <button
                    disabled={pagination.page >= pagination.totalPages}
                    onClick={() => fetchData(pagination.page + 1)}
                    className="px-3 py-1 text-xs border border-gray-200 rounded-lg hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed"
                  >
                    Next
                  </button>
                </div>
              </div>
            )}
          </>
        )}
      </div>

      {canEdit && (
        <BulkActionBar
          count={selection.count}
          actions={BULK_ACTIONS}
          onAction={handleBulkAction}
          onClear={selection.clear}
        />
      )}

      {bulkAction && (
        <BulkConfirmModal
          open
          onClose={() => setBulkAction(null)}
          onConfirm={executeBulkAction}
          title={bulkAction.confirmTitle}
          description={bulkAction.confirmDescription(selection.count)}
          count={selection.count}
          variant={bulkAction.variant}
          loading={bulkLoading}
        />
      )}

      <AssignTaskForm
        open={showCreate}
        onClose={() => setShowCreate(false)}
        onCreated={() => fetchData(1)}
      />

      {confirmAction && (
        <ConfirmDialog
          open
          onClose={() => setConfirmAction(null)}
          onConfirm={async () => {
            await handleStatusUpdate(confirmAction.id, confirmAction.status);
            setConfirmAction(null);
          }}
          title={confirmAction.label}
          description={`Mark this task as ${confirmAction.status.toLowerCase()}?`}
          confirmLabel={confirmAction.label}
        />
      )}
    </div>
  );
}
