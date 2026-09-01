"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import type { ColumnDef, PaginationState, SortingState } from "@tanstack/react-table";
import { useQueryClient } from "@tanstack/react-query";
import { Search, Settings2, Trash2, Wallet, X } from "lucide-react";
import { toast } from "sonner";
import { BaseTable } from "@/components/ui/base-table";
import { BaseSelect, type BaseSelectOption } from "@/components/ui/base-select";
import { Card } from "@/components/ui/dashboard-card";
import { buttonVariants } from "@/components/ui/button";
import Field from "@/components/lien/field";
import { ActionMenu, type ActionMenuItem } from "@/components/selling/action-menu";
import { ConfirmDialog, FormModal } from "@/components/selling/modal";
import { Button } from "@/components/selling/button";
import { ContactsEmptyState } from "@/components/selling/contacts/contacts-empty-state";
import { cn } from "@/lib/utils";
import {
  TABLE_CELL_CLASSNAME,
  TABLE_HEADER_CLASSNAME,
  TABLE_HEADER_CELL_CLASSNAME,
  TABLE_LINK_CLASSNAME,
} from "@/components/selling/table-cell-styles";
import { ApiError } from "@/lib/api-client";
import { lienPaymentsService } from "@/lib/selling/lien-payments.service";
import type {
  LienPaymentItem,
  RecordLienPaymentRequest,
} from "@/lib/selling/lien-payments.types";
import { useLienPayments } from "@/lib/selling/use-lien-payments";
import type { LienDetailsResult } from "@/types/lien-selling";

// Still needed as a passthrough for BaseTable (src/components/ui), which
// renders a plain <button> (not the selling Button component) and exposes
// primaryButtonClassName as a generic override, not selling-specific.
const PRIMARY_BUTTON_CLASSNAME = "bg-[#EE7132] hover:bg-[#EE7132]/90 text-white";

export const PAYMENT_METHODS = [
  "Check",
  "ACH",
  "Wire Transfer",
  "Credit / Debit Card",
  "Trust Account",
  "Other",
];
export const PAYMENT_METHOD_OPTIONS: BaseSelectOption[] = PAYMENT_METHODS.map(
  (method) => ({ value: method, label: method }),
);

export function formatCurrency(value: number): string {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
  }).format(value);
}

export function formatDate(value: string | null): string {
  if (!value) return "—";
  const [year, month, day] = value.slice(0, 10).split("-").map(Number);
  return new Intl.DateTimeFormat("en-US", {
    month: "2-digit",
    day: "2-digit",
    year: "numeric",
  }).format(new Date(year, month - 1, day));
}

export function PaymentTab({ lien }: { lien: LienDetailsResult }) {
  const caseId = lien.caseInformation?.id ?? null;
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
  const [showAddPayment, setShowAddPayment] = useState(false);
  const [voidTarget, setVoidTarget] = useState<LienPaymentItem | null>(null);
  const [voiding, setVoiding] = useState(false);

  // The payments endpoint is case-scoped (search/filter/sort/pagination all
  // run server-side), not lien-scoped — a case backs exactly one lien in the
  // lien-selling flow today, so the case's payments are this lien's payments.
  const activeSort = sorting[0] ?? { id: "paymentDate", desc: true };
  const paymentsQuery = useLienPayments(caseId ?? "", {
    search: search.trim() || undefined,
    paymentMethod: methodFilter || undefined,
    // "Delete" in this tab actually voids the payment server-side rather
    // than removing the row — excluding voided rows here keeps the list
    // matching what "Delete" implied to the user.
    postingStatus: "Posted",
    sortBy: activeSort.id,
    sortDirection: activeSort.desc ? "desc" : "asc",
    page: pagination.pageIndex + 1,
    pageSize: pagination.pageSize,
  });
  const response = paymentsQuery.data;
  const summary = response?.summary;

  const refresh = () => {
    if (!caseId) return;
    void queryClient.invalidateQueries({
      queryKey: ["lien-payments", caseId],
    });
  };

  const confirmVoid = async () => {
    if (!voidTarget || !caseId) return;
    setVoiding(true);
    try {
      await lienPaymentsService.voidLienPayment(
        caseId,
        voidTarget.id,
        "Deleted from lien payment tab",
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
        accessorKey: "detailsContext",
        header: "Details/Context",
        cell: ({ row }) => (
          <span
            className={`${TABLE_CELL_CLASSNAME} block max-w-56 truncate`}
            title={row.original.detailsContext ?? undefined}
          >
            {row.original.detailsContext || "—"}
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
    {
      label: "Lien Selling Amount",
      value: formatCurrency(summary?.lienSellingAmount ?? 0),
    },
    { label: "Total Paid", value: formatCurrency(summary?.totalPaid ?? 0) },
    {
      label: "Remaining Balance",
      value: formatCurrency(summary?.remainingBalance ?? 0),
    },
    {
      label: "Lien Aging",
      value:
        summary?.lienAgingDays == null ? "—" : `${summary.lienAgingDays} Days`,
    },
  ];

  if (!caseId) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg">
        <ContactsEmptyState
          icon={Wallet}
          title="No Case Linked"
          description="Add case information to this lien before recording payments."
        />
      </div>
    );
  }

  return (
    <div className="space-y-5">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {summaryCards.map((card) => (
          <div
            key={card.label}
            className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm"
          >
            <p className="text-xs text-gray-400">{card.label}</p>
            <p className="mt-2 text-2xl font-bold text-gray-900">
              {card.value}
            </p>
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
          emptyMessage="No payments have been recorded for this lien."
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
                onClick={() => setShowAddPayment(true)}
              >
                Add Payment
              </Button>
            </div>
          }
        />
      </Card>

      {showAddPayment && (
        <AddLienPaymentModal
          caseId={caseId}
          lienId={lien.lienId}
          onClose={() => setShowAddPayment(false)}
          onSaved={() => {
            setShowAddPayment(false);
            refresh();
          }}
        />
      )}

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

interface AddLienPaymentFormState {
  amount: string;
  paymentMethod: string;
  paymentDate: string;
  checkAmount: string;
  checkDate: string;
  checkNumber: string;
  referenceNumber: string;
  notes: string;
}

const EMPTY_FORM: AddLienPaymentFormState = {
  amount: "",
  paymentMethod: "",
  paymentDate: "",
  checkAmount: "",
  checkDate: "",
  checkNumber: "",
  referenceNumber: "",
  notes: "",
};

function AddLienPaymentModal({
  caseId,
  lienId,
  onClose,
  onSaved,
}: {
  caseId: string;
  lienId: string;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [form, setForm] = useState<AddLienPaymentFormState>(EMPTY_FORM);
  const [saving, setSaving] = useState(false);

  const isCheck = form.paymentMethod === "Check";
  const amount = Number(form.amount);
  const canSubmit =
    form.amount.trim() !== "" &&
    amount > 0 &&
    form.paymentMethod !== "" &&
    form.paymentDate !== "";

  const handleSubmit = async () => {
    if (!canSubmit) return;
    setSaving(true);
    try {
      const detailsContext = isCheck
        ? [
            form.checkAmount &&
              `Check Amount: ${formatCurrency(Number(form.checkAmount))}`,
            form.checkDate && `Check Date: ${formatDate(form.checkDate)}`,
          ]
            .filter(Boolean)
            .join(" · ") || undefined
        : undefined;

      const request: RecordLienPaymentRequest = {
        amount,
        paymentDate: form.paymentDate,
        paymentMethod: form.paymentMethod,
        referenceNumber: (isCheck ? form.checkNumber : form.referenceNumber).trim(),
        detailsContext,
        notes: form.notes.trim() || undefined,
        allocations: [{ lienId, amount }],
      };

      await lienPaymentsService.recordLienPayment(caseId, request);
      toast.success("Payment recorded.");
      onSaved();
    } catch (err) {
      toast.error(
        err instanceof ApiError ? err.message : "Failed to record payment",
      );
    } finally {
      setSaving(false);
    }
  };

  return (
    <FormModal
      open
      onClose={onClose}
      onSubmit={handleSubmit}
      title="Add Lien Payment"
      subtitle="Provide the payment information below to keep your payment details accurate and up to date."
      submitLabel="Add Payment"
      submitDisabled={!canSubmit}
      loading={saving}
    >
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <Field
          type="number"
          label="Payment Amount"
          required
          maxDecimals={2}
          prefix="$"
          value={form.amount}
          onChange={(v) => setForm((prev) => ({ ...prev, amount: v }))}
        />
        <Field
          type="select"
          label="Payment Method"
          required
          multiple={false}
          placeholder="Select payment method"
          options={PAYMENT_METHOD_OPTIONS}
          value={form.paymentMethod || null}
          onChange={(v: string) =>
            setForm((prev) => ({ ...prev, paymentMethod: v }))
          }
        />

        <Field
          type="date"
          label="Payment Date"
          required
          value={form.paymentDate}
          onChange={(v) => setForm((prev) => ({ ...prev, paymentDate: v }))}
        />
        {isCheck ? (
          <Field
            type="number"
            label="Check Amount"
            maxDecimals={2}
            prefix="$"
            value={form.checkAmount}
            onChange={(v) => setForm((prev) => ({ ...prev, checkAmount: v }))}
          />
        ) : (
          <Field
            type="text"
            label="Reference / ID #"
            value={form.referenceNumber}
            onChange={(v) =>
              setForm((prev) => ({ ...prev, referenceNumber: v }))
            }
          />
        )}

        {isCheck && (
          <>
            <Field
              type="date"
              label="Check Date"
              value={form.checkDate}
              onChange={(v) => setForm((prev) => ({ ...prev, checkDate: v }))}
            />
            <Field
              type="text"
              label="Check Number"
              value={form.checkNumber}
              onChange={(v) =>
                setForm((prev) => ({ ...prev, checkNumber: v }))
              }
            />
          </>
        )}
      </div>

      <div className="mt-4">
        <Field
          type="textarea"
          label="Notes"
          placeholder="Leave payment note here..."
          value={form.notes}
          onChange={(v) => setForm((prev) => ({ ...prev, notes: v }))}
        />
      </div>
    </FormModal>
  );
}
