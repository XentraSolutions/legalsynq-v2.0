import { useState } from "react";
import type { ColumnDef } from "@tanstack/react-table";
import { BaseTable } from "@/components/ui/base-table";
import { LienTableToolbar } from "@/components/lien/lien-table";
import { useLienStore } from "@/stores/lien-store";
import { settlementService } from "@/lib/settlement";
import type { CaseLienItem, CaseLienItemMetadata } from "@/lib/cases";
import type { LegacyCasePayment } from "@/lib/settlement/settlement.types";
import { CollapsibleSection } from "../components/collapsible-section";
import { formatCurrency } from "../utils/case-detail-utils";
import { ActionMenu } from "@/components/lien/action-menu";
import { useRouter } from "next/navigation";

function toAmount(value: string | number | null | undefined): number | null {
  if (value === null || value === undefined || value === "") return null;
  const n = typeof value === "number" ? value : Number(value);
  return Number.isNaN(n) ? null : n;
}

export function PaymentHistoryWidget({
  payments,
  liens,
  paymentsLoadedAt,
  onRefreshPayments,
  isPaymentsFetching,
  onEditPayment,
  onDeletePayment,
}: {
  payments: LegacyCasePayment[];
  liens: (CaseLienItem & CaseLienItemMetadata)[];
  paymentsLoadedAt: Date | null;
  onRefreshPayments: () => void;
  isPaymentsFetching: boolean;
  onEditPayment: (p: any) => void;
  onDeletePayment: (p: string) => void;
}) {
  const addToast = useLienStore((s) => s.addToast);
  const [openMenuId, setOpenMenuId] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const lienById = new Map(liens.map((l) => [l.id, l]));
  const router = useRouter();

  const paymentColumns: ColumnDef<LegacyCasePayment, any>[] = [
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
          {row.original.lienCode ||
            lienById.get(row.original.lienId)?.lienNumber ||
            row.original.lienId ||
            "—"}
        </span>
      ),
    },
    {
      id: "lienStatus",
      header: "Lien Status",
      cell: ({ row }) => (
        <span className="text-xs text-gray-600 whitespace-nowrap">
          {row.original.lienStatus ||
            lienById.get(row.original.lienId)?.status ||
            "—"}
        </span>
      ),
    },
    {
      id: "amountToSettle",
      header: "Amount to Settle",
      meta: { align: "right" },
      cell: ({ row }) => (
        <span className="text-sm text-gray-700 font-medium tabular-nums whitespace-nowrap">
          {formatCurrency(toAmount(row.original.amountToSettle))}
        </span>
      ),
    },
    {
      id: "checkAmount",
      header: "Check Amount",
      meta: { align: "right" },
      cell: ({ row }) => (
        <span className="text-sm text-gray-700 font-medium tabular-nums whitespace-nowrap">
          {formatCurrency(toAmount(row.original.checkAmount))}
        </span>
      ),
    },
    {
      id: "checkReceived",
      header: "Check Received",
      cell: ({ row }) => (
        <span className="text-xs text-gray-500 whitespace-nowrap">
          {row.original.checkDate || "—"}
        </span>
      ),
    },
    {
      id: "checkNumber",
      header: "Check Number",
      cell: ({ row }) => (
        <span className="text-xs font-mono text-gray-500 whitespace-nowrap">
          {row.original.checkNumber || "—"}
        </span>
      ),
    },
    {
      id: "settlementType",
      header: "Settlement Type",
      cell: ({ row }) => (
        <span className="text-xs text-gray-600 whitespace-nowrap">
          {row.original.type || "—"}
        </span>
      ),
    },
    {
      id: "settlementStatus",
      header: "Settlement Status",
      cell: ({ row }) => (
        <span className="text-xs text-gray-600 whitespace-nowrap">
          {row.original.status || "—"}
        </span>
      ),
    },
    {
      id: "date",
      header: "Date",
      cell: ({ row }) => (
        <span className="text-xs text-gray-500 whitespace-nowrap">
          {row.original.date || "—"}
        </span>
      ),
    },
    {
      id: "menu",
      header: "",
      cell: ({ row }) => {
        const p = row.original;
        return (
          <div onClick={(e) => e.stopPropagation()}>
            <ActionMenu
              items={[
                {
                  label: "Edit",
                  onClick: () => {
                    onEditPayment(p);
                  },
                },
                {
                  label: "Delete",
                  variant: "danger",
                  onClick: () => {
                    onDeletePayment(p.id!);
                  },
                },
              ]}
            />
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
