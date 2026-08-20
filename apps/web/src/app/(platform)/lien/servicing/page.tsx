"use client";

import { useState, useEffect, useCallback, useMemo } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import type { ColumnDef } from "@tanstack/react-table";
import { BaseTable } from "@/components/ui/base-table";
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
import { ApiError } from "@/lib/api-client";

export const dynamic = "force-dynamic";

function formatCurrency(amount: number | null): string {
  if (amount === null || amount === undefined) return "—";
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
  }).format(amount);
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
    pageSize: 10,
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
  const [exporting, setExporting] = useState(true);

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
          pageSize: 10,
          limit: 10,
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
    try {
      const response = await servicingService.export();

      const src = `data:text/${response.data[0]?.export_format};base64,${response.data[0]?.base64}`;
      const link = document.createElement("a");
      link.href = src;
      link.download = response.data[0]?.filename;
      link.click();
      link.remove();
    } catch (err) {
      if (err instanceof ApiError) {
        addToast({
          type: "error",
          title: "Export Failed",
          description: err?.message,
        });
      }
    } finally {
      setExporting(false);
    }
  };

  const columns = useMemo<ColumnDef<ServicingListItem, any>[]>(
    () => [
      {
        id: "caseCode",
        header: "Case number",
        meta: {
          minWidth: "180px",
        },
        cell: ({ row }) => (
          <Link
            href={`/lien/cases/${row.original.caseId}/servicing`}
            className="text-sm"
          >
            {row.original.caseCode}
          </Link>
        ),
      },
      {
        id: "name",
        header: "Plaintiff Name",
        cell: ({ row }) => (
          <span className="text-sm text-gray-700">{row.original.name}</span>
        ),
      },
      {
        id: "lawfirm",
        header: "Current Law Firm",
        cell: ({ row }) => (
          <span className="text-sm text-gray-600 max-w-xs truncate block">
            {row.original.lawfirm}
          </span>
        ),
      },
      {
        id: "currentStatus",
        header: "Current Status",
        cell: ({ row }) => (
          <span className="text-sm text-gray-500">
            {row.original.currentStatus}
          </span>
        ),
      },
      {
        id: "settlementStatus",
        header: "Settlement Status",
        cell: ({ row }) => (
          <span className="text-sm text-gray-500">
            {row.original.settlementStatus}
          </span>
        ),
      },
      {
        id: "billingAmount",
        header: "Billing Amount",
        cell: ({ row }) => (
          <span className="text-sm text-gray-500">
            {formatCurrency(row.original.billingAmount)}
          </span>
        ),
      },
      {
        id: "purchaseAmount",
        header: "Purchase Amount",
        cell: ({ row }) => (
          <span className="text-sm text-gray-500">
            {formatCurrency(row.original.purchaseAmount)}
          </span>
        ),
      },
      {
        id: "settlementAmount",
        header: "Amount Settled",
        cell: ({ row }) => (
          <span className="text-sm text-gray-500">
            {formatCurrency(row.original.settlementAmount)}
          </span>
        ),
      },
      {
        id: "settlementDate",
        header: "Settled Date",
        cell: ({ row }) => (
          <span className="text-sm text-gray-500">
            {row.original.settlementDate}
          </span>
        ),
      },
    ],
    [],
  );

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
              disabled={exporting}
            >
              {exporting ? "Exporting..." : "Export"}
            </button>
          ) : undefined
        }
      />
      <FilterToolbar searchPlaceholder="Search tasks..." onSearch={setSearch} />

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

      <BaseTable
        data={items}
        columns={columns}
        getRowId={(s) => s.id ?? s.caseId}
        isLoading={loading}
        emptyMessage="No tasks match your filters."
        getRowClassName={(s) =>
          actionLoading === (s.id ?? s.caseId)
            ? "opacity-50"
            : selection.isSelected(s.id ?? s.caseId)
              ? "bg-primary/5"
              : undefined
        }
        manualPagination
        pageCount={pagination.totalPages}
        totalCount={pagination.totalCount}
        pagination={{
          pageIndex: pagination.page - 1,
          pageSize: pagination.pageSize,
        }}
        onPaginationChange={(updater) => {
          const next =
            typeof updater === "function"
              ? updater({
                  pageIndex: pagination.page - 1,
                  pageSize: pagination.pageSize,
                })
              : updater;
          fetchData(next.pageIndex + 1);
        }}
        className="bg-white border-gray-200 rounded-xl"
      />

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
