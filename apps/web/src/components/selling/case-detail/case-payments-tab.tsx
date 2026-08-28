"use client";

import { useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import type { ColumnDef, PaginationState, SortingState } from "@tanstack/react-table";
import { useQueryClient } from "@tanstack/react-query";
import { Search, Settings2, Trash2, X } from "lucide-react";
import { toast } from "sonner";
import { BaseTable } from "@/components/ui/base-table";
import { BaseSelect } from "@/components/ui/base-select";
import { Card } from "@/components/ui/dashboard-card";
import { buttonVariants } from "@legalsynq/design-system";
import { ActionMenu, type ActionMenuItem } from "@/components/selling/action-menu";
import { ConfirmDialog } from "@/components/selling/modal";
import { Button } from "@/components/selling/button";
import { cn } from "@/lib/utils";
import {
  TABLE_CELL_CLASSNAME,
  TABLE_HEADER_CLASSNAME,
  TABLE_HEADER_CELL_CLASSNAME,
  TABLE_LINK_CLASSNAME,
} from "@/components/selling/table-cell-styles";
import { ApiError } from "@/lib/api-client";
import { lienPaymentsService } from "@/lib/selling/lien-payments.service";
import {
  PAYMENT_METHOD_OPTIONS,
  formatCurrency,
  formatDate,
} from "@/components/selling/lien-detail/payment-tab";
import type { LienPaymentItem } from "@/lib/selling/lien-payments.types";
import { useLienPayments } from "@/lib/selling/use-lien-payments";

const PRIMARY_BUTTON_CLASSNAME = "bg-[#EE7132] hover:bg-[#EE7132]/90 text-white";

export function CasePaymentsTab({ caseId }: { caseId: string }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [methodFilter, setMethodFilter] = useState("");
  const [sorting, setSorting] = useState<SortingState>([
    { id: "paymentDate", desc: true },
  ]);
  const [pagination, setPagination] = useState<PaginationState>({
    pageIndex: 0,
    pageSize: 10,
  });
  const [voidTarget, setVoidTarget] = useState<LienPaymentItem | null>(null);
  const [voiding, setVoiding] = useState(false);

  const activeSort = sorting[0] ?? { id: "paymentDate", desc: true };
  const paymentsQuery = useLienPayments(caseId, {
    search: search.trim() || undefined,
    paymentMethod: methodFilter || undefined,
    postingStatus: "Posted",
    sortBy: activeSort.id,
    sortDirection: activeSort.desc ? "desc" : "asc",
    page: pagination.pageIndex + 1,
    pageSize: pagination.pageSize,
  });
  const response = paymentsQuery.data;
  const summary = response?.summary;

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ["lien-payments", caseId] });
  };

  const confirmVoid = async () => {
    if (!voidTarget) return;
    setVoiding(true);
    try {
      await lienPaymentsService.voidLienPayment(
        caseId,
        voidTarget.id,
        "Deleted from case payments tab",
      );
      toast.success("Payment record deleted.");
      setVoidTarget(null);
      refresh();
    } catch (err) {
      toast.error(
        err instanceof ApiError ? err.message : "Failed to delete payment record",
      );
    } finally {
      setVoiding(false);
    }
  };

  const paymentActions = useMemo(
    () =>
      (payment: LienPaymentItem): ActionMenuItem[] => [
        {
          label: "Delete",
          icon: Trash2,
          variant: "danger",
          onClick: () => setVoidTarget(payment),
        },
      ],
    [],
  );

  const columns = useMemo<ColumnDef<LienPaymentItem>[]>(
    () => [
      {
        accessorKey: "lienNumber",
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
        accessorKey: "paymentDate",
        header: "Payment Date",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>
            {formatDate(row.original.paymentDate)}
          </span>
        ),
      },
      {
        accessorKey: "paymentMethod",
        header: "Payment Method",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>{row.original.paymentMethod}</span>
        ),
      },
      {
        accessorKey: "referenceNumber",
        header: "Reference / ID #",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>
            {row.original.referenceNumber || "—"}
          </span>
        ),
      },
      {
        accessorKey: "amount",
        header: "Payment Amount",
        cell: ({ row }) => (
          <span className={`${TABLE_CELL_CLASSNAME} font-medium tabular-nums`}>
            {formatCurrency(row.original.amount)}
          </span>
        ),
      },
      {
        accessorKey: "notes",
        header: "Notes",
        cell: ({ row }) => (
          <span
            className={`${TABLE_CELL_CLASSNAME} block max-w-56 truncate`}
            title={row.original.notes ?? undefined}
          >
            {row.original.notes || "—"}
          </span>
        ),
      },
      {
        id: "actions",
        header: "",
        enableSorting: false,
        cell: ({ row }) => (
          <div className="flex justify-end">
            <ActionMenu items={paymentActions(row.original)} />
          </div>
        ),
        meta: { align: "right", width: "56px" },
      },
    ],
    [paymentActions],
  );

  const summaryCards = [
    { label: "Total Ask Amount", value: formatCurrency(summary?.lienSellingAmount ?? 0) },
    { label: "Total Paid", value: formatCurrency(summary?.totalPaid ?? 0) },
    { label: "Remaining Balance", value: formatCurrency(summary?.remainingBalance ?? 0) },
  ];

  return (
    <div className="space-y-5">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        {summaryCards.map((card) => (
          <div
            key={card.label}
            className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm"
          >
            <p className="text-xs text-gray-400">{card.label}</p>
            <p className="mt-2 text-2xl font-bold text-gray-900">{card.value}</p>
          </div>
        ))}
      </div>

      {paymentsQuery.isError && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {paymentsQuery.error instanceof ApiError
            ? paymentsQuery.error.message
            : "Failed to load payments."}
        </div>
      )}

      <Card title="Payment Information">
        <BaseTable
          data={response?.items ?? []}
          columns={columns}
          getRowId={(payment) => payment.id}
          isLoading={paymentsQuery.isLoading}
          emptyMessage="No payments have been recorded for this case."
          sorting={sorting}
          onSortingChange={(updater) => {
            setSorting((current) => (typeof updater === "function" ? updater(current) : updater));
            setPagination((current) => ({ ...current, pageIndex: 0 }));
          }}
          manualSorting
          manualFiltering
          manualPagination
          pageCount={Math.max(1, Math.ceil((response?.totalCount ?? 0) / pagination.pageSize))}
          totalCount={response?.totalCount ?? 0}
          pagination={pagination}
          onPaginationChange={(updater) =>
            setPagination((current) => (typeof updater === "function" ? updater(current) : updater))
          }
          pageSizeOptions={[10, 25, 50]}
          className="border-0 rounded-none"
          primaryButtonClassName={PRIMARY_BUTTON_CLASSNAME}
          headerClassName={TABLE_HEADER_CLASSNAME}
          headerCellClassName={TABLE_HEADER_CELL_CLASSNAME}
          toolbar={
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
                      setPagination((current) => ({ ...current, pageIndex: 0 }));
                    }}
                    className="w-full pl-9 pr-3 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
                  />
                </div>
                <BaseSelect
                  value={methodFilter}
                  onChange={(value) => {
                    setMethodFilter(value);
                    setPagination((current) => ({ ...current, pageIndex: 0 }));
                  }}
                  options={PAYMENT_METHOD_OPTIONS}
                  placeholder="Filter"
                  clearable
                  className={cn(buttonVariants({ variant: "secondary" }), "border-gray-300")}
                  contentClassName="w-56"
                  triggerContent={({ selectedLabel, onClear }) => (
                    <>
                      <Settings2 className="h-4 w-4" />
                      <span className="truncate max-w-[140px]">
                        {selectedLabel || "Filter"}
                      </span>
                      {selectedLabel && (
                        <X
                          aria-label="Clear selection"
                          className="h-3.5 w-3.5 text-gray-400 hover:text-gray-600"
                          onClick={onClear}
                        />
                      )}
                    </>
                  )}
                />
              </div>
              <Button
                variant="secondary"
                rightIcon="squarePen"
                onClick={() =>
                  router.push(`/selling/portfolio/cases/${caseId}/payments/add`)
                }
              >
                Add Payment
              </Button>
            </div>
          }
        />
      </Card>

      {voidTarget && (
        <ConfirmDialog
          open
          onClose={() => setVoidTarget(null)}
          onConfirm={confirmVoid}
          loading={voiding}
          title="Delete Payment Record?"
          description="Are you sure you want to delete this payment record? This action cannot be undone and will void the payment, permanently removing it from the system."
          confirmLabel="Yes, Delete"
          confirmVariant="danger"
        />
      )}
    </div>
  );
}
