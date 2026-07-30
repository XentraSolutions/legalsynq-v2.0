"use client";
import Link from "next/link";
import { DateDisplay } from "@/components/ui/date-display";
import { LIEN_TYPE_LABELS } from "@/types/lien";
import { LienStatusBadge } from "./lien-status-badge";
import { useMemo, useState } from "react";
import { ColumnDef, SortingState } from "@tanstack/react-table";
import { StatusBadge } from "./status-badge";
import { BaseTable } from "../ui/base-table";
import { PaginationMeta } from "@/lib/contacts";
import { LiensQuery } from "@/lib/liens";
import {
  EMPTY_LIENS_FILTERS,
  LiensFilterValues,
} from "@/app/(platform)/selling/liens/components/liens-filter";
import { LienListItem } from "@/lib/selling";
import { useRouter } from "next/navigation";

interface PortfolioTableProps {
  liens: LienListItem[];
  sorting: SortingState;
  onSortingChange: (e: any) => void;
  pagination: PaginationMeta;
  handlePageChange: (e: any) => void;
  onRowSelect: (id: string) => void;
}

function formatCurrency(amount?: number): string {
  if (amount == null) return "—";
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
    maximumFractionDigits: 0,
  }).format(amount);
}

export function PortfolioTable({
  liens,
  sorting,
  onSortingChange,
  handlePageChange,
  pagination,
  onRowSelect,
}: PortfolioTableProps) {
  const router = useRouter();
  const columns = useMemo<ColumnDef<LienListItem, any>[]>(
    () => [
      {
        id: "lienId",
        accessorKey: "lienId",
        header: "Lien ID",
        cell: ({ row }) => (
          <span className="text-xs font-mono text-gray-700">
            {row.original.lienNumber}
          </span>
        ),
      },
      {
        id: "fundingCompany",
        header: "Funding Company",
        accessorKey: "fundingCompany",

        cell: ({ row }) => (
          <span className="text-sm text-gray-700">
            {row.original.fundingCompany || "—"}
          </span>
        ),
      },
      {
        id: "initialServiceDate",
        header: "Initial Service Date",
        accessorKey: "initialServiceDate",
        cell: ({ row }) => (
          <span className="text-sm text-gray-700">
            {row.original.initialServiceDate || "—"}
          </span>
        ),
      },
      {
        id: "billingAmount",
        header: "Billing Amount",
        accessorKey: "billingAmount",
        cell: ({ row }) => (
          <span className="text-sm text-gray-700">
            {row.original.billingAmount || "—"}
          </span>
        ),
      },
      {
        id: "askAmount",
        header: "Ask Amount",
        accessorKey: "askAmount",
        cell: ({ row }) => (
          <span className="text-sm text-gray-700">
            {row.original.askAmount || "—"}
          </span>
        ),
      },
      {
        id: "status",
        accessorKey: "status",
        header: "Lien Status",
        cell: ({ row }) => <StatusBadge status={row.original.status} />,
      },
    ],
    [],
  );

  return (
    <div className="bg-white rounded-lg overflow-hidden">
      <BaseTable
        data={liens}
        columns={columns}
        getRowId={(l) => l.lienId}
        sorting={sorting}
        onSortingChange={onSortingChange}
        manualSorting
        onRowClick={(l) => onRowSelect(l.lienId)}
        emptyMessage="No liens match your filters."
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
          handlePageChange(next.pageIndex + 1);
        }}
        className="bg-white border-gray-200 rounded-xl"
      />
    </div>
  );
}
