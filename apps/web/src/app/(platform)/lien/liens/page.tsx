"use client";

export const dynamic = "force-dynamic";

import { useState, useEffect, useCallback, useMemo } from "react";
import { useRouter } from "next/navigation";
import type { ColumnDef } from "@tanstack/react-table";
import { BaseTable } from "@/components/ui/base-table";
import { PageHeader } from "@/components/lien/page-header";
import { FilterToolbar } from "@/components/lien/filter-toolbar";
import { StatusBadge } from "@/components/lien/status-badge";
import { CreateLienModal } from "@/components/lien/forms/create-lien-modal";
import { useLienStore } from "@/stores/lien-store";
import { useRoleAccess } from "@/hooks/use-role-access";
import { ApiError } from "@/lib/api-client";
import {
  liensService,
  type LienListItem,
  type LiensQuery,
  type PaginationMeta,
} from "@/lib/liens";
import { useProviderMode } from "@/hooks/use-provider-mode";
import { LiensFilter } from "./components/liens-filter";

function formatCurrency(amount: number | null): string {
  if (amount === null || amount === undefined) return "—";
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
  }).format(amount);
}

function lienDetailHref(lien: LienListItem): string {
  return lien.caseId
    ? `/lien/cases/${lien.caseId}/liens/${lien.id}`
    : `/lien/liens/${lien.id}`;
}

export default function LiensPage() {
  const { isSellMode } = useProviderMode();
  const ra = useRoleAccess();
  const router = useRouter();
  const addToast = useLienStore((s) => s.addToast);

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

  const [actionOpen, setActionOpen] = useState(false);
  const [showFilter, setShowFilter] = useState(false);

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

  const columns = useMemo<ColumnDef<LienListItem, any>[]>(
    () => [
      {
        id: "lienNumber",
        header: "Lien #",
        cell: ({ row }) => (
          <span className="text-xs font-mono text-gray-700">
            {row.original.lienNumber}
          </span>
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
                {/* not part of migration phase 1 */}
                {/* {ra.can("lien:create") && (
                  <button
                    onClick={() => {
                      setShowCreate(true);
                      setActionOpen(false);
                    }}
                    className="w-full text-left px-4 py-2 text-sm hover:bg-gray-100"
                  >
                    New Lien
                  </button>
                )} */}
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
          onRowClick={(l) => router.push(lienDetailHref(l))}
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
    </div>
  );
}
