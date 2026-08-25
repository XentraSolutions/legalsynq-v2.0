"use client";

import { useMemo, useState } from "react";
import type { ColumnDef, PaginationState, SortingState } from "@tanstack/react-table";
import { useQueryClient } from "@tanstack/react-query";
import { BaseTable } from "@/components/ui/base-table";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { FormModal } from "@/components/lien/modal";
import { ApiError } from "@/lib/api-client";
import { settlementService } from "@/lib/settlement";
import type { CasePaymentItem } from "@/lib/settlement/settlement.types";
import { useCasePayments } from "@/hooks/use-case-payments";
import { useCaseLiens } from "@/hooks/use-case-liens";
import { useLienStore } from "@/stores/lien-store";
import { AddPaymentForm } from "../../components/add-payment-form";

const PAYMENT_METHODS = ["Check", "ACH", "Wire", "Cash", "Other"];

function formatCurrency(value: number): string {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
  }).format(value);
}

function formatDate(value: string | null): string {
  if (!value) return "—";
  const [year, month, day] = value.slice(0, 10).split("-").map(Number);
  return new Intl.DateTimeFormat("en-US", {
    month: "2-digit",
    day: "2-digit",
    year: "numeric",
  }).format(new Date(year, month - 1, day));
}

export function PaymentsTab({ caseId, canEdit }: { caseId: string; canEdit: boolean }) {
  const addToast = useLienStore((state) => state.addToast);
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [paymentMethod, setPaymentMethod] = useState("all");
  const [sorting, setSorting] = useState<SortingState>([
    { id: "paymentDate", desc: true },
  ]);
  const [pagination, setPagination] = useState<PaginationState>({
    pageIndex: 0,
    pageSize: 10,
  });
  const [showAddPayment, setShowAddPayment] = useState(false);
  const [voidTarget, setVoidTarget] = useState<CasePaymentItem | null>(null);
  const [voidReason, setVoidReason] = useState("");
  const [voiding, setVoiding] = useState(false);

  const activeSort = sorting[0] ?? { id: "paymentDate", desc: true };
  const paymentsQuery = useCasePayments(caseId, {
    search: search.trim() || undefined,
    paymentMethod: paymentMethod === "all" ? undefined : paymentMethod,
    sortBy: activeSort.id as "paymentDate" | "paymentMethod" | "amount",
    sortDirection: activeSort.desc ? "desc" : "asc",
    page: pagination.pageIndex + 1,
    pageSize: pagination.pageSize,
  });
  const allLiensQuery = useCaseLiens(caseId, {}, "all-liens");
  const response = paymentsQuery.data;
  const summary = response?.summary;

  const refreshPaymentData = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["case-payment-ledger", caseId] }),
      queryClient.invalidateQueries({ queryKey: ["case-payments", caseId] }),
      queryClient.invalidateQueries({ queryKey: ["settlement-payment-details", caseId] }),
      queryClient.invalidateQueries({ queryKey: ["case-liens-all", caseId] }),
      queryClient.invalidateQueries({ queryKey: ["case-liens", caseId] }),
      queryClient.invalidateQueries({ queryKey: ["caseDetail", caseId] }),
    ]);
  };

  const confirmVoid = async () => {
    if (!voidTarget || !voidReason.trim()) return;
    setVoiding(true);
    try {
      await settlementService.voidCasePayment(caseId, voidTarget.id, voidReason.trim());
      addToast({
        type: "success",
        title: "Payment Voided",
        description: `Payment ${voidTarget.paymentNumber} was removed from posted totals.`,
      });
      setVoidTarget(null);
      setVoidReason("");
      await refreshPaymentData();
    } catch (error) {
      addToast({
        type: "error",
        title: "Void Failed",
        description: error instanceof ApiError ? error.message : "Failed to void payment.",
      });
    } finally {
      setVoiding(false);
    }
  };

  const columns = useMemo<ColumnDef<CasePaymentItem>[]>(
    () => [
      {
        accessorKey: "lienNumber",
        header: "Lien ID",
        enableSorting: false,
        cell: ({ row }) => <span className="font-medium text-primary">{row.original.lienNumber}</span>,
      },
      {
        accessorKey: "paymentDate",
        header: "Payment Date",
        cell: ({ row }) => formatDate(row.original.paymentDate),
      },
      { accessorKey: "paymentMethod", header: "Payment Method" },
      {
        accessorKey: "referenceNumber",
        header: "Reference / ID #",
        enableSorting: false,
        cell: ({ row }) => row.original.referenceNumber || "—",
      },
      {
        accessorKey: "amount",
        header: "Payment Amount",
        meta: { align: "right" },
        cell: ({ row }) => (
          <span className="font-medium tabular-nums">{formatCurrency(row.original.amount)}</span>
        ),
      },
      {
        accessorKey: "detailsContext",
        header: "Details / Context",
        enableSorting: false,
        cell: ({ row }) => row.original.detailsContext || "—",
      },
      {
        accessorKey: "notes",
        header: "Notes",
        enableSorting: false,
        cell: ({ row }) => (
          <span className="block max-w-64 truncate" title={row.original.notes ?? undefined}>
            {row.original.notes || "—"}
          </span>
        ),
      },
      {
        accessorKey: "postingStatus",
        header: "Status",
        enableSorting: false,
        cell: ({ row }) => (
          <span className={row.original.postingStatus === "Voided"
            ? "inline-flex rounded-full bg-red-50 px-2 py-0.5 text-xs font-medium text-red-600"
            : "inline-flex rounded-full bg-green-50 px-2 py-0.5 text-xs font-medium text-green-700"}
          >
            {row.original.postingStatus}
          </span>
        ),
      },
      {
        id: "actions",
        header: "",
        enableSorting: false,
        cell: ({ row }) => row.original.postingStatus === "Posted" && canEdit ? (
          <button
            type="button"
            onClick={() => setVoidTarget(row.original)}
            className="text-xs font-medium text-red-600 hover:text-red-700"
          >
            Void
          </button>
        ) : null,
      },
    ],
    [canEdit],
  );

  const summaryCards = [
    { label: "Lien Selling Amount", value: formatCurrency(summary?.lienSellingAmount ?? 0), icon: "ri-price-tag-3-line", color: "bg-orange-50 text-orange-600" },
    { label: "Total Paid", value: formatCurrency(summary?.totalPaid ?? 0), icon: "ri-checkbox-circle-line", color: "bg-green-50 text-green-600" },
    { label: "Remaining Balance", value: formatCurrency(summary?.remainingBalance ?? 0), icon: "ri-wallet-3-line", color: "bg-blue-50 text-blue-600" },
    { label: "Lien Aging", value: summary?.lienAgingDays == null ? "—" : `${summary.lienAgingDays} days`, icon: "ri-time-line", color: "bg-violet-50 text-violet-600" },
  ];

  return (
    <div className="space-y-5">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {summaryCards.map((card) => (
          <div key={card.label} className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
            <div className="flex items-center gap-3">
              <div className={`flex h-10 w-10 items-center justify-center rounded-lg ${card.color}`}>
                <i className={`${card.icon} text-xl`} />
              </div>
              <div>
                <p className="text-xs font-medium text-gray-500">{card.label}</p>
                <p className="mt-1 text-xl font-semibold text-gray-900">{card.value}</p>
              </div>
            </div>
          </div>
        ))}
      </div>

      {paymentsQuery.isError && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {paymentsQuery.error instanceof ApiError ? paymentsQuery.error.message : "Failed to load payments."}
        </div>
      )}

      <BaseTable
        data={response?.items ?? []}
        columns={columns}
        getRowId={(payment) => payment.id}
        isLoading={paymentsQuery.isLoading}
        emptyMessage="No payments have been recorded for this case."
        sorting={sorting}
        onSortingChange={(updater) => {
          setSorting((current) => typeof updater === "function" ? updater(current) : updater);
          setPagination((current) => ({ ...current, pageIndex: 0 }));
        }}
        manualSorting
        manualFiltering
        manualPagination
        pageCount={Math.max(1, Math.ceil((response?.totalCount ?? 0) / pagination.pageSize))}
        totalCount={response?.totalCount ?? 0}
        pagination={pagination}
        onPaginationChange={(updater) => setPagination((current) =>
          typeof updater === "function" ? updater(current) : updater)}
        pageSizeOptions={[10, 25, 50]}
        className="bg-white border-gray-200 rounded-xl"
        toolbar={(
          <div className="flex flex-col gap-3 border-b border-gray-100 p-4 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex flex-1 flex-col gap-3 sm:flex-row">
              <div className="relative max-w-md flex-1">
                <i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
                <Input
                  value={search}
                  onChange={(event) => {
                    setSearch(event.target.value);
                    setPagination((current) => ({ ...current, pageIndex: 0 }));
                  }}
                  placeholder="Search payments"
                  className="pl-9"
                />
              </div>
              <Select
                value={paymentMethod}
                onValueChange={(value) => {
                  setPaymentMethod(value);
                  setPagination((current) => ({ ...current, pageIndex: 0 }));
                }}
              >
                <SelectTrigger className="w-full sm:w-48">
                  <SelectValue placeholder="Payment method" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All methods</SelectItem>
                  {PAYMENT_METHODS.map((method) => <SelectItem key={method} value={method}>{method}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            {canEdit && (
              <button
                type="button"
                onClick={() => setShowAddPayment(true)}
                className="inline-flex items-center justify-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-white hover:bg-primary/90"
              >
                <i className="ri-add-line" /> Add Payment
              </button>
            )}
          </div>
        )}
      />

      <AddPaymentForm
        open={showAddPayment}
        onClose={() => setShowAddPayment(false)}
        caseId={caseId}
        liens={allLiensQuery.data?.items ?? []}
        liensLoadedAt={allLiensQuery.dataUpdatedAt ? new Date(allLiensQuery.dataUpdatedAt) : null}
        onRefreshLiens={() => void allLiensQuery.refetch()}
        isLiensFetching={allLiensQuery.isFetching}
        onSaved={() => void refreshPaymentData()}
      />

      <FormModal
        open={Boolean(voidTarget)}
        onClose={() => {
          if (voiding) return;
          setVoidTarget(null);
          setVoidReason("");
        }}
        onSubmit={confirmVoid}
        title="Void Payment"
        subtitle="The original record will remain in the financial history."
        submitLabel="Void Payment"
        submitDisabled={!voidReason.trim() || voiding}
        loading={voiding}
        size="sm"
      >
        <label className="mb-1 block text-sm font-medium text-gray-700">
          Reason <span className="text-red-500">*</span>
        </label>
        <Textarea
          value={voidReason}
          onChange={(event) => setVoidReason(event.target.value)}
          maxLength={500}
          rows={4}
          placeholder="Explain why this payment is being voided"
        />
      </FormModal>
    </div>
  );
}
