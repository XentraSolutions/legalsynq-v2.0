"use client";
import Link from "next/link";
import { Archive, Eye, RotateCcw, SquarePen } from "lucide-react";
import { DateDisplay } from "@/components/ui/date-display";
import { LIEN_TYPE_LABELS } from "@/types/lien";
import { LienStatusBadge } from "../lien/lien-status-badge";
import { useMemo, useState } from "react";
import { ColumnDef, SortingState } from "@tanstack/react-table";
import { StatusBadge } from "../lien/status-badge";
import { BaseTable } from "../ui/base-table";
import { PaginationMeta } from "@/lib/contacts";
import { LiensQuery } from "@/lib/liens";
import {
  EMPTY_LIENS_FILTERS,
  LiensFilterValues,
} from "@/app/(platform)/selling/liens/components/liens-filter";
import { LienListItem, liensService } from "@/lib/selling";
import { useRouter } from "next/navigation";
import { ActionMenu } from "@/components/selling/action-menu";
import { ConfirmDialog } from "@/components/selling/modal";
import { useToast } from "@/lib/toast-context";

interface PortfolioRowActionsProps {
  lien: LienListItem;
  onActionComplete?: () => void;
}

function PortfolioRowActions({ lien, onActionComplete }: PortfolioRowActionsProps) {
  const router = useRouter();
  const { show: showToast } = useToast();
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const isArchived = lien.status === "Archived" || lien.sellerStatus === "Archived";

  const handleArchiveToggle = async () => {
    setLoading(true);
    try {
      if (isArchived) {
        await liensService.restoreLien(lien.lienId);
        showToast("Lien restored.", "success");
      } else {
        await liensService.archiveLien(lien.lienId);
        showToast("Lien archived.", "success");
      }
      setConfirmOpen(false);
      onActionComplete?.();
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Action failed", "error");
    } finally {
      setLoading(false);
    }
  };

  const canEdit = ["Pending", "Internal", "Draft"].includes(lien.status);

  return (
    <>
      <ActionMenu
        items={[
          {
            label: "View",
            icon: Eye,
            onClick: () => router.push(`/selling/portfolio/lien/${lien.lienId}`),
          },
          ...(canEdit
            ? [
                {
                  label: "Edit",
                  icon: SquarePen,
                  onClick: () =>
                    router.push(`/selling/portfolio/lien/${lien.lienId}/edit`),
                },
              ]
            : []),
          isArchived
            ? {
                label: "Restore",
                icon: RotateCcw,
                onClick: () => setConfirmOpen(true),
              }
            : {
                label: "Archive",
                icon: Archive,
                variant: "danger",
                onClick: () => setConfirmOpen(true),
              },
        ]}
      />
      <ConfirmDialog
        open={confirmOpen}
        onClose={() => setConfirmOpen(false)}
        onConfirm={handleArchiveToggle}
        loading={loading}
        title={isArchived ? "Restore This Lien?" : "Archive This Lien?"}
        description={
          isArchived
            ? "This lien will be restored to the Pending list for active portfolio tracking."
            : "This lien will be hidden from active portfolio lists, but its record and history will be retained."
        }
        confirmLabel={isArchived ? "Restore" : "Archive"}
        confirmVariant={isArchived ? "primary" : "danger"}
      />
    </>
  );
}

interface PortfolioTableProps {
  liens: LienListItem[];
  sorting: SortingState;
  onSortingChange: (e: any) => void;
  pagination: PaginationMeta;
  handlePageChange: (e: any) => void;
  onRowSelect: (id: string) => void;
  onActionComplete?: () => void;
  isLoading?: boolean;
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
  onActionComplete,
  isLoading,
}: PortfolioTableProps) {
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
            {formatCurrency(row.original.billingAmount) || "—"}
          </span>
        ),
      },
      {
        id: "askAmount",
        header: "Ask Amount",
        accessorKey: "askAmount",
        cell: ({ row }) => (
          <span className="text-sm text-gray-700">
            {formatCurrency(row.original.askAmount) || "—"}
          </span>
        ),
      },
      {
        id: "status",
        accessorKey: "status",
        header: "Lien Status",
        cell: ({ row }) => <StatusBadge status={row.original.status} />,
      },
      {
        id: "actions",
        header: "",
        enableSorting: false,
        cell: ({ row }) => (
          <div className="flex justify-end">
            <PortfolioRowActions
              lien={row.original}
              onActionComplete={onActionComplete}
            />
          </div>
        ),
      },
    ],
    [onActionComplete],
  );

  return (
    <div className="bg-white rounded-lg overflow-hidden">
      <BaseTable
        data={liens}
        columns={columns}
        getRowId={(l) => l.lienId}
        isLoading={isLoading}
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
