"use client";

import { useMemo, useState } from "react";
import type { SortingState } from "@tanstack/react-table";
import { LiensTableCard } from "@/components/selling/liens-table-card";
import {
  EMPTY_LIENS_FILTERS,
  type LiensFilterValues,
} from "@/app/(platform)/selling/liens/components/liens-filter";
import { useCaseLiens } from "@/lib/selling/use-case-liens";
import type { LiensQuery } from "@/lib/selling/liens.types";
import { ApiError } from "@/lib/api-client";

const SORT_BY_MAP: Record<string, string> = {
  createdAtUtc: "createdAtUtc",
  lienId: "lienId",
  fundingCompany: "fundingCompany",
  initialServiceDate: "initialServiceDate",
  billingAmount: "billingAmount",
  askAmount: "askAmount",
  status: "status",
};

export function CaseLiensTab({
  caseId,
  caseCode,
}: {
  caseId: string;
  caseCode: string;
}) {
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [filters, setFilters] = useState<LiensFilterValues>(EMPTY_LIENS_FILTERS);
  const [sorting, setSorting] = useState<SortingState>([]);
  const [pagination, setPagination] = useState({ page: 1, pageSize: 10 });

  const query = useMemo<LiensQuery>(
    () => ({
      tab: status || undefined,
      search: search.trim() || undefined,
      fundingCompanyIds: filters.fundingCompanyIds,
      initialServiceDateFrom: filters.initialServiceDateFrom || undefined,
      initialServiceDateTo: filters.initialServiceDateTo || undefined,
      sortBy: sorting[0] ? SORT_BY_MAP[sorting[0].id] : undefined,
      sortDirection: sorting[0] ? (sorting[0].desc ? "desc" : "asc") : undefined,
      page: pagination.page,
      pageSize: pagination.pageSize,
    }),
    [status, search, filters, sorting, pagination],
  );

  const { data, isLoading, isError, error, refetch } = useCaseLiens(caseId, query);

  const paginationMeta = {
    page: pagination.page,
    pageSize: pagination.pageSize,
    totalCount: data?.pagination.totalCount ?? 0,
    totalPages: data?.pagination.totalPages ?? 1,
  };

  return (
    <div className="space-y-4">
      {isError && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error instanceof ApiError ? error.message : "Failed to load liens."}
        </div>
      )}

      <LiensTableCard
        title="Liens"
        search={search}
        onSearchChange={(v) => {
          setSearch(v);
          setPagination((prev) => ({ ...prev, page: 1 }));
        }}
        status={status}
        onStatusChange={(v) => {
          setStatus(v);
          setPagination((prev) => ({ ...prev, page: 1 }));
        }}
        filters={filters}
        onApplyFilter={(next) => {
          setFilters(next);
          setPagination((prev) => ({ ...prev, page: 1 }));
        }}
        liens={data?.items ?? []}
        isLoading={isLoading}
        sorting={sorting}
        onSortingChange={(updater) => {
          setSorting((current) =>
            typeof updater === "function" ? updater(current) : updater,
          );
          setPagination((prev) => ({ ...prev, page: 1 }));
        }}
        pagination={paginationMeta}
        onPageChange={(page: number) =>
          setPagination((prev) => ({ ...prev, page }))
        }
        onPageSizeChange={(pageSize: number) =>
          setPagination({ page: 1, pageSize })
        }
        caseId={caseId}
        caseCode={caseCode}
        onBulkUploaded={() => refetch()}
      />
    </div>
  );
}
