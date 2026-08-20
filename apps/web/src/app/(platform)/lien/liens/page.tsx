"use client";

export const dynamic = "force-dynamic";

import { useState, useEffect, useMemo } from "react";
import { useRouter } from "next/navigation";
import type { ColumnDef, SortingState } from "@tanstack/react-table";
import { BaseTable } from "@/components/ui/base-table";
import { PageHeader } from "@/components/lien/page-header";
import { FilterToolbar } from "@/components/lien/filter-toolbar";
import { StatusBadge } from "@/components/lien/status-badge";
import { DateDisplay } from "@/components/ui/date-display";
import { CreateLienModal } from "@/components/lien/forms/create-lien-modal";
import { useLienStore } from "@/stores/lien-store";
import {
  usePrimaryLoad,
  useBackgroundReady,
} from "@/hooks/use-background-queue";
import { ApiError } from "@/lib/api-client";
import {
  liensService,
  type LienListItem,
  type PaginationMeta,
} from "@/lib/liens";
import {
  LiensFilter,
  EMPTY_LIENS_FILTERS,
  type LiensFilterValues,
} from "./components/liens-filter";
import { LiensExportQuery } from "@/lib/liens/liens.types";
import { dateConverter } from "@/lib/cases/cases.mapper";
import { useLiens } from "@/hooks/use-case-liens";
import { useQueryClient } from "@tanstack/react-query";
import type { LiensQuery } from "@/lib/liens";

function formatCurrency(amount: number | null): string {
  if (amount === null || amount === undefined) return "—";
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
  }).format(amount);
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

// Maps table column ids to the sortBy keys ListLiens is expected to
// recognize (mirrors LienResponse field names — the same convention the
// lawFirmIds/medicalFacilityIds/etc. filter params already follow, see the
// TODO on LiensQuery in liens.types.ts). Backend support for sortBy/
// sortDirection on this endpoint isn't confirmed yet — this is what
// e2e/(platform)/lien/mutations/liens-sort.spec.ts verifies.
const SORT_BY_MAP: Record<string, string> = {
  lienNumber: "lienNumber",
  plaintiffName: "plaintiff",
  lawFirm: "lawFirm",
  facilityName: "medicalFacility",
  purchaseDate: "purchaseDate",
  purchaseAmount: "totalPurchase",
  totalBilling: "totalBilling",
  status: "status",
  initialServiceDate: "initialServiceDate",
  caseManager: "caseManager",
};

export default function LiensPage() {
  const router = useRouter();
  const addToast = useLienStore((s) => s.addToast);
  const queryClient = useQueryClient();

  const [pagination, setPagination] = useState<PaginationMeta>({
    page: 1,
    pageSize: 10,
    totalCount: 0,
    totalPages: 0,
  });

  const [search, setSearch] = useState("");
  const [searchInput, setSearchInput] = useState("");
  const [searchFocused, setSearchFocused] = useState(false);
  const [filters, setFilters] =
    useState<LiensFilterValues>(EMPTY_LIENS_FILTERS);
  const [sorting, setSorting] = useState<SortingState>([]);
  const [showCreate, setShowCreate] = useState(false);
  const [showFilter, setShowFilter] = useState(false);

  const activeFilterCount = countActiveFilters(filters);
  const [exporting, setExporting] = useState(false);

  const query: LiensQuery = {
    search: search || undefined,
    page: pagination.page,
    pageSize: 10,
    lawFirmIds: filters.lawFirmIds,
    medicalFacilityIds: filters.medicalFacilityIds,
    caseManagerIds: filters.caseManagerIds,
    lienStatusIds: filters.lienStatusIds,
    purchaseDateFrom: filters.purchaseDateFrom || undefined,
    purchaseDateTo: filters.purchaseDateTo || undefined,
    closedDateFrom: filters.closedDateFrom || undefined,
    closedDateTo: filters.closedDateTo || undefined,
    sortBy: sorting[0] ? SORT_BY_MAP[sorting[0].id] : undefined,
    sortDirection: sorting[0] ? (sorting[0].desc ? "desc" : "asc") : undefined,
  };

  const {
    data: liensResult,
    isLoading,
    isError,
    error,
    refetch,
  } = useLiens(query);
  const liens = liensResult?.items ?? [];

  // Registers the table's own load with the app-wide background queue so
  // the filter modal's option prefetch (below) waits for it instead of
  // competing with the primary table fetch for network/render time.
  // Combined with `!isLoading` directly (rather than trusting the queue
  // alone) so the very first render is correct with no propagation delay —
  // see useBackgroundReady's doc.
  usePrimaryLoad(isLoading);
  const bgReady = useBackgroundReady() && !isLoading;

  useEffect(() => {
    if (liensResult) setPagination(liensResult.pagination);
  }, [liensResult]);

  useEffect(() => {
    const timeout = setTimeout(() => setSearch(searchInput), 300);
    return () => clearTimeout(timeout);
  }, [searchInput]);

  useEffect(() => {
    setPagination((p) => ({ ...p, page: 1 }));
  }, [search, filters, sorting]);

  const exportLiens = async () => {
    const params: LiensExportQuery = {
      keyword: search ?? "",
      caseId: null,
      lawFirmId: filters.lawFirmIds?.length
        ? filters.lawFirmIds.toString()
        : null,
      medicalFacilityId: filters.medicalFacilityIds?.length
        ? filters.medicalFacilityIds.toString()
        : null,
      caseManagerId: filters.caseManagerIds?.length
        ? filters.caseManagerIds.toString()
        : null,
      lienStatusId: filters.lienStatusIds?.length
        ? filters.lienStatusIds.toString()
        : null,
      purchaseDate:
        filters.purchaseDateFrom && filters.purchaseDateTo
          ? `${dateConverter(filters.purchaseDateFrom)}-${dateConverter(filters.purchaseDateTo)}`
          : (filters.purchaseDateFrom ?? filters.purchaseDateTo ?? null),

      closedDate:
        filters.closedDateFrom && filters.closedDateTo
          ? `${dateConverter(filters.closedDateFrom)}-${dateConverter(filters.closedDateTo)}`
          : (filters.closedDateFrom ?? filters.closedDateTo ?? null),
    };
    try {
      setExporting(true);
      const req = await liensService.export(params);
      const item = req.data?.[0];
      const src = `data:text/${item.export_format};base64,${item.base64}`;
      const link = document.createElement("a");
      link.href = src;
      link.download = item.filename;
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

  const handleCreated = () => {
    setShowCreate(false);
    queryClient.invalidateQueries({ queryKey: ["liens"] });
    addToast({
      type: "success",
      title: "Lien Created",
      description: "New lien has been created successfully",
    });
  };

  const handleApplyFilter = (next: LiensFilterValues) => {
    setFilters(next);
  };

  const searchDropdown = searchFocused ? (
    <div
      onMouseDown={(e) => e.preventDefault()}
      className="absolute left-0 right-0 top-full mt-1 max-h-96 overflow-y-auto bg-white border border-gray-200 rounded-lg shadow-lg z-50"
    >
      {isLoading ? (
        <div className="px-4 py-3 text-sm text-gray-400">Searching...</div>
      ) : liens.length === 0 ? (
        <div className="px-4 py-3 text-sm text-gray-400">No liens found.</div>
      ) : (
        liens.map((l) => (
          <button
            key={l.id}
            type="button"
            onClick={() => {
              setSearchFocused(false);
              router.push(lienDetailHref(l));
            }}
            className="w-full text-left px-4 py-2.5 hover:bg-gray-50 border-b border-gray-100 last:border-b-0"
          >
            <div className="text-sm font-semibold text-gray-800">
              {l.isConfidential
                ? "Confidential"
                : l.plaintiff || l.subjectName}
            </div>
            <div className="text-xs text-gray-500 mt-0.5">
              <span className="text-primary">Initial Service Date: </span>
              <span className="text-gray-700">
                {l.initialServiceDate || "—"}
              </span>
              {", "}
              <span className="text-primary">Purchase Date: </span>
              <span className="text-gray-700">{l.purchaseDate || "—"}</span>
              {l.lawFirm ? `, ${l.lawFirm}` : ""}{" "}
              <span className="text-primary">Lien ID: </span>
              <span className="text-gray-700">{l.lienNumber}</span>
            </div>
          </button>
        ))
      )}
    </div>
  ) : null;

  const columns = useMemo<ColumnDef<LienListItem, any>[]>(
    () => [
      {
        id: "lienNumber",
        accessorKey: "lienNumber",
        header: "Lien ID",
        meta: {
          ...frozenColumn("left-0", "w-[170px] min-w-[170px]"),
          minWidth: "170px",
        },
        cell: ({ row }) => (
          <span className="text-sm">{row.original.lienNumber}</span>
        ),
      },
      {
        id: "plaintiffName",
        // Display-only columns (no accessorKey) are unsortable to TanStack by
        // default — this accessorFn is what makes the column sortable at all,
        // matching the isConfidential masking the cell itself renders.
        accessorFn: (row) =>
          row.isConfidential
            ? "Confidential"
            : row.plaintiff || row.subjectName || "",
        header: "Plaintiff Name",
        meta: {
          ...frozenColumn("left-[170px]", "w-[180px] min-w-[180px]"),
          minWidth: "180px",
        },
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
        meta: frozenColumn("left-[350px]", "w-[220px] min-w-[220px]", true),
        cell: ({ row }) => (
          <span className="text-sm text-gray-700">
            {row.original.lawFirm || "—"}
          </span>
        ),
      },
      {
        id: "facilityName",
        accessorKey: "facilityName",
        header: "Medical Facility",
        meta: { minWidth: "220px" },
        cell: ({ row }) => (
          <span className="text-sm text-gray-700">
            {row.original.facilityName || "—"}
          </span>
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
            <DateDisplay
              value={row.original.initialServiceDate}
              format="date"
              fallback="—"
            />
          </span>
        ),
      },
      {
        id: "caseManager",
        accessorKey: "caseManager",
        header: "Case Manager",
        cell: ({ row }) => (
          <span className="text-sm text-gray-700">
            {row.original.caseManager || "—"}
          </span>
        ),
      },
    ],
    [],
  );

  return (
    <div className="space-y-5">
      <PageHeader
        title="Liens"
        subtitle={isLoading ? "Loading..." : `${pagination.totalCount} liens`}
      />

      <FilterToolbar
        searchPlaceholder="Search liens by number or subject..."
        onSearch={(e) => {
          setSearchInput(e);
        }}
        onSearchFocus={() => setSearchFocused(true)}
        onSearchBlur={() => setSearchFocused(false)}
        dropdown={searchDropdown}
      >
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
        <button className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors disabled:bg-primary/50 cursor-pointer"
          onClick={()=>exportLiens()}
          disabled={exporting}
        >
          {exporting ? "Exporting..." : "Export"}
        </button>
      </FilterToolbar>

      {isError && (
        <div className="flex items-center gap-2 p-3 bg-red-50 border border-red-200 rounded-lg">
          <i className="ri-error-warning-line text-red-600" />
          <p className="text-sm text-red-700">
            {error instanceof ApiError ? error.message : "Failed to load liens"}
          </p>
          <button
            onClick={() => refetch()}
            className="ml-auto text-sm text-red-600 hover:underline"
          >
            Retry
          </button>
        </div>
      )}

      <BaseTable
        data={liens}
        columns={columns}
        getRowId={(l) => l.id}
        isLoading={isLoading}
        sorting={sorting}
        onSortingChange={setSorting}
        manualSorting
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
          setPagination((p) => ({ ...p, page: next.pageIndex + 1 }));
        }}
        className="bg-white border-gray-200 rounded-xl"
      />

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
