"use client";

import { useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { File, CloudUpload, Search } from "lucide-react";
import type { SortingState } from "@tanstack/react-table";
import { Card } from "@/components/ui/dashboard-card";
import { PortfolioTable } from "@/components/selling/portfolio-table";
import { Button } from "@/components/selling/button";
import { ActionMenu } from "@/components/selling/action-menu";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  LiensFilter,
  EMPTY_LIENS_FILTERS,
  type LiensFilterValues,
} from "@/app/(platform)/selling/liens/components/liens-filter";
import { BulkUploadForm } from "@/components/selling/forms/bulk-upload-form";
import { useCaseLiens } from "@/lib/selling/use-case-liens";
import type { LiensQuery } from "@/lib/selling/liens.types";
import { ApiError } from "@/lib/api-client";

const STATUS_OPTIONS = [
  { key: "", label: "All Statuses" },
  { key: "Pending", label: "Pending" },
  { key: "Internal", label: "Internal" },
  { key: "Sold", label: "Sold" },
  { key: "Archived", label: "Archived" },
];

const SORT_BY_MAP: Record<string, string> = {
  createdAtUtc: "createdAtUtc",
  lienId: "lienId",
  fundingCompany: "fundingCompany",
  initialServiceDate: "initialServiceDate",
  billingAmount: "billingAmount",
  askAmount: "askAmount",
  status: "status",
};

function countActiveFilters(f: LiensFilterValues): number {
  return (
    (f.fundingCompanyIds.length ? 1 : 0) +
    (f.initialServiceDateFrom || f.initialServiceDateTo ? 1 : 0)
  );
}

export function CaseLiensTab({
  caseId,
  caseCode,
}: {
  caseId: string;
  caseCode: string;
}) {
  const router = useRouter();
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [filters, setFilters] = useState<LiensFilterValues>(EMPTY_LIENS_FILTERS);
  const [showFilter, setShowFilter] = useState(false);
  const [sorting, setSorting] = useState<SortingState>([]);
  const [pagination, setPagination] = useState({ page: 1, pageSize: 10 });
  const [bulkUpload, setBulkUpload] = useState(false);
  const activeFilterCount = countActiveFilters(filters);

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

      <Card title="Liens">
        <div className="bg-white rounded-xl py-3 flex flex-wrap items-center justify-between gap-3">
          <div className="flex flex-wrap items-center gap-3">
            <div className="relative flex-1 min-w-[300px] max-w-md">
              <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" />
              <input
                type="text"
                placeholder="Search"
                value={search}
                onChange={(e) => {
                  setSearch(e.target.value);
                  setPagination((prev) => ({ ...prev, page: 1 }));
                }}
                className="w-full pl-9 pr-3 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
              />
            </div>
            <Select
              value={status || "all"}
              onValueChange={(v) => {
                setStatus(v === "all" ? "" : v);
                setPagination((prev) => ({ ...prev, page: 1 }));
              }}
            >
              <SelectTrigger className="w-40">
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                {STATUS_OPTIONS.map((option) => (
                  <SelectItem key={option.key} value={option.key || "all"}>
                    {option.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Button
              variant="secondary"
              className="border-gray-300"
              leftIcon="settings2"
              onClick={() => setShowFilter(true)}
            >
              Filter
              {activeFilterCount > 0 && (
                <span className="ml-0.5 inline-flex items-center justify-center h-4 min-w-4 px-1 rounded-full bg-primary text-white text-[10px] font-semibold">
                  {activeFilterCount}
                </span>
              )}
            </Button>
          </div>
          <ActionMenu
            trigger={
              <Button variant="primary" rightIcon="chevronDown">
                Add New Lien
              </Button>
            }
            items={[
              {
                label: "Add Single Lien",
                icon: File,
                onClick: () =>
                  router.push(`/selling/portfolio/lien/add?caseId=${caseId}`),
              },
              {
                label: "Bulk Upload",
                icon: CloudUpload,
                onClick: () => setBulkUpload(true),
              },
            ]}
          />
        </div>

        <LiensFilter
          open={showFilter}
          onClose={() => setShowFilter(false)}
          value={filters}
          onApplyFilter={(next) => {
            setFilters(next);
            setPagination((prev) => ({ ...prev, page: 1 }));
          }}
        />

        <PortfolioTable
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
          handlePageChange={(page: number) =>
            setPagination((prev) => ({ ...prev, page }))
          }
          onPageSizeChange={(pageSize: number) =>
            setPagination({ page: 1, pageSize })
          }
        />
      </Card>

      {bulkUpload && (
        <BulkUploadForm
          open={bulkUpload}
          onClose={() => setBulkUpload(false)}
          referenceType="Case"
          referenceId={caseId}
          caseCode={caseCode}
          onUploaded={() => refetch()}
        />
      )}
    </div>
  );
}
