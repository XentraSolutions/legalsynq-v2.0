// app/portfolio/PortfolioClient.tsx

"use client";

import { useQuery } from "@tanstack/react-query";
import { LiensQuery, liensService } from "@/lib/selling";
import { useEffect, useMemo, useState } from "react";
import { MetricCard } from "./dashboard/metric-card";
import { Tabs } from "./tabs";
import { LiensTableCard } from "./liens-table-card";
import {
  LiensFilterValues,
  EMPTY_LIENS_FILTERS,
} from "@/app/(platform)/selling/liens/components/liens-filter";
import {
  useBackgroundReady,
  usePrimaryLoad,
} from "@/hooks/use-background-queue";
import { useRouter } from "next/navigation";
import { PaginationMeta } from "@/lib/liens";
import { SortingState } from "@tanstack/react-table";
import { PageHeader } from "@/components/lien/page-header";
import { SkeletonCard, SkeletonTable } from "@/components/lien/skeleton-loader";

// Keys must match PortfolioTable's column ids (portfolio-table.tsx). Values
// mirror the sortBy convention SellingDashboardService.Sort already accepts
// (lienId, fundingCompany, initialServiceDate, billingAmount, askAmount,
// highestBid, status) — createdAtUtc isn't handled there yet, sent anyway on
// the assumption the backend will add support for it.
const SORT_BY_MAP: Record<string, string> = {
  createdAtUtc: "createdAtUtc",
  lienId: "lienId",
  fundingCompany: "fundingCompany",
  initialServiceDate: "initialServiceDate",
  billingAmount: "billingAmount",
  askAmount: "askAmount",
  status: "status",
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

  const [selectedStatus, setSelectedStatus] = useState<string>("Pending");
  const [filters, setFilters] =
    useState<LiensFilterValues>(EMPTY_LIENS_FILTERS);
  const [sorting, setSorting] = useState<SortingState>([]);
  const [pagination, setPagination] = useState<PaginationMeta>({
    page: 1,
    pageSize: 10,
    totalCount: 0,
    totalPages: 0,
  });

  const currentQuery = useMemo(
    (): LiensQuery => ({
      tab: selectedStatus || undefined,
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

  const handlePageSizeChange = (newPageSize: number) => {
    setPagination((prev) => ({ ...prev, page: 1, pageSize: newPageSize }));
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
        />
        <Tabs
          bordered={false}
          defaultTab="liens"
          tabs={[
            { key: "cases", label: "Cases" },
            { key: "liens", label: "Liens" },
          ]}
          onChange={(tab) => {
            if (tab === "cases") router.push("/selling/portfolio/cases");
          }}
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

        <LiensTableCard
          title="All Liens"
          search={searchInput}
          onSearchChange={setSearchInput}
          status={selectedStatus}
          onStatusChange={setSelectedStatus}
          filters={filters}
          onApplyFilter={handleApplyFilter}
          primaryReady={bgReady}
          liens={liens?.items ?? []}
          isLoading={isLiensPending}
          sorting={sorting}
          onSortingChange={setSorting}
          pagination={paginationData}
          onPageChange={handlePageChange}
          onPageSizeChange={handlePageSizeChange}
          onActionComplete={() => {
            refetchLiens();
            refetch();
          }}
          onBulkUploaded={() => refetchLiens()}
        />
      </div>
    </>
  );
}
