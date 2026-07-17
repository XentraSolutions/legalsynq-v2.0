"use client";

export const dynamic = "force-dynamic";

import { useState, useEffect, useCallback, useMemo } from "react";
import { useRouter } from "next/navigation";
import type { ColumnDef } from "@tanstack/react-table";
import { BaseTable } from "@/components/ui/base-table";
import { PageHeader } from "@/components/lien/page-header";
import { StatusBadge } from "@/components/lien/status-badge";
import { CreateLienModal } from "@/components/lien/forms/create-lien-modal";
import { useLienStore } from "@/stores/lien-store";
import { usePrimaryLoad, useBackgroundReady } from "@/hooks/use-background-queue";
import { ApiError } from "@/lib/api-client";
import {
  liensService,
  type LienListItem,
  type LiensQuery,
  type PaginationMeta,
} from "@/lib/liens";
import { LiensFilter, EMPTY_LIENS_FILTERS, type LiensFilterValues } from "./components/liens-filter";

function formatCurrency(amount: number | null): string {
  if (amount === null || amount === undefined) return "—";
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
  }).format(amount);
}

function formatShortDate(value: string | null | undefined): string {
  if (!value) return "—";
  const d = new Date(value);
  if (isNaN(d.getTime())) return value;
  return d.toLocaleDateString("en-US", {
    month: "2-digit",
    day: "2-digit",
    year: "numeric",
    timeZone: "UTC",
  });
}

function countActiveFilters(f: LiensFilterValues): number {
  return (
    // Each dropdown counts as 1 filter regardless of how many items are
    // checked within it, same as each date range counting as 1 below.
    (f.lawFirmIds.length ? 1 : 0) +
    (f.medicalFacilityIds.length ? 1 : 0) +
    (f.caseManagerIds.length ? 1 : 0) +
    (f.lienStatusIds.length ? 1 : 0) +
    (f.purchaseDateFrom || f.purchaseDateTo ? 1 : 0) +
    (f.closedDateFrom || f.closedDateTo ? 1 : 0)
  );
}

// Sticky classes for the frozen leading columns (Lien ID / Plaintiff Name /
// Law Firm). `left` offsets are cumulative fixed widths: 110px + 160px = 270px.
function frozenColumn(left: string, width: string, last = false) {
  const edge = last
    ? "border-r border-gray-200 shadow-[4px_0_6px_-4px_rgba(0,0,0,0.10)]"
    : "";
  return {
    headerClassName: `sticky ${left} z-10 bg-gray-50 ${width} ${edge}`,
    cellClassName: `sticky ${left} z-10 bg-white group-hover:bg-gray-50 transition-colors ${edge}`,
  };
}

function lienDetailHref(lien: LienListItem): string {
  return lien.caseId
    ? `/lien/cases/${lien.caseId}/liens/${lien.id}`
    : `/lien/liens/${lien.id}`;
}

export default function LiensPage() {
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
  // Registers the table's own load with the app-wide background queue so
  // the filter modal's option prefetch (below) waits for it instead of
  // competing with the primary table fetch for network/render time.
  // Combined with `!loading` directly (rather than trusting the queue
  // alone) so the very first render is correct with no propagation delay —
  // see useBackgroundReady's doc.
  usePrimaryLoad(loading);
  const bgReady = useBackgroundReady() && !loading;

  const [search, setSearch] = useState("");
  const [filters, setFilters] = useState<LiensFilterValues>(EMPTY_LIENS_FILTERS);
  const [showCreate, setShowCreate] = useState(false);
  const [showFilter, setShowFilter] = useState(false);

  const activeFilterCount = countActiveFilters(filters);

  const currentQuery = useCallback(
    (): LiensQuery => ({
      search: search || undefined,
      page: 1,
      pageSize: 10,
      lawFirmIds: filters.lawFirmIds,
      medicalFacilityIds: filters.medicalFacilityIds,
      caseManagerIds: filters.caseManagerIds,
      lienStatusIds: filters.lienStatusIds,
      purchaseDateFrom: filters.purchaseDateFrom || undefined,
      purchaseDateTo: filters.purchaseDateTo || undefined,
      closedDateFrom: filters.closedDateFrom || undefined,
      closedDateTo: filters.closedDateTo || undefined,
    }),
    [search, filters],
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
  }, [search, filters, fetchLiens, currentQuery]);

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

  const handleApplyFilter = (next: LiensFilterValues) => {
    setFilters(next);
  };

  const columns = useMemo<ColumnDef<LienListItem, any>[]>(
    () => [
      {
        id: "lienNumber",
        accessorKey: "lienNumber",
        header: "Lien ID",
        meta: frozenColumn("left-0", "w-[110px] min-w-[110px]"),
        cell: ({ row }) => (
          <span className="text-xs font-mono text-gray-700">
            {row.original.lienNumber}
          </span>
        ),
      },
      {
        id: "plaintiffName",
        header: "Plaintiff Name",
        meta: frozenColumn("left-[110px]", "w-[160px] min-w-[160px]"),
        cell: ({ row }) =>
          row.original.isConfidential ? (
            <span className="italic text-gray-400 text-sm">Confidential</span>
          ) : (
            <span className="text-sm text-gray-700">
              {row.original.plaintiff || row.original.subjectName || "—"}
            </span>
          ),
      },
      {
        id: "lawFirm",
        accessorKey: "lawFirm",
        header: "Law Firm",
        meta: frozenColumn("left-[270px]", "w-[150px] min-w-[150px]", true),
        cell: ({ row }) => (
          <span className="text-sm text-gray-700">{row.original.lawFirm || "—"}</span>
        ),
      },
      {
        id: "facilityName",
        accessorKey: "facilityName",
        header: "Medical Facility",
        cell: ({ row }) => (
          <span className="text-sm text-gray-700">{row.original.facilityName || "—"}</span>
        ),
      },
      {
        id: "purchaseDate",
        accessorKey: "purchaseDate",
        header: "Purchase Date",
        cell: ({ row }) => (
          <span className="text-sm text-gray-700 whitespace-nowrap">
            {row.original.purchaseDate || "—"}
          </span>
        ),
      },
      {
        id: "purchaseAmount",
        accessorKey: "purchaseAmount",
        header: "Purchase Amount",
        cell: ({ row }) => (
          <span className="text-sm text-gray-700 tabular-nums">
            {formatCurrency(row.original.purchaseAmount)}
          </span>
        ),
      },
      {
        id: "totalBilling",
        accessorKey: "totalBilling",
        header: "Billing Amount",
        cell: ({ row }) => (
          <span className="text-sm text-gray-700 tabular-nums">
            {formatCurrency(row.original.totalBilling)}
          </span>
        ),
      },
      {
        id: "status",
        accessorKey: "status",
        header: "Lien Status",
        cell: ({ row }) => <StatusBadge status={row.original.status} />,
      },
      {
        id: "initialServiceDate",
        accessorKey: "initialServiceDate",
        header: "Initial Service Date",
        cell: ({ row }) => (
          <span className="text-sm text-gray-700 whitespace-nowrap">
            {formatShortDate(row.original.initialServiceDate)}
          </span>
        ),
      },
      {
        id: "caseManager",
        accessorKey: "caseManager",
        header: "Case Manager",
        cell: ({ row }) => (
          <span className="text-sm text-gray-700">{row.original.caseManager || "—"}</span>
        ),
      },
    ],
    [],
  );

  return (
    <div className="space-y-5">
      <PageHeader title="Liens" subtitle={loading ? "Loading..." : `${pagination.totalCount} liens`} />

      <div className="bg-white border border-gray-200 rounded-xl px-4 py-3 flex flex-wrap items-center gap-3">
        <div className="relative flex-1 min-w-[200px]">
          <i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm" />
          <input
            type="text"
            placeholder="Search liens by number or subject..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full pl-9 pr-3 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
          />
        </div>
        <button
          onClick={() => setShowFilter(true)}
          className="relative flex items-center gap-1.5 text-sm font-medium text-gray-700 border border-gray-300 rounded-lg px-4 py-2 hover:bg-gray-50 transition-colors"
        >
          <i className="ri-filter-3-line text-base" />
          Filter
          {activeFilterCount > 0 && (
            <span className="ml-0.5 inline-flex items-center justify-center h-4 min-w-4 px-1 rounded-full bg-primary text-white text-[10px] font-semibold">
              {activeFilterCount}
            </span>
          )}
        </button>
        <button className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors">
          Export
        </button>
      </div>

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
          toolbar={
            activeFilterCount > 0 ? (
              <div className="flex items-center justify-between gap-3 px-4 py-3 bg-blue-50/70 border-b border-blue-100">
                <span className="flex items-center gap-2 text-sm text-gray-700">
                  <span className="h-2.5 w-2.5 rounded-full bg-primary" />
                  {activeFilterCount} Filter(s) Applied
                </span>
                <button
                  onClick={() => setFilters(EMPTY_LIENS_FILTERS)}
                  className="text-sm font-medium text-primary bg-white rounded-lg px-4 py-1.5 shadow-sm hover:bg-gray-50 transition-colors"
                >
                  Clear Filter
                </button>
              </div>
            ) : undefined
          }
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
        value={filters}
        onApplyFilter={handleApplyFilter}
        primaryReady={bgReady}
      />
    </div>
  );
}
