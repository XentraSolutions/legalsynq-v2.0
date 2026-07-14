"use client";

export const dynamic = "force-dynamic";

import { useState, useEffect, useCallback, useMemo } from "react";
import Link from "next/link";
import type { ColumnDef, OnChangeFn, RowSelectionState } from "@tanstack/react-table";
import { BaseTable } from "@/components/ui/base-table";
import { PageHeader } from "@/components/lien/page-header";
import { FilterToolbar } from "@/components/lien/filter-toolbar";
import { StatusBadge } from "@/components/lien/status-badge";
import { SideDrawer } from "@/components/lien/side-drawer";
import { CreateLienModal } from "@/components/lien/forms/create-lien-modal";
import { BulkActionBar } from "@/components/lien/bulk-action-bar";
import { BulkConfirmModal } from "@/components/lien/bulk-confirm-modal";
import { BulkResultBanner } from "@/components/lien/bulk-result-banner";
import { useLienStore } from "@/stores/lien-store";
import { useRoleAccess } from "@/hooks/use-role-access";
import { useSelectionState } from "@/hooks/use-selection-state";
import { ApiError } from "@/lib/api-client";
import {
  liensService,
  type LienListItem,
  type LiensQuery,
  type PaginationMeta,
} from "@/lib/liens";
import { useProviderMode } from "@/hooks/use-provider-mode";
import {
  executeBulk,
  type BulkActionConfig,
  type BulkOperationResult,
} from "@/lib/bulk-operations";
import { LiensFilter } from "./components/liens-filter";

function formatCurrency(amount: number | null): string {
  if (amount === null || amount === undefined) return "\u2014";
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
  }).format(amount);
}

const BULK_ACTIONS: BulkActionConfig[] = [
  {
    key: "withdraw",
    label: "Withdraw",
    icon: "ri-close-circle-line",
    variant: "danger",
    confirmTitle: "Withdraw Liens",
    confirmDescription: (count) =>
      `This will withdraw ${count} lien${count !== 1 ? "s" : ""} from the marketplace. Only liens in "Active" or "Offered" status will be affected.`,
  },
];

export default function LiensPage() {
  const { isSellMode } = useProviderMode();
  const ra = useRoleAccess();
  const addToast = useLienStore((s) => s.addToast);
  const selection = useSelectionState();

  const [liens, setLiens] = useState<LienListItem[]>([]);
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
  const [typeFilter, setTypeFilter] = useState("");
  const [showCreate, setShowCreate] = useState(false);
  const [previewId, setPreviewId] = useState<string | null>(null);

  const [actionOpen, setActionOpen] = useState(false);
  const [showFilter, setShowFilter] = useState(false);

  const [bulkAction, setBulkAction] = useState<BulkActionConfig | null>(null);
  const [bulkLoading, setBulkLoading] = useState(false);
  const [bulkResult, setBulkResult] = useState<BulkOperationResult | null>(
    null,
  );

  const currentQuery = useCallback(
    (): LiensQuery => ({
      search: search || undefined,
      status: statusFilter || undefined,
      lienType: typeFilter || undefined,
      page: 1,
      pageSize: 10,
    }),
    [search, statusFilter, typeFilter],
  );

  const fetchLiens = useCallback(async (query: LiensQuery = {}) => {
    setLoading(true);
    setError(null);
    try {
      const result = await liensService.getLiens(query);
      setLiens(result.items);
      setPagination(result.pagination);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Failed to load liens");
      }
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchLiens(currentQuery());
  }, [search, statusFilter, typeFilter, fetchLiens, currentQuery]);

  const handlePageChange = (newPage: number) => {
    fetchLiens({
      ...currentQuery(),
      page: newPage,
      pageSize: pagination.pageSize,
    });
  };

  const previewLien = previewId ? liens.find((l) => l.id === previewId) : null;

  const handleCreated = () => {
    setShowCreate(false);
    fetchLiens(currentQuery());
    addToast({
      type: "success",
      title: "Lien Created",
      description: "New lien has been created successfully",
    });
  };

  const handleCasesFilter = () => {
    setShowFilter(false);
    fetchLiens(currentQuery());
  };

  const canEdit = ra.can("lien:edit");

  const handleBulkAction = (actionKey: string) => {
    const action = BULK_ACTIONS.find((a) => a.key === actionKey);
    if (action) setBulkAction(action);
  };

  const executeBulkAction = async () => {
    if (!bulkAction) return;
    setBulkLoading(true);
    const result = await executeBulk(selection.ids, async (id) => {
      const lien = liens.find((l) => l.id === id);
      if (!lien) throw new Error("Lien not found in current list");
      if (lien.status !== "Active" && lien.status !== "Offered") {
        throw new Error(
          `Lien is "${lien.status}" — only Active or Offered liens can be withdrawn`,
        );
      }
      await liensService.withdraw(id);
    });
    setBulkLoading(false);
    setBulkAction(null);
    setBulkResult(result);
    selection.clear();
    fetchLiens(currentQuery());
  };

  const allIds = liens.map((l) => l.id);

  const rowSelection = useMemo<RowSelectionState>(() => {
    const obj: RowSelectionState = {};
    selection.selectedIds.forEach((id) => {
      obj[id] = true;
    });
    return obj;
  }, [selection.selectedIds]);

  const handleRowSelectionChange: OnChangeFn<RowSelectionState> = (updater) => {
    const next = typeof updater === "function" ? updater(rowSelection) : updater;
    const nextIds = new Set(Object.keys(next).filter((id) => next[id]));
    const prevIds = selection.selectedIds;

    if (nextIds.size === prevIds.size) return;

    const allNowSelected = nextIds.size === liens.length && prevIds.size !== liens.length;
    const allNowDeselected = nextIds.size === 0 && prevIds.size === liens.length;
    if (allNowSelected || allNowDeselected) {
      selection.toggleAll(allIds);
      return;
    }

    const toggled = liens.find((l) => prevIds.has(l.id) !== nextIds.has(l.id));
    if (toggled) selection.toggle(toggled.id);
  };

  const columns = useMemo<ColumnDef<LienListItem, any>[]>(
    () => [
      {
        id: "lienNumber",
        header: "Lien #",
        cell: ({ row }) => (
          <Link
            href={`/lien/liens/${row.original.id}`}
            onClick={(e) => e.stopPropagation()}
            className="text-xs font-mono text-primary hover:underline"
          >
            {row.original.lienNumber}
          </Link>
        ),
      },
      {
        id: "lienTypeLabel",
        header: "Type",
        cell: ({ row }) => (
          <span className="text-sm text-gray-700">{row.original.lienTypeLabel}</span>
        ),
      },
      {
        id: "subjectName",
        header: "Subject",
        cell: ({ row }) =>
          row.original.isConfidential ? (
            <span className="italic text-gray-400 text-sm">Confidential</span>
          ) : (
            <span className="text-sm text-gray-700">{row.original.subjectName || "—"}</span>
          ),
      },
      {
        id: "originalAmount",
        header: "Original",
        cell: ({ row }) => (
          <span className="text-sm text-gray-700 tabular-nums">
            {formatCurrency(row.original.originalAmount)}
          </span>
        ),
      },
      ...(isSellMode
        ? [
            {
              id: "offerPrice",
              header: "Offer",
              cell: ({ row }: { row: { original: LienListItem } }) => (
                <span className="text-sm text-gray-700 tabular-nums">
                  {formatCurrency(row.original.offerPrice)}
                </span>
              ),
            } as ColumnDef<LienListItem, any>,
          ]
        : []),
      {
        id: "status",
        header: "Status",
        cell: ({ row }) => <StatusBadge status={row.original.status} />,
      },
      {
        id: "createdAt",
        header: "Created",
        cell: ({ row }) => (
          <span className="text-xs text-gray-400 whitespace-nowrap">{row.original.createdAt}</span>
        ),
      },
    ],
    [isSellMode],
  );

  return (
    <div className="space-y-5">
      <PageHeader
        title="Liens"
        subtitle={loading ? "Loading..." : `${pagination.totalCount} liens`}
        actions={
          <div className="relative">
            {/* Dropdown Button */}
            <button
              onClick={() => setActionOpen(!actionOpen)}
              className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors"
            >
              Actions
              <i className="ri-arrow-down-s-line text-base" />
            </button>
            {/* Dropdown Menu */}
            {actionOpen && (
              <div className="absolute right-0 mt-2 w-48 bg-white border border-gray-200 rounded-lg shadow-lg z-50">
                {/* Create Lien */}
                {ra.can("lien:create") && (
                  <button
                    onClick={() => {
                      setShowCreate(true);
                      setActionOpen(false);
                    }}
                    className="w-full text-left px-4 py-2 text-sm hover:bg-gray-100"
                  >
                    New Lien
                  </button>
                )}
                {/* Filter */}
                <button
                  onClick={() => {
                    setShowFilter(true);
                    setActionOpen(false);
                  }}
                  className="w-full text-left px-4 py-2 text-sm hover:bg-gray-100"
                >
                  Filter
                </button>

                {/* Export CSV */}
                <button
                  onClick={() => {
                    setActionOpen(false);
                  }}
                  className="w-full text-left px-4 py-2 text-sm hover:bg-gray-100"
                >
                  Export
                </button>
              </div>
            )}
          </div>
        }
      />

      <FilterToolbar
        searchPlaceholder="Search liens by number or subject..."
        onSearch={setSearch}
        filters={[
          {
            label: "All Statuses",
            value: statusFilter,
            onChange: setStatusFilter,
            options: [
              { value: "Draft", label: "Draft" },
              { value: "Active", label: "Active" },
              ...(isSellMode
                ? [
                    { value: "Offered", label: "Offered" },
                    { value: "Sold", label: "Sold" },
                  ]
                : []),
              { value: "Withdrawn", label: "Withdrawn" },
            ],
          },
          {
            label: "All Types",
            value: typeFilter,
            onChange: setTypeFilter,
            options: [
              { value: "MedicalLien", label: "Medical Lien" },
              { value: "AttorneyLien", label: "Attorney Lien" },
              { value: "SettlementAdvance", label: "Settlement Advance" },
              { value: "WorkersCompLien", label: "Workers' Comp Lien" },
              { value: "PropertyLien", label: "Property Lien" },
              { value: "Other", label: "Other" },
            ],
          },
        ]}
      />

      <BulkResultBanner
        result={bulkResult}
        onDismiss={() => setBulkResult(null)}
        entityLabel="liens"
      />

      {error && (
        <div className="flex items-center gap-2 p-3 bg-red-50 border border-red-200 rounded-lg">
          <i className="ri-error-warning-line text-red-600" />
          <p className="text-sm text-red-700">{error}</p>
          <button
            onClick={() => fetchLiens(currentQuery())}
            className="ml-auto text-sm text-red-600 hover:underline"
          >
            Retry
          </button>
        </div>
      )}

      {loading ? (
        <div className="p-10 text-center">
          <div className="inline-block h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
          <p className="text-sm text-gray-400 mt-2">Loading liens...</p>
        </div>
      ) : (
        <BaseTable
          data={liens}
          columns={columns}
          getRowId={(l) => l.id}
          emptyMessage="No liens match your filters."
          onRowClick={(l) => setPreviewId(l.id)}
          rowSelection={canEdit ? rowSelection : undefined}
          onRowSelectionChange={handleRowSelectionChange}
          getRowClassName={(l) => (selection.isSelected(l.id) ? "bg-primary/5" : undefined)}
          manualPagination
          pageCount={pagination.totalPages}
          totalCount={pagination.totalCount}
          pagination={{ pageIndex: pagination.page - 1, pageSize: pagination.pageSize }}
          onPaginationChange={(updater) => {
            const next =
              typeof updater === "function"
                ? updater({ pageIndex: pagination.page - 1, pageSize: pagination.pageSize })
                : updater;
            handlePageChange(next.pageIndex + 1);
          }}
          className="bg-white border-gray-200 rounded-xl"
        />
      )}

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

      <CreateLienModal
        open={showCreate}
        onClose={() => setShowCreate(false)}
        onCreated={handleCreated}
      />
      <LiensFilter
        open={showFilter}
        onClose={() => setShowFilter(false)}
        onApplyFilter={handleCasesFilter}
      />

      <SideDrawer
        open={!!previewLien}
        onClose={() => setPreviewId(null)}
        title={previewLien?.lienNumber || ""}
        subtitle={previewLien?.lienTypeLabel}
      >
        {previewLien && (
          <div className="space-y-4">
            <StatusBadge status={previewLien.status} size="md" />
            <div className="grid grid-cols-2 gap-3 text-sm">
              <div>
                <p className="text-xs text-gray-400">Original</p>
                <p className="font-medium text-gray-700">
                  {formatCurrency(previewLien.originalAmount)}
                </p>
              </div>
              {isSellMode && (
                <div>
                  <p className="text-xs text-gray-400">Offer Price</p>
                  <p className="font-medium text-blue-600">
                    {formatCurrency(previewLien.offerPrice)}
                  </p>
                </div>
              )}
              <div>
                <p className="text-xs text-gray-400">Jurisdiction</p>
                <p className="text-gray-700">
                  {previewLien.jurisdiction || "\u2014"}
                </p>
              </div>
              <div>
                <p className="text-xs text-gray-400">Case</p>
                <p className="text-gray-700">
                  {previewLien.caseId ? (
                    <Link
                      href={`/lien/cases/${previewLien.caseId}`}
                      className="text-primary hover:underline"
                    >
                      View Case
                    </Link>
                  ) : (
                    "\u2014"
                  )}
                </p>
              </div>
            </div>
            <Link
              href={`/lien/liens/${previewLien.id}`}
              className="block text-center text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90"
            >
              View Full Details
            </Link>
          </div>
        )}
      </SideDrawer>
    </div>
  );
}
