import { useState } from "react";
import type { ColumnDef } from "@tanstack/react-table";
import { BaseTable } from "@/components/ui/base-table";
import { LienTableToolbar } from "@/components/lien/lien-table";
import { useLienStore } from "@/stores/lien-store";
import { settlementService } from "@/lib/settlement";
import type { CaseLienItem, CaseLienItemMetadata } from "@/lib/cases";
import { CollapsibleSection } from "../components/collapsible-section";
import { formatCurrency } from "../utils/case-detail-utils";

export function PaymentHistoryWidget({
  payments,
  liens,
  paymentsLoadedAt,
  onRefreshPayments,
  isPaymentsFetching,
}: {
  payments: import("@/lib/settlement/settlement.types").LegacyCasePayment[];
  liens: (CaseLienItem & CaseLienItemMetadata)[];
  paymentsLoadedAt: Date | null;
  onRefreshPayments: () => void;
  isPaymentsFetching: boolean;
}) {
  const addToast = useLienStore((s) => s.addToast);
  const [openMenuId, setOpenMenuId] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const handleDelete = async (id: string) => {
    setDeletingId(id);
    setOpenMenuId(null);
    try {
      await settlementService.deleteSettlementPayment(id);
      addToast({
        type: "success",
        title: "Payment Deleted",
        description: "The payment record was removed.",
      });
      onRefreshPayments();
    } catch {
      addToast({
        type: "error",
        title: "Delete Failed",
        description: "Failed to delete the payment.",
      });
    } finally {
      setDeletingId(null);
    }
  };

  const paymentColumns: ColumnDef<
    import("@/lib/settlement/settlement.types").LegacyCasePayment,
    any
  >[] = [
    {
      id: "paymentNumber",
      header: "Payment ID",
      cell: ({ row }) => (
        <span className="text-xs font-mono text-gray-500 whitespace-nowrap">
          {row.original.paymentNumber != null
            ? `#${row.original.paymentNumber}`
            : "—"}
        </span>
      ),
    },
    {
      id: "lienId",
      header: "Lien ID",
      cell: ({ row }) => (
        <span className="text-xs font-mono text-primary whitespace-nowrap">
          {row.original.lienCode ?? row.original.lienId ?? "—"}
        </span>
      ),
    },
    {
      id: "lienStatus",
      header: "Lien Status",
      cell: ({ row }) => (
        <span className="text-xs text-gray-600 whitespace-nowrap">
          {row.original.lienStatus ?? "—"}
        </span>
      ),
    },
    {
      id: "amountToSettle",
      header: "Amount to Settle",
      meta: { align: "right" },
      cell: ({ row }) => {
        const amount =
          row.original.amountToSettle != null
            ? parseFloat(String(row.original.amountToSettle))
            : null;
        return (
          <span className="text-sm text-gray-700 font-medium tabular-nums whitespace-nowrap">
            {amount != null ? formatCurrency(amount) : "—"}
          </span>
        );
      },
    },
    {
      id: "checkReceived",
      header: "Check Received",
      cell: () => (
        <span className="text-xs text-gray-400 whitespace-nowrap">—</span>
      ),
    },
    {
      id: "checkNumber",
      header: "Check Number",
      cell: ({ row }) => (
        <span className="text-xs font-mono text-gray-500 whitespace-nowrap">
          {row.original.checkNumber ?? "—"}
        </span>
      ),
    },
    {
      id: "type",
      header: "Settlement Type",
      cell: ({ row }) => (
        <span className="text-xs text-gray-600 whitespace-nowrap">
          {row.original.type ?? "—"}
        </span>
      ),
    },
    {
      id: "status",
      header: "Settlement Status",
      cell: ({ row }) => (
        <span className="text-xs text-gray-600 whitespace-nowrap">
          {row.original.status ?? "—"}
        </span>
      ),
    },
    {
      id: "date",
      header: "Date",
      cell: ({ row }) => (
        <span className="text-xs text-gray-500 whitespace-nowrap">
          {row.original.date ?? "—"}
        </span>
      ),
    },
    {
      id: "menu",
      header: "",
      cell: ({ row }) => {
        const p = row.original;
        const rowKey = `paymentHistory${row.index}`;
        const isDeleting = deletingId === rowKey;
        return (
          <div className="text-center relative">
            {p.id ? (
              <>
                <button
                  type="button"
                  disabled={isDeleting}
                  onClick={() =>
                    setOpenMenuId(openMenuId === rowKey ? null : rowKey)
                  }
                  className="inline-flex items-center justify-center w-7 h-7 rounded hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors disabled:opacity-40"
                >
                  {isDeleting ? (
                    <i className="ri-loader-4-line text-sm animate-spin" />
                  ) : (
                    <i className="ri-more-2-line text-sm" />
                  )}
                </button>
                {openMenuId === rowKey && (
                  <div className="absolute right-0 top-full mt-1 w-32 bg-white border border-gray-200 rounded-lg shadow-lg z-50">
                    <button
                      type="button"
                      onClick={() => handleDelete(p.id!)}
                      className="w-full text-left px-3 py-2 text-sm text-red-600 hover:bg-red-50 transition-colors flex items-center gap-2"
                    >
                      <i className="ri-delete-bin-line text-sm" />
                      Delete
                    </button>
                  </div>
                )}
              </>
            ) : (
              <span className="text-gray-300 text-xs">—</span>
            )}
          </div>
        );
      },
    },
  ];

  return (
    <CollapsibleSection title="Payment History" icon="ri-exchange-dollar-line">
      {payments.length === 0 ? (
        <div className="border border-gray-100 rounded-lg overflow-hidden">
          <LienTableToolbar
            loadedAt={paymentsLoadedAt}
            onRefresh={onRefreshPayments}
            isRefreshing={isPaymentsFetching}
          />
          <div className="text-center py-8">
            <i className="ri-exchange-dollar-line text-2xl text-gray-300" />
            <p className="text-sm text-gray-400 mt-2">No payment history</p>
          </div>
        </div>
      ) : (
        <>
          <BaseTable
            columns={paymentColumns}
            data={payments}
            enablePagination={false}
            toolbar={
              <LienTableToolbar
                loadedAt={paymentsLoadedAt}
                onRefresh={onRefreshPayments}
                isRefreshing={isPaymentsFetching}
              />
            }
          />
          <div className="mt-3">
            <p className="text-xs text-gray-400">
              {payments.length} payment{payments.length !== 1 ? "s" : ""}
            </p>
          </div>
        </>
      )}
    </CollapsibleSection>
  );
}
