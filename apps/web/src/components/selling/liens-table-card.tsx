"use client";

import { useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { CloudUpload, File, Search } from "lucide-react";
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
  type LiensFilterValues,
} from "@/app/(platform)/selling/liens/components/liens-filter";
import { BulkUploadForm } from "@/components/selling/forms/bulk-upload-form";
import type { LienListItem, PaginationMeta } from "@/lib/selling";

const STATUS_OPTIONS = [
  { key: "", label: "All Statuses" },
  { key: "Pending", label: "Pending" },
  { key: "Internal", label: "Internal" },
  { key: "Sold", label: "Sold" },
  { key: "Archived", label: "Archived" },
];

function countActiveFilters(f: LiensFilterValues): number {
  return (
    (f.fundingCompanyIds.length ? 1 : 0) +
    (f.initialServiceDateFrom || f.initialServiceDateTo ? 1 : 0)
  );
}

interface LiensTableCardProps {
  title: string;
  search: string;
  onSearchChange: (value: string) => void;
  onSearchFocus?: () => void;
  onSearchBlur?: () => void;
  status: string;
  onStatusChange: (value: string) => void;
  filters: LiensFilterValues;
  onApplyFilter: (value: LiensFilterValues) => void;
  primaryReady?: boolean;
  liens: LienListItem[];
  isLoading?: boolean;
  sorting: SortingState;
  onSortingChange: (updater: any) => void;
  pagination: PaginationMeta;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
  onActionComplete?: () => void;
  // Contextual navigation: when rendered inside a case detail page, "Add
  // Single Lien" and bulk upload should stay scoped to that case.
  caseId?: string;
  caseCode?: string;
  onBulkUploaded?: () => void;
}

export function LiensTableCard({
  title,
  search,
  onSearchChange,
  onSearchFocus,
  onSearchBlur,
  status,
  onStatusChange,
  filters,
  onApplyFilter,
  primaryReady,
  liens,
  isLoading,
  sorting,
  onSortingChange,
  pagination,
  onPageChange,
  onPageSizeChange,
  onActionComplete,
  caseId,
  caseCode,
  onBulkUploaded,
}: LiensTableCardProps) {
  const router = useRouter();
  const [showFilter, setShowFilter] = useState(false);
  const [bulkUpload, setBulkUpload] = useState(false);
  const activeFilterCount = useMemo(() => countActiveFilters(filters), [filters]);

  const addSingleLienHref = caseId
    ? `/selling/portfolio/lien/add?caseId=${caseId}`
    : "/selling/portfolio/lien/add";

  return (
    <>
      <Card title={title}>
        <div className="bg-white rounded-xl py-3 flex flex-wrap items-center justify-between gap-3">
          <div className="flex flex-wrap items-center gap-3">
            <div className="relative flex-1 min-w-[300px] max-w-md">
              <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" />
              <input
                type="text"
                placeholder="Search"
                value={search}
                onChange={(e) => onSearchChange(e.target.value)}
                onFocus={onSearchFocus}
                onBlur={onSearchBlur}
                className="w-full pl-9 pr-3 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
              />
            </div>
            <Select
              value={status || "all"}
              onValueChange={(v) => onStatusChange(v === "all" ? "" : v)}
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
                onClick: () => router.push(addSingleLienHref),
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
          onApplyFilter={onApplyFilter}
          primaryReady={primaryReady}
        />

        <PortfolioTable
          liens={liens}
          isLoading={isLoading}
          sorting={sorting}
          onSortingChange={onSortingChange}
          pagination={pagination}
          handlePageChange={onPageChange}
          onPageSizeChange={onPageSizeChange}
          onActionComplete={onActionComplete}
        />
      </Card>

      {bulkUpload && (
        <BulkUploadForm
          open={bulkUpload}
          onClose={() => setBulkUpload(false)}
          referenceType={caseId ? "Case" : undefined}
          referenceId={caseId}
          caseCode={caseCode}
          onUploaded={onBulkUploaded}
        />
      )}
    </>
  );
}
