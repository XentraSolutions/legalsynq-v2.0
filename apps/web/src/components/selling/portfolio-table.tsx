"use client";
import Link from "next/link";
import { Archive, Eye, Inbox, Loader2, RotateCcw, Send, SquarePen, Tag, Undo2 } from "lucide-react";
import { DateDisplay } from "@/components/ui/date-display";
import { LIEN_TYPE_LABELS } from "@/types/lien";
import { LienStatusBadge } from "../lien/lien-status-badge";
import { useEffect, useMemo, useState } from "react";
import { ColumnDef, SortingState } from "@tanstack/react-table";
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
import { KNOWN_LIEN_ACTIONS } from "@/components/selling/lien-row-actions-menu";
import { ConfirmDialog } from "@/components/selling/modal";
import { toast } from "sonner";
import {
  TABLE_CELL_CLASSNAME,
  TABLE_LINK_CLASSNAME,
  TABLE_HEADER_CLASSNAME,
  TABLE_HEADER_CELL_CLASSNAME,
} from "@/components/selling/table-cell-styles";

interface PortfolioRowActionsProps {
  lien: LienListItem;
  onActionComplete?: () => void;
}

function PortfolioRowActions({ lien, onActionComplete }: PortfolioRowActionsProps) {
  const router = useRouter();
  const [confirmAction, setConfirmAction] = useState<
    "archive" | "restore" | "keep" | "withdraw-sale" | null
  >(null);
  const [loading, setLoading] = useState(false);

  // The liens list endpoint doesn't reliably populate `availableActions`, so
  // fetch the single-lien detail (which does) the first time the menu opens,
  // and render the sale-lifecycle actions from that instead of guessing.
  const [availableActions, setAvailableActions] = useState<string[] | null>(null);
  const [actionsLoading, setActionsLoading] = useState(false);

  const handleMenuOpenChange = (open: boolean) => {
    if (open && availableActions === null && !actionsLoading) {
      setActionsLoading(true);
      liensService
        .getLienById(lien.lienId)
        .then((detail) => setAvailableActions(detail.availableActions ?? []))
        .catch(() => setAvailableActions([]))
        .finally(() => setActionsLoading(false));
    }
  };

  const canPrepareSale = availableActions?.includes("prepare-sale") ?? false;
  const canConfirmSale = availableActions?.includes("confirm-sale") ?? false;
  const canKeep = availableActions?.includes("keep") ?? false;
  const canWithdrawSale = availableActions?.includes("withdraw-sale") ?? false;
  const canArchive = availableActions?.includes("archive") ?? false;
  const canRestore = availableActions?.includes("restore") ?? false;

  useEffect(() => {
    const unsupported = (availableActions ?? []).filter(
      (action) => !KNOWN_LIEN_ACTIONS.includes(action),
    );
    if (unsupported.length > 0) {
      console.warn(
        `PortfolioRowActions: lien ${lien.lienId} has unsupported action(s): ${unsupported.join(", ")}`,
      );
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [availableActions?.join(",")]);

  const runConfirmAction = async () => {
    if (!confirmAction) return;
    setLoading(true);
    try {
      if (confirmAction === "restore") {
        await liensService.restoreLien(lien.lienId);
        toast.success("Lien restored.");
      } else if (confirmAction === "archive") {
        await liensService.archiveLien(lien.lienId);
        toast.success("Lien archived.");
      } else if (confirmAction === "withdraw-sale") {
        await liensService.withdrawSale(lien.lienId);
        toast.success("Lien withdrawn from sale and returned to Pending.");
      } else {
        await liensService.moveToManagement(lien.lienId, {
          reason: "Retained internally",
        });
        toast.success("Lien kept as internal asset.");
      }
      setConfirmAction(null);
      onActionComplete?.();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Action failed");
    } finally {
      setLoading(false);
    }
  };

  const canEdit = ["Pending", "Internal", "Draft"].includes(lien.status);

  return (
    <>
      <ActionMenu
        onOpenChange={handleMenuOpenChange}
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
          ...(actionsLoading
            ? [
                {
                  label: "Loading actions…",
                  icon: Loader2,
                  disabled: true,
                  onClick: () => {},
                },
              ]
            : []),
          ...(canPrepareSale
            ? [
                {
                  label: "Sell Lien",
                  icon: Tag,
                  onClick: () =>
                    router.push(`/selling/portfolio/lien/${lien.lienId}/sell`),
                },
              ]
            : []),
          ...(canConfirmSale
            ? [
                {
                  label: "Continue Sale",
                  icon: Send,
                  onClick: () =>
                    router.push(`/selling/portfolio/lien/${lien.lienId}/sell`),
                },
              ]
            : []),
          ...(canKeep
            ? [
                {
                  label: "Keep",
                  icon: Inbox,
                  onClick: () => setConfirmAction("keep"),
                },
              ]
            : []),
          ...(canWithdrawSale
            ? [
                {
                  label: "Withdraw from Sale",
                  icon: Undo2,
                  onClick: () => setConfirmAction("withdraw-sale"),
                },
              ]
            : []),
          ...(canRestore
            ? [
                {
                  label: "Restore",
                  icon: RotateCcw,
                  onClick: () => setConfirmAction("restore"),
                },
              ]
            : []),
          ...(canArchive
            ? [
                {
                  label: "Archive",
                  icon: Archive,
                  variant: "danger" as const,
                  onClick: () => setConfirmAction("archive"),
                },
              ]
            : []),
        ]}
      />
      <ConfirmDialog
        open={confirmAction !== null}
        onClose={() => setConfirmAction(null)}
        onConfirm={runConfirmAction}
        loading={loading}
        title={
          confirmAction === "restore"
            ? "Restore This Lien?"
            : confirmAction === "archive"
              ? "Archive This Lien?"
              : confirmAction === "withdraw-sale"
                ? "Withdraw From Sale?"
                : "Keep as Internal Asset?"
        }
        description={
          confirmAction === "restore"
            ? "This lien will be restored to the Pending list for active portfolio tracking."
            : confirmAction === "archive"
              ? "This lien will be hidden from active portfolio lists, but its record and history will be retained."
              : confirmAction === "withdraw-sale"
                ? "This lien will no longer be visible to the buyer and will need to be re-submitted for sale."
                : "This lien will be kept as a private internal asset instead of being offered for sale."
        }
        confirmLabel={
          confirmAction === "restore"
            ? "Restore"
            : confirmAction === "archive"
              ? "Archive"
              : confirmAction === "withdraw-sale"
                ? "Withdraw"
                : "Keep"
        }
        confirmVariant={
          confirmAction === "archive" || confirmAction === "withdraw-sale" ? "danger" : "primary"
        }
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
  onPageSizeChange: (pageSize: number) => void;
  onActionComplete?: () => void;
  isLoading?: boolean;
}

const PAGE_SIZE_OPTIONS = [10, 25, 50, 100];
// Still needed as a passthrough for BaseTable (src/components/ui/base-table),
// which renders a plain <button> (not the selling Button component) and
// exposes primaryButtonClassName as a generic override, not selling-specific.
const PRIMARY_BUTTON_CLASSNAME = "bg-[#EE7132] hover:bg-[#EE7132]/90 text-white";

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
  onPageSizeChange,
  pagination,
  onActionComplete,
  isLoading,
}: PortfolioTableProps) {
  const columns = useMemo<ColumnDef<LienListItem, any>[]>(
    () => [
      {
        id: "createdAtUtc",
        accessorKey: "createdAtUtc",
        header: "Date Created",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>
            <DateDisplay value={row.original.createdAtUtc} format="date" />
          </span>
        ),
      },
      {
        id: "lienId",
        accessorKey: "lienId",
        header: "Lien ID",
        cell: ({ row }) => (
          <Link
            href={`/selling/portfolio/lien/${row.original.lienId}`}
            onClick={(e) => e.stopPropagation()}
            className={TABLE_LINK_CLASSNAME}
          >
            {row.original.lienNumber}
          </Link>
        ),
      },
      {
        id: "fundingCompany",
        header: "Funding Company",
        accessorKey: "fundingCompany",

        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>
            {row.original.fundingCompany || "—"}
          </span>
        ),
      },
      {
        id: "initialServiceDate",
        header: "Initial Service Date",
        accessorKey: "initialServiceDate",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>
            {row.original.initialServiceDate || "—"}
          </span>
        ),
      },
      {
        id: "billingAmount",
        header: "Billing Amount",
        accessorKey: "billingAmount",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>
            {formatCurrency(row.original.billingAmount) || "—"}
          </span>
        ),
      },
      {
        id: "askAmount",
        header: "Ask Amount",
        accessorKey: "askAmount",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>
            {formatCurrency(row.original.askAmount) || "—"}
          </span>
        ),
      },
      {
        id: "status",
        accessorKey: "status",
        header: "Lien Status",
        cell: ({ row }) => <LienStatusBadge status={row.original.status} />,
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
    <div className="bg-white overflow-hidden">
      <BaseTable
        data={liens}
        columns={columns}
        getRowId={(l) => l.lienId}
        isLoading={isLoading}
        sorting={sorting}
        onSortingChange={onSortingChange}
        manualSorting
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
          if (next.pageSize !== pagination.pageSize) {
            onPageSizeChange(next.pageSize);
          }
        }}
        pageSizeOptions={PAGE_SIZE_OPTIONS}
        className="bg-white border-0 rounded-none"
        primaryButtonClassName={PRIMARY_BUTTON_CLASSNAME}
        headerClassName={TABLE_HEADER_CLASSNAME}
        headerCellClassName={TABLE_HEADER_CELL_CLASSNAME}
      />
    </div>
  );
}
