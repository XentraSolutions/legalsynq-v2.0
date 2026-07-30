// app/portfolio/PortfolioClient.tsx

"use client";

import { useQuery } from "@tanstack/react-query";
import { LiensQuery, liensService } from "@/lib/selling";
import { useCallback, useEffect, useMemo, useState } from "react";
import { MetricCard } from "./dashboard/metric-card";
import { Tabs } from "../ui/tabs";
import { PortfolioTable } from "../lien/portfolio-table";
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

const PORTFOLIO_STATUSES = [
  { key: "Pending", label: "Pending" },
  { key: "Internal", label: "Internal" },
  { key: "Sold", label: "Sold" },
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
  const [actionOpen, setActionOpen] = useState(false);
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
      (f.lienStatusIds.length ? 1 : 0) +
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
  }, [currentQuery]);

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

  if (isPending) return <p>Loading...</p>;

  if (error) return <p>Something went wrong.</p>;

  const handleApplyFilter = (next: LiensFilterValues) => {
    setFilters(next);
  };

  return (
    <>
      <div className="space-y-4">
        <div className="flex justify-between">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Portfolio</h1>
            <p className="text-md text-[#737373]">
              Manage, monitor, and bundle multiple liens into structured
              portfolios for sale.
            </p>
          </div>

          <div className="flex items-end">
            <div className="relative">
              {/* Dropdown Button */}
              <button
                onClick={() => {
                  setActionOpen(!actionOpen);
                }}
                className="flex items-center justify-between gap-1.5 text-sm font-medium text-center text-white bg-[#EE7132] hover:bg-[#EE7132]/90 rounded-lg px-2 py-2 w-35 transition-colors"
              >
                Add New Lien
                <i className="ri-arrow-down-s-line text-base" />
              </button>
              {/* Dropdown Menu */}
              {actionOpen && (
                <div className="absolute right-0 mt-2 w-48 bg-white border border-gray-200 rounded-lg shadow-lg z-50">
                  <button
                    onClick={() => {
                      router.push("add-liens");
                      setActionOpen(false);
                    }}
                    className="w-full text-left px-4 py-2 text-sm hover:bg-gray-100"
                  >
                    <i className="ri-file-line mr-2"></i>
                    Add Single Lien
                  </button>
                  <button
                    onClick={() => {
                      setbulkUpload(true);
                      setActionOpen(false);
                    }}
                    className="w-full text-left px-4 py-2 text-sm hover:bg-gray-100"
                  >
                    <i className="ri-upload-cloud-2-line mr-2"></i>
                    Bulk Upload
                  </button>
                </div>
              )}
            </div>
          </div>
        </div>
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
            <div className="relative min-w-[300px]">
              <i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm" />
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
            <button
              onClick={() => setShowFilter(true)}
              className="relative min-w-[150px] flex items-center gap-1.5 text-sm font-medium text-gray-700 border border-gray-300 rounded-lg px-4 py-2 hover:bg-gray-50 transition-colors"
            >
              <i className="ri-filter-3-line text-base" />
              Filter
              {activeFilterCount > 0 && (
                <span className="ml-0.5 inline-flex items-center justify-center h-4 min-w-4 px-1 rounded-full bg-primary text-white text-[10px] font-semibold">
                  {activeFilterCount}
                </span>
              )}
            </button>
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
            onRowSelect={(id) => router.push(`portfolio/${id}`)}
          />
          {/* {data.items && <PortfolioTable liens={data.items} />} */}
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
