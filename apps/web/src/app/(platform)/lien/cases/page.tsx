"use client";

import { useState, useEffect, useMemo } from "react";
import { useRouter } from "next/navigation";
import type { ColumnDef, SortingState } from "@tanstack/react-table";
import { BaseTable } from "@/components/ui/base-table";
import { PageHeader } from "@/components/lien/page-header";
import { FilterToolbar } from "@/components/lien/filter-toolbar";
import { StatusBadge } from "@/components/lien/status-badge";
import { ConfirmDialog } from "@/components/lien/modal";
import { CreateCaseForm } from "@/components/lien/forms/create-case-form";
import { BulkActionBar } from "@/components/lien/bulk-action-bar";
import { BulkConfirmModal } from "@/components/lien/bulk-confirm-modal";
import { BulkResultBanner } from "@/components/lien/bulk-result-banner";
import { useLienStore } from "@/stores/lien-store";
import { useRoleAccess } from "@/hooks/use-role-access";
import { useSelectionState } from "@/hooks/use-selection-state";
import {
  casesService,
  type CaseListItem,
  type PaginationMeta,
} from "@/lib/cases";
import {
  executeBulk,
  type BulkActionConfig,
  type BulkOperationResult,
} from "@/lib/bulk-operations";
import {
  CasesFilter,
  EMPTY_CASES_FILTERS,
  type CasesFilterValues,
} from "./components/cases-filter";
import { CasesQuery } from "@/lib/cases/cases.types";
import { useCases, useCreateCase } from "@/hooks/use-case-liens";
import { useQueryClient } from "@tanstack/react-query";
import {
  usePrimaryLoad,
  useBackgroundReady,
} from "@/hooks/use-background-queue";
import MedicalLienComponent from "@/components/lien/add-medical-lien/add-medical-lien/medical-lien-component";
import { ApiError } from "@/lib/api-client";

export const dynamic = "force-dynamic";

const STATUSES = [
  "PreDemand",
  "DemandSent",
  "InNegotiation",
  "CaseSettled",
  "Closed",
];
const STATUS_LABELS: Record<string, string> = {
  PreDemand: "Pre-Demand",
  DemandSent: "Demand Sent",
  InNegotiation: "In Negotiation",
  CaseSettled: "Case Settled",
  Closed: "Closed",
};

// Maps table column ids to the sortBy keys the cases v3 endpoint recognizes
// (see CaseRepository.GetPagedAsync's sortBy switch). lawFirm/caseManager/
// accidentType aren't handled by that switch yet (backend support pending) —
// once added, these keys should match whatever the backend expects.
const SORT_BY_MAP: Record<string, string> = {
  caseNumber: "caseCode",
  clientName: "fullName",
  dateOfIncident: "dateOfLoss",
  clientDob: "dateOfBirth",
  status: "status",
  lawFirm: "lawFirm",
  caseManager: "caseManager",
  accidentType: "accidentType",
};

function countActiveFilters(f: CasesFilterValues): number {
  return (
    (f.lawFirmId.length ? 1 : 0) +
    (f.accidentTypeId.length ? 1 : 0) +
    (f.caseManagerId.length ? 1 : 0) +
    (f.statusId.length ? 1 : 0)
  );
}

const BULK_ACTIONS: BulkActionConfig[] = [
  {
    key: "advance-status",
    label: "Advance Status",
    icon: "ri-arrow-right-line",
    variant: "primary",
    confirmTitle: "Advance Case Status",
    confirmDescription: (count) =>
      `This will advance ${count} case${count !== 1 ? "s" : ""} to their next status. Cases already at "Closed" will be skipped.`,
  },
];

export default function CasesPage() {
  const router = useRouter();
  const addToast = useLienStore((s) => s.addToast);
  const ra = useRoleAccess();
  const selection = useSelectionState();

  // const [cases, setCases] = useState<CaseListItem[]>([]);

  const [pagination, setPagination] = useState<PaginationMeta>({
    page: 1,
    pageSize: 10,
    totalCount: 0,
    totalPages: 0,
  });
  const [exporting, setExporting] = useState(true);

  const [search, setSearch] = useState("");
  const [searchInput, setSearchInput] = useState("");
  const [searchFocused, setSearchFocused] = useState(false);

  const [filters, setFilters] =
    useState<CasesFilterValues>(EMPTY_CASES_FILTERS);
  const [showCreate, setShowCreate] = useState(false);
  const [showFilter, setShowFilter] = useState(false);
  const [showMedicalLien, setShowMedicalLien] = useState(false);

  const [confirmAction, setConfirmAction] = useState<boolean>(false);

  const [bulkAction, setBulkAction] = useState<BulkActionConfig | null>(null);
  const [bulkLoading, setBulkLoading] = useState(false);
  const [bulkResult, setBulkResult] = useState<BulkOperationResult | null>(
    null,
  );
  const [caseId, setCaseId] = useState("");

  const [sorting, setSorting] = useState<SortingState>([
    { id: "createdAt", desc: true },
  ]);

  const query = {
    keyword: search || "",
    page: pagination.page,
    limit: 10,
    sortBy: (sorting[0] && SORT_BY_MAP[sorting[0].id]) ?? "createdAt",
    sortDirection: sorting[0]?.desc === false ? "asc" : "desc",
    accidentTypeId: filters.accidentTypeId.join(",") || "",
    caseManagerId: filters.caseManagerId.join(",") || "",
    lawFirmId: filters.lawFirmId.join(",") || "",
    statusId: filters.statusId.join(",") || "",
  };

  const { data: cases, isLoading, isFetching } = useCases(query);
  const queryClient = useQueryClient();
  // Registers the table's own load with the app-wide background queue so the
  // filter modal's option prefetch waits for it instead of competing with the
  // primary table fetch — same pattern as the liens page.
  usePrimaryLoad(isLoading);
  const bgReady = useBackgroundReady() && !isLoading;
  const caseNumber = useMemo(() => {
    if (!showCreate) return "";
    const year = new Date().getFullYear();
    const nextCount = pagination.totalCount + 1;
    const paddedCount = String(nextCount).padStart(4, "0");
    return `CASE-${year}-${paddedCount}`;
  }, [showCreate, pagination]);

  const exportCases = async () => {
    setExporting(true);
    try {
      const response = await casesService.exportCases({
        caseId: null,
        keyword: search,
        lawFirmId: filters.lawFirmId.join(",") || null,
        accidentTypeId: filters.accidentTypeId.join(",") || null,
        statusId: filters.statusId.join(",") || null,
        caseManagerId: filters.caseManagerId.join(",") || null,
      });

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

  useEffect(() => {
    if (cases) setPagination(cases?.pagination);
  }, [cases]);

  useEffect(() => {
    const timeout = setTimeout(() => setSearch(searchInput), 300);
    return () => clearTimeout(timeout);
  }, [searchInput]);

  useEffect(() => {
    setPagination((p) => ({ ...p, page: 1 }));
  }, [sorting]);

  useEffect(() => {}, [pagination.page, search]);

  const canEdit = ra.can("case:edit");

  const confirmStatusChange = async () => {
    setShowMedicalLien(true);
    setConfirmAction(false);
  };

  const handleCaseCreated = (id: string) => {
    setShowCreate(false);
    setCaseId(id);
    setConfirmAction(true);
  };

  const handleApplyFilter = (next: CasesFilterValues) => {
    setFilters(next);
  };

  const allIds = cases?.items.map((c) => c.id) ?? [];
  const activeFilterCount = countActiveFilters(filters);

  const searchDropdown = searchFocused ? (
    <div
      onMouseDown={(e) => e.preventDefault()}
      className="absolute left-0 right-0 top-full mt-1 max-h-96 overflow-y-auto bg-white border border-gray-200 rounded-lg shadow-lg z-50"
    >
      {isLoading ? (
        <div className="px-4 py-3 text-sm text-gray-400">Searching...</div>
      ) : (cases?.items.length ?? 0) === 0 ? (
        <div className="px-4 py-3 text-sm text-gray-400">No cases found.</div>
      ) : (
        cases!.items.map((c) => (
          <button
            key={c.id}
            type="button"
            onClick={() => {
              setSearchFocused(false);
              router.push(`/lien/cases/${c.id}`);
            }}
            className="w-full text-left px-4 py-2.5 hover:bg-gray-50 border-b border-gray-100 last:border-b-0"
          >
            <div className="text-sm font-semibold text-gray-800">
              {c.clientName}
            </div>
            <div className="text-xs text-gray-500 mt-0.5">
              <span className="text-primary">Date of Loss: </span>
              <span className="text-gray-700">{c.dateOfIncident}</span>
              {", "}
              <span className="text-primary">Date of Birth: </span>
              <span className="text-gray-700">{c.clientDob}</span>
              {c.lawFirm ? `, ${c.lawFirm}` : ""}{" "}
              <span className="text-primary">Case ID: </span>
              <span className="text-gray-700">{c.caseNumber}</span>
            </div>
          </button>
        ))
      )}
      {ra.can("case:create") && (
        <button
          type="button"
          onClick={() => {
            setSearchFocused(false);
            setShowCreate(true);
          }}
          className="w-full text-left px-4 py-2.5 text-sm font-medium text-primary hover:bg-gray-50 flex items-center gap-1.5 border-t border-gray-100"
        >
          <i className="ri-add-line text-base" />
          Add New Case
        </button>
      )}
    </div>
  ) : null;

  const columns = useMemo<ColumnDef<CaseListItem, any>[]>(
    () => [
      {
        id: "caseNumber",
        header: "Case ID",
        accessorFn: (row) => row.caseNumber,
        meta: { minWidth: "180px" },
        cell: ({ row }) => (
          <span className="text-sm font-medium text-gray-700">
            {row.original.caseNumber}
          </span>
        ),
      },
      {
        id: "clientName",
        header: "Plaintiff Name",
        accessorFn: (row) => row.clientName,
        cell: ({ row }) => (
          <span className="text-sm text-gray-700 ">
            {row.original.clientName}
          </span>
        ),
      },
      {
        id: "lawFirm",
        header: "Law Firm",
        accessorFn: (row) => row.lawFirm,
        cell: ({ row }) => (
          <span className="text-sm text-gray-600">
            {row.original.lawFirm || "—"}
          </span>
        ),
      },
      {
        id: "caseManager",
        header: "Case Manager",
        accessorFn: (row) => row.caseManager,
        cell: ({ row }) => (
          <span className="text-sm text-gray-600">
            {row.original.caseManager || "—"}
          </span>
        ),
      },
      {
        id: "accidentType",
        header: "Accident Type",
        accessorFn: (row) => row.accidentType,
        cell: ({ row }) => (
          <span className="text-sm text-gray-600">
            {row.original.accidentType || "—"}
          </span>
        ),
      },
      {
        id: "dateOfIncident",
        header: "Date of Loss",
        accessorFn: (row) => row.dateOfIncident,
        cell: ({ row }) => (
          <span className="text-xs text-gray-500 tabular-nums">
            {row.original.dateOfIncident || "—"}
          </span>
        ),
      },
      {
        id: "clientDob",
        header: "DOB",
        accessorFn: (row) => row.clientDob,
        cell: ({ row }) => (
          <span className="text-xs text-gray-500 tabular-nums">
            {row.original.clientDob || "—"}
          </span>
        ),
      },
      {
        id: "status",
        header: "Status",
        accessorFn: (row) => row.status,
        cell: ({ row }) => (
          <StatusBadge
            status={row.original.status}
            label={row.original.statusLabel}
          />
        ),
      },
    ],
    [router],
  );

  return (
    <div className="space-y-4">
      <PageHeader
        title="Cases"
        subtitle={isLoading ? "Loading..." : `${pagination.totalCount} cases`}
        actions={
          ra.can("case:create") ? (
            <button
              onClick={() => setShowCreate(true)}
              className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors"
            >
              <i className="ri-add-line text-base" />
              Add New Case
            </button>
          ) : undefined
        }
      />

      <FilterToolbar
        searchPlaceholder="Search by case number or client name..."
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
        <button
          onClick={exportCases}
          className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors"
          disabled={exporting}
        >
          {exporting ? "Exporting..." : "Export"}
        </button>
      </FilterToolbar>

      <BulkResultBanner
        result={bulkResult}
        onDismiss={() => setBulkResult(null)}
        entityLabel="cases"
      />

      <BaseTable
        data={cases?.items ?? []}
        columns={columns}
        getRowId={(c) => c.id}
        isLoading={isLoading}
        emptyMessage="No cases match your filters."
        onRowClick={(c) => router.push(`/lien/cases/${c.id}`)}
        getRowClassName={(c) =>
          selection.isSelected(c.id) ? "bg-primary/5" : undefined
        }
        sorting={sorting}
        onSortingChange={setSorting}
        manualSorting
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

      {showCreate && (
        <CreateCaseForm
          open={showCreate}
          caseNumber={caseNumber}
          onClose={() => setShowCreate(false)}
          onCreated={handleCaseCreated}
        />
      )}
      <CasesFilter
        open={showFilter}
        onClose={() => setShowFilter(false)}
        value={filters}
        onApplyFilter={handleApplyFilter}
        primaryReady={bgReady}
      />

      <ConfirmDialog
        open={confirmAction}
        onClose={() => setConfirmAction(false)}
        onConfirm={confirmStatusChange}
        title="Confirmation"
        description={`New Case created. Do you want to add a lien now?`}
        confirmLabel="Yes"
      />
      {showMedicalLien && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 overflow-y-auto">
          <div className="bg-white rounded-lg shadow-lg max-w-2xl w-full mx-4 my-6">
            <MedicalLienComponent
              caseId={caseId}
              onClose={() => {
                setShowMedicalLien(false);
                router.push(`/lien/cases/${caseId}/liens`);
              }}
            />
          </div>
        </div>
      )}
    </div>
  );
}

// "https://legal-dmm-prod.legalsynq.com/70om7wvWruLZg1PA/DrS0uTyouKgBVGQKnlGj1WVe7l0JCksh.pdf"
