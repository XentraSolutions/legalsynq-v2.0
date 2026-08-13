// app/portfolio/PortfolioClient.tsx

"use client";

import { useQuery } from "@tanstack/react-query";
import { ChevronDown, CloudUpload, File, Search, Settings2 } from "lucide-react";
import { ActionMenu } from "./action-menu";
import { LiensQuery, liensService } from "@/lib/selling";
import { useCallback, useEffect, useMemo, useState } from "react";
import { MetricCard } from "./dashboard/metric-card";
import { Tabs } from "../ui/tabs";
import { PortfolioTable } from "./portfolio-table";
import { Card } from "../ui/dashboard-card";
import {
  LiensFilter,
  LiensFilterValues,
} from "@/app/(platform)/selling/liens/components/liens-filter";
import { EMPTY_LIENS_FILTERS } from "@/app/(platform)/selling/liens/components/liens-filter";
import {
  useBackgroundReady,
  usePrimaryLoad,
} from "@/hooks/use-background-queue";
import { useRouter } from "next/navigation";
import { BulkUploadForm } from "./forms/bulk-upload-form";
import { PaginationMeta } from "@/lib/liens";
import { SortingState } from "@tanstack/react-table";
import { useLienStore } from "@/stores/lien-store";
import { Button } from "@/components/ui/button";
import { PageHeader } from "@/components/lien/page-header";
import { SkeletonCard, SkeletonTable } from "@/components/lien/skeleton-loader";

const PORTFOLIO_STATUSES = [
  { key: "Pending", label: "Pending" },
  { key: "Internal", label: "Internal" },
  { key: "Sold", label: "Sold" },
  // { key: "all", label: "all" },
];

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

const INITIAL_QUERY: Record<string, unknown> = {
  tab: "Pending",
  search: "",
  fundingCompanyIds: [],
  lienStatusIds: [],
  initialServiceDateFrom: [],
  initialServiceDateTo: [],
  sortBy: undefined,
  initialServiceDate: "",
  sortDirection: undefined,
  page: 1,
  pageSize: 20,
};

function PortfolioSkeleton() {
  return (
    <div className="space-y-4 animate-pulse">
      <div className="flex items-center justify-between gap-4">
        <div className="space-y-2">
          <div className="h-5 bg-gray-200 rounded w-40" />
          <div className="h-3 bg-gray-100 rounded w-80" />
        </div>
        <div className="h-9 bg-gray-100 rounded-lg w-36" />
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        {Array.from({ length: 4 }).map((_, i) => (
          <SkeletonCard key={i} />
        ))}
      </div>

      <div className="flex gap-2">
        {Array.from({ length: 3 }).map((_, i) => (
          <div key={i} className="h-8 bg-gray-100 rounded-lg w-20" />
        ))}
      </div>

      <div className="bg-white border border-gray-200 rounded-xl p-5 space-y-4">
        <div className="flex items-center gap-3">
          <div className="h-9 bg-gray-100 rounded-lg flex-1 max-w-md" />
          <div className="h-9 bg-gray-100 rounded-lg w-24" />
        </div>
        <SkeletonTable rows={6} cols={6} />
      </div>
    </div>
  );
}

export default function PortfolioClient() {
  const router = useRouter();
  const [loading, setLoading] = useState(true);
  usePrimaryLoad(loading);
  const bgReady = useBackgroundReady() && !loading;
  const [search, setSearch] = useState("");
  const [searchInput, setSearchInput] = useState("");
  const [searchFocused, setSearchFocused] = useState(false);

  const [selectedStatus, setSelectedStatus] = useState<string>("Pending");
  const [filters, setFilters] =
    useState<LiensFilterValues>(EMPTY_LIENS_FILTERS);
  const [showFilter, setShowFilter] = useState(false);
  const activeFilterCount = countActiveFilters(filters);
  const [bulkUpload, setbulkUpload] = useState(false);
  const [sorting, setSorting] = useState<SortingState>([]);
  const [pagination, setPagination] = useState<PaginationMeta>({
    page: 1,
    pageSize: 10,
    totalCount: 0,
    totalPages: 0,
  });

  function countActiveFilters(f: LiensFilterValues): number {
    return (
      // Each dropdown counts as 1 filter regardless of how many items are
      // checked within it, same as each date range counting as 1 below.
      (f.fundingCompanyIds.length ? 1 : 0) +
      (f.initialServiceDateFrom || f.initialServiceDateTo ? 1 : 0)
    );
  }
  const currentQuery = useMemo(
    (): LiensQuery => ({
      tab: selectedStatus,
      search: searchInput || undefined,
      fundingCompanyIds: filters.fundingCompanyIds,
      lienStatusIds: filters.lienStatusIds,
      initialServiceDateFrom: filters.initialServiceDateFrom,
      initialServiceDateTo: filters.initialServiceDateTo,
      sortBy: sorting[0] ? SORT_BY_MAP[sorting[0].id] : undefined,
      initialServiceDate: filters.initialServiceDate,
      sortDirection: sorting[0] ? (sorting[0].desc ? "desc" : "asc") : "desc",
      page: pagination.page,
      pageSize: pagination.pageSize,
    }),
    [selectedStatus, search, filters, sorting, pagination],
  );
  const { data, isPending, error, refetch } = useQuery({
    queryKey: ["portfolio"],
    queryFn: () => liensService.getSellingDashboard(INITIAL_QUERY),
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  });

  const {
    data: liens,
    isPending: isLiensPending,
    error: isLiensError,
    refetch: refetchLiens,
  } = useQuery({
    queryKey: ["liens", currentQuery],
    queryFn: () => liensService.getLiens(currentQuery),
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  });

  const paginationData = useMemo(
    () => ({
      ...pagination,
      totalPages: liens?.pagination.totalPages ?? 1,
      totalCount: liens?.pagination.totalCount ?? 0,
    }),
    [pagination, liens],
  );

  const handlePageChange = (newPage: number) => {
    setPagination((prev) => ({ ...prev, page: newPage }));
  };

  useEffect(() => {
    refetchLiens();
  }, [currentQuery, refetchLiens]);

  useEffect(() => {
    setPagination({
      page: 1,
      pageSize: 10,
      totalCount: 0,
      totalPages: 0,
    });
  }, [selectedStatus]);

  useEffect(() => {
    const timeout = setTimeout(() => setSearch(searchInput), 350);
    return () => clearTimeout(timeout);
  }, [searchInput]);

  if (isPending) return <PortfolioSkeleton />;

  if (error) return <p>Something went wrong.</p>;

  const handleApplyFilter = (next: LiensFilterValues) => {
    setFilters(next);
  };

  return (
    <>
      <div className="space-y-4">
        <PageHeader
          title="Portfolio"
          subtitle="Manage, monitor, and bundle multiple liens into structured portfolios for sale."
          card={false}
          actions={
            <ActionMenu
              trigger={
                <Button
                  className="bg-[#EE7132] hover:bg-[#EE7132]/90 text-white"
                  rightIcon={<ChevronDown className="h-4 w-4" />}
                  iconDivider
                >
                  Add New Lien
                </Button>
              }
              items={[
                {
                  label: "Add Single Lien",
                  icon: File,
                  onClick: () => router.push("/selling/portfolio/lien/add"),
                },
                {
                  label: "Bulk Upload",
                  icon: CloudUpload,
                  onClick: () => setbulkUpload(true),
                },
              ]}
            />
          }
        />
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          <MetricCard
            label="Total Portfolio Value"
            value={data.summary.totalPortfolioValue}
            description=""
            formatAsCurrency={true}
          />
          <MetricCard
            label="Total Pending"
            value={data.summary.totalPending}
            description=""
            formatAsCurrency={true}
          />

          <MetricCard
            label="Total Internal"
            value={data.summary.totalInternal}
            description=""
            formatAsCurrency={true}
          />
          <MetricCard
            label="Total Sold"
            value={data.summary.totalSold}
            description=""
            formatAsCurrency={true}
          />
        </div>

        <div className="basis-2/4">
          <Tabs
            bordered={false}
            defaultTab={selectedStatus}
            onChange={(e) => setSelectedStatus(e)}
            tabs={PORTFOLIO_STATUSES}
          ></Tabs>
        </div>
        <Card title={`${selectedStatus} Liens`}>
          <div className="bg-white rounded-xl py-3 flex flex-wrap items-center gap-3">
            <div className="relative flex-1 min-w-[300px] max-w-md">
              <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" />
              <input
                type="text"
                placeholder="Search"
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                onFocus={() => setSearchFocused(true)}
                onBlur={() => setSearchFocused(false)}
                className="w-full pl-9 pr-3 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
              />
            </div>
            <Button
              variant="secondary"
              className="border-gray-300"
              leftIcon={<Settings2 className="h-4 w-4" />}
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
          <LiensFilter
            open={showFilter}
            onClose={() => setShowFilter(false)}
            value={filters}
            onApplyFilter={handleApplyFilter}
            primaryReady={bgReady}
          />

          <PortfolioTable
            pagination={paginationData}
            sorting={sorting}
            onSortingChange={setSorting}
            handlePageChange={handlePageChange}
            liens={liens?.items ?? []}
            isLoading={isLiensPending}
            onRowSelect={(id) => router.push(`/selling/portfolio/lien/${id}`)}
            onActionComplete={() => {
              refetchLiens();
              refetch();
            }}
          />
        </Card>

        {bulkUpload && (
          <BulkUploadForm
            open={bulkUpload}
            onClose={() => setbulkUpload(false)}
            onUploaded={() => {
              refetchLiens();
            }}
          ></BulkUploadForm>
        )}
      </div>
    </>
  );
}
