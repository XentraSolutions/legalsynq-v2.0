"use client";

import Link from "next/link";
import { useMemo } from "react";
import { useRouter } from "next/navigation";
import { ColumnDef, SortingState } from "@tanstack/react-table";
import { Eye } from "lucide-react";
import { BaseTable } from "../ui/base-table";
import { StatusBadge } from "@/components/lien/status-badge";
import { DateDisplay } from "@/components/ui/date-display";
import type { CaseSearchItem } from "@/lib/selling";
import { PaginationMeta } from "@/lib/selling";
import { ActionMenu, type ActionMenuItem } from "@/components/selling/action-menu";
import {
  TABLE_CELL_CLASSNAME,
  TABLE_LINK_CLASSNAME,
  TABLE_HEADER_CLASSNAME,
  TABLE_HEADER_CELL_CLASSNAME,
} from "@/components/selling/table-cell-styles";

interface CasesTableProps {
  cases: CaseSearchItem[];
  sorting: SortingState;
  onSortingChange: (e: any) => void;
  pagination: PaginationMeta;
  handlePageChange: (e: any) => void;
  onPageSizeChange: (pageSize: number) => void;
  isLoading?: boolean;
}

const PAGE_SIZE_OPTIONS = [10, 25, 50, 100];

export function CasesTable({
  cases,
  sorting,
  onSortingChange,
  handlePageChange,
  onPageSizeChange,
  pagination,
  isLoading,
}: CasesTableProps) {
  const router = useRouter();

  const columns = useMemo<ColumnDef<CaseSearchItem, any>[]>(
    () => [
      {
        id: "caseNumber",
        accessorKey: "caseNumber",
        header: "Case ID",
        cell: ({ row }) => (
          <Link
            href={`/selling/portfolio/cases/${row.original.caseId}`}
            onClick={(e) => e.stopPropagation()}
            className={TABLE_LINK_CLASSNAME}
          >
            {row.original.caseNumber}
          </Link>
        ),
      },
      {
        id: "firstName",
        accessorKey: "firstName",
        header: "First Name",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>
            {row.original.firstName || "—"}
          </span>
        ),
      },
      {
        id: "lastName",
        accessorKey: "lastName",
        header: "Last Name",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>
            {row.original.lastName || "—"}
          </span>
        ),
      },
      {
        id: "handlingLawFirm",
        accessorKey: "handlingLawFirmName",
        header: "Law Firm",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>
            {row.original.handlingLawFirmName || "—"}
          </span>
        ),
      },
      {
        id: "caseManager",
        accessorKey: "caseManagerName",
        header: "Case Manager",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>
            {row.original.caseManagerName || "—"}
          </span>
        ),
      },
      {
        id: "accidentType",
        accessorKey: "accidentTypeName",
        header: "Accident Type",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>
            {row.original.accidentTypeName || "—"}
          </span>
        ),
      },
      {
        id: "dateOfLoss",
        accessorKey: "dateOfLoss",
        header: "Date of Loss",
        cell: ({ row }) => (
          <DateDisplay value={row.original.dateOfLoss} format="date" />
        ),
      },
      {
        id: "birthdate",
        accessorKey: "birthdate",
        header: "Date of Birth",
        cell: ({ row }) => (
          <DateDisplay value={row.original.birthdate} format="date" />
        ),
      },
      {
        id: "caseStatus",
        accessorKey: "caseStatus",
        header: "Status",
        cell: ({ row }) => <StatusBadge status={row.original.caseStatus} />,
      },
      {
        id: "actions",
        header: "",
        cell: ({ row }) => {
          const items: ActionMenuItem[] = [
            {
              label: "View",
              icon: Eye,
              onClick: () =>
                router.push(`/selling/portfolio/cases/${row.original.caseId}`),
            },
            // Delete disabled until the delete-case API is available.
            // {
            //   label: "Delete",
            //   icon: Trash2,
            //   variant: "danger",
            //   onClick: () => {},
            // },
          ];
          return <ActionMenu items={items} />;
        },
      },
    ],
    [router],
  );

  return (
    <div className="bg-white overflow-hidden">
      <BaseTable
        data={cases}
        columns={columns}
        getRowId={(c) => c.caseId}
        isLoading={isLoading}
        sorting={sorting}
        onSortingChange={onSortingChange}
        manualSorting
        emptyMessage="No cases yet."
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
          if (next.pageSize !== pagination.pageSize) {
            onPageSizeChange(next.pageSize);
          }
        }}
        pageSizeOptions={PAGE_SIZE_OPTIONS}
        className="bg-white border-0 rounded-none"
        headerClassName={TABLE_HEADER_CLASSNAME}
        headerCellClassName={TABLE_HEADER_CELL_CLASSNAME}
      />
    </div>
  );
}
