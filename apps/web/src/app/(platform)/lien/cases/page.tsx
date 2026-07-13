"use client";

import { useState, useEffect, useCallback, useMemo } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { PageHeader } from "@/components/lien/page-header";
import { FilterToolbar } from "@/components/lien/filter-toolbar";
import { StatusBadge } from "@/components/lien/status-badge";
import { ActionMenu } from "@/components/lien/action-menu";
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
import { ApiError } from "@/lib/api-client";
import { CasesFilter } from "./components/cases-filter";
import { CasesQuery, CaseStatusResponse } from "@/lib/cases/cases.types";
import { useCases, useCreateCase } from "@/hooks/use-case-liens";
import { useQueryClient } from "@tanstack/react-query";

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
  const [status, setStatus] = useState<Array<CaseStatusResponse>>();

  const [pagination, setPagination] = useState<PaginationMeta>({
    page: 1,
    pageSize: 20,
    totalCount: 0,
    totalPages: 0,
  });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState("");
  const [searchInput, setSearchInput] = useState("");

  const [statusFilter, setStatusFilter] = useState<string>("");
  const [params, setParams] = useState<{
    accidentTypeId: string | null;
    caseManagerId: string | null;
    lawFirmId: string | null;
    statusId: string | null;
  }>({
    accidentTypeId: null,
    caseManagerId: null,
    lawFirmId: null,
    statusId: null,
  });
  const [showCreate, setShowCreate] = useState(false);
  const [showFilter, setShowFilter] = useState(false);
  const [confirmAction, setConfirmAction] = useState<{
    id: string;
    status: string;
    name: string;
  } | null>(null);
  const [actionOpen, setActionOpen] = useState(false);

  const [bulkAction, setBulkAction] = useState<BulkActionConfig | null>(null);
  const [bulkLoading, setBulkLoading] = useState(false);
  const [bulkResult, setBulkResult] = useState<BulkOperationResult | null>(
    null,
  );
  const query = {
    keyword: search || "",
    page: pagination.page,
    limit: 20,
    sortBy: "createdAt",
    sortDirection: "desc",
    accidentTypeId: params?.accidentTypeId?.toString() ?? "",
    caseManagerId: params?.caseManagerId?.toString() ?? "",
    lawFirmId: params?.lawFirmId?.toString() ?? "",
    statusId: statusFilter.toString() || params?.statusId?.toString(),
  };

  const { data: cases, isLoading, isFetching } = useCases(query);
  const queryClient = useQueryClient();

  const caseNumber = useMemo(() => {
    if (!showCreate) return "";
    const year = new Date().getFullYear();
    const nextCount = pagination.totalCount + 1;
    const paddedCount = String(nextCount).padStart(4, "0");
    return `CASE-${year}-${paddedCount}`;
  }, [showCreate, pagination]);

  const fetchCases = () => {
    queryClient.invalidateQueries({
      queryKey: ["cases"],
    });
  };

  const lookupCaseStatus = useCallback(async () => {
    try {
      const result = await casesService.getCaseStatus();
      setStatus(result);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      }
    } finally {
    }
  }, []);

  const exportCases = async () => {
    const response = await casesService.exportCases({
      caseId: null,
      keyword: search,
      ...params,
    });

    const src = `data:text/${response.data[0]?.export_format};base64,${response.data[0]?.base64}`;
    const link = document.createElement("a");
    link.href = src;
    link.download = response.data[0]?.filename;
    link.click();
    link.remove();
  };

  useEffect(() => {
    lookupCaseStatus();
  }, []);

  useEffect(() => {
    const timeout = setTimeout(() => setSearch(searchInput), 300);
    return () => clearTimeout(timeout);
  }, [searchInput]);

  useEffect(() => {
    // fetchCases();
  }, [pagination.page, search]);

  const canEdit = ra.can("case:edit");

  const handleAdvanceStatus = async (caseItem: CaseListItem) => {
    const currentStatus = status?.find((s) => s.code === caseItem.status);

    if (!currentStatus) return;

    const nextStatus = status?.find(
      (s) => s.sortOrder === currentStatus.sortOrder + 1,
    );

    if (nextStatus) {
      setConfirmAction({
        id: caseItem.id,
        status: nextStatus.code,
        name: nextStatus.name,
      });
    }
  };

  const handleChangeStatusFilter = async (statusName: string) => {
    const filtered = status?.find((s) => s.code == statusName);
    setStatusFilter(filtered?.code ?? "");
  };

  const confirmStatusChange = async () => {
    if (!confirmAction) return;
    try {
      const response = await casesService.updateCaseStatus(
        confirmAction.id,
        confirmAction.status,
      );
      addToast({
        type: "success",
        title: "Status Updated",
        description: `Case moved to ${response.status}`,
      });
      setConfirmAction(null);
      fetchCases();
    } catch (err) {
      const message =
        err instanceof ApiError ? err.message : "Failed to update status";
      addToast({ type: "error", title: "Update Failed", description: message });
      setConfirmAction(null);
    }
  };

  const handleCaseCreated = () => {
    setShowCreate(false);
  };

  const handleCasesFilter = (e: any) => {
    setShowFilter(false);
    setParams(e);
  };

  const handleBulkAction = (actionKey: string) => {
    const action = BULK_ACTIONS.find((a) => a.key === actionKey);
    if (action) setBulkAction(action);
  };

  const executeBulkAction = async () => {
    if (!bulkAction) return;
    if (!cases) return;
    setBulkLoading(true);
    const result = await executeBulk(selection.ids, async (id) => {
      const caseItem = cases.items.find((c) => c.id === id);
      if (!caseItem) throw new Error("Case not found in current list");
      const idx = STATUSES.indexOf(caseItem.status);
      if (idx >= STATUSES.length - 1)
        throw new Error(
          `Case is already "${STATUS_LABELS[caseItem.status] || caseItem.status}"`,
        );
      await casesService.updateCaseStatus(id, STATUSES[idx + 1]);
    });
    setBulkLoading(false);
    setBulkAction(null);
    setBulkResult(result);
    selection.clear();
    fetchCases();
  };

  const allIds = cases?.items.map((c) => c.id) ?? [];

  return (
    <div className="space-y-4">
      <PageHeader
        title="Cases"
        subtitle={isLoading ? "Loading..." : `${pagination.totalCount} cases`}
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
                {/* Create Case */}
                {ra.can("case:create") && (
                  <button
                    onClick={() => {
                      setShowCreate(true);
                      setActionOpen(false);
                    }}
                    className="w-full text-left px-4 py-2 text-sm hover:bg-gray-100"
                  >
                    Create Case
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
                    exportCases();
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
        searchPlaceholder="Search by case number or client name..."
        onSearch={(e) => {
          console.log(e);
          setSearchInput(e);
        }}
        filters={[
          {
            label: "All Statuses",
            value: statusFilter,
            onChange: (e) => handleChangeStatusFilter(e),
            options: status
              ? status?.map((s) => ({
                  value: s.code,
                  label: s.name,
                }))
              : [],
          },
        ]}
      />

      <BulkResultBanner
        result={bulkResult}
        onDismiss={() => setBulkResult(null)}
        entityLabel="cases"
      />

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 flex items-center gap-2">
          <i className="ri-error-warning-line text-red-500 text-sm" />
          <p className="text-sm text-red-700">{error}</p>
          <button
            onClick={() => fetchCases()}
            className="ml-auto text-sm text-red-600 hover:underline font-medium"
          >
            Retry
          </button>
        </div>
      )}

      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        {isLoading ? (
          <div className="py-12 text-center">
            <div className="inline-block h-5 w-5 animate-spin rounded-full border-2 border-primary border-t-transparent" />
            <p className="text-sm text-gray-400 mt-2">Loading cases...</p>
          </div>
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="min-w-full text-sm">
                <thead>
                  <tr className="bg-gray-50/80 border-b border-gray-100">
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">
                      Case ID
                    </th>
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">
                      Plaintiff Name
                    </th>
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">
                      Law Firm
                    </th>
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">
                      Case Manager
                    </th>
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">
                      Accident Type
                    </th>
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">
                      Date of Loss
                    </th>
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">
                      DOB
                    </th>
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">
                      Status
                    </th>
                    <th className="px-3 py-2.5 w-10" />
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {cases != undefined &&
                    cases.items.map((c) => (
                      <tr
                        key={c.id}
                        className={`hover:bg-gray-50/80 transition-colors cursor-pointer ${selection.isSelected(c.id) ? "bg-primary/5" : ""}`}
                        onClick={() => router.push(`/lien/cases/${c.id}`)}
                      >
                        <td className="px-3 py-2.5">
                          <Link
                            href={`/lien/cases/${c.id}`}
                            onClick={(e) => e.stopPropagation()}
                            className="text-xs font-mono text-primary hover:underline"
                          >
                            {c.caseNumber}
                          </Link>
                        </td>
                        <td className="px-3 py-2.5 text-sm text-gray-700 font-medium">
                          {c.clientName}
                        </td>
                        <td className="px-3 py-2.5 text-sm text-gray-600">
                          {c.lawFirm || "—"}
                        </td>
                        <td className="px-3 py-2.5 text-sm text-gray-600">
                          {c.caseManager || "—"}
                        </td>
                        <td className="px-3 py-2.5 text-sm text-gray-600">
                          {c.accidentType || "—"}
                        </td>
                        <td className="px-3 py-2.5 text-xs text-gray-500 tabular-nums">
                          {c.dateOfIncident || "—"}
                        </td>
                        <td className="px-3 py-2.5 text-xs text-gray-500 tabular-nums">
                          {c.clientDob || "—"}
                        </td>
                        <td className="px-3 py-2.5">
                          <StatusBadge status={c.status} />
                        </td>
                        <td
                          className="px-3 py-2.5 text-right"
                          onClick={(e) => e.stopPropagation()}
                        >
                          <ActionMenu
                            items={[
                              {
                                label: "View Details",
                                icon: "ri-eye-line",
                                onClick: () =>
                                  router.push(`/lien/cases/${c.id}`),
                              },
                              ...(canEdit
                                ? [
                                    {
                                      label: "Advance Status",
                                      icon: "ri-arrow-right-line",
                                      onClick: () => handleAdvanceStatus(c),
                                    },
                                  ]
                                : []),
                            ]}
                          />
                        </td>
                      </tr>
                    ))}
                </tbody>
              </table>
            </div>
            {cases?.items?.length === 0 && !loading && (
              <div className="py-12 text-center">
                <i className="ri-folder-open-line text-2xl text-gray-300" />
                <p className="text-sm text-gray-400 mt-2">
                  No cases match your filters.
                </p>
              </div>
            )}
          </>
        )}
      </div>

      {pagination.totalPages > 0 && (
        <div className="flex items-center justify-between">
          <p className="text-xs text-gray-500">
            Page {pagination.page} of {pagination.totalPages} ·{" "}
            {pagination.totalCount} total
          </p>
          <div className="flex gap-1.5">
            <button
              onClick={() => setPagination((p) => ({ ...p, page: p.page - 1 }))}
              disabled={pagination.page <= 1}
              className="px-3 py-1.5 text-xs font-medium border border-gray-200 rounded-lg hover:bg-gray-50 disabled:opacity-40 transition-colors"
            >
              Previous
            </button>
            <button
              onClick={() => setPagination((p) => ({ ...p, page: p.page + 1 }))}
              disabled={pagination.page >= pagination.totalPages}
              className="px-3 py-1.5 text-xs font-medium border border-gray-200 rounded-lg hover:bg-gray-50 disabled:opacity-40 transition-colors"
            >
              Next
            </button>
          </div>
        </div>
      )}

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
        onApplyFilter={handleCasesFilter}
      />

      {confirmAction && (
        <ConfirmDialog
          open
          onClose={() => setConfirmAction(null)}
          onConfirm={confirmStatusChange}
          title="Change Case Status"
          description={`Move this case to ${confirmAction.name}?`}
          confirmLabel="Update Status"
        />
      )}
    </div>
  );
}
