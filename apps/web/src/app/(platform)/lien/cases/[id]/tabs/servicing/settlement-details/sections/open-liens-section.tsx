import { LienTable, LienTableToolbar } from "@/components/lien/lien-table";
import type { LienColumnDef } from "@/components/lien/lien-table";
import type { CaseLienItem, CaseLienItemMetadata } from "@/lib/cases";
import { CollapsibleSection } from "../../../../components/collapsible-section";
import { formatCurrency } from "../../../../utils/case-detail-utils";

const lienDisplayColumns: LienColumnDef[] = [
  {
    id: "lienId",
    header: "Lien ID",
    cell: (l) => (
      <span className="text-xs font-mono text-primary">{l.lienNumber}</span>
    ),
  },
  {
    id: "billing",
    header: "Billing Amt",
    align: "right",
    cell: (l) => (
      <span className="text-sm text-gray-700 tabular-nums">
        {formatCurrency(l.originalAmount)}
      </span>
    ),
  },
  {
    id: "reduction",
    header: "Reduction",
    align: "right",
    cell: (l) => (
      <span className="text-sm text-gray-500 tabular-nums">
        {l.reductionAmount !== null ? formatCurrency(l.reductionAmount) : "---"}
      </span>
    ),
  },
  {
    id: "payment",
    header: "Payment",
    align: "right",
    cell: (l) => (
      <span className="text-sm text-gray-500 tabular-nums">
        {l.paymentAmount !== null ? formatCurrency(l.paymentAmount) : "---"}
      </span>
    ),
  },
  {
    id: "balance",
    header: "Balance",
    align: "right",
    cell: (l) => (
      <span className="text-sm text-gray-700 font-medium tabular-nums">
        {formatCurrency(l.balance)}
      </span>
    ),
  },
];

export function OpenLiensSection({
  openLiens,
  liensLoadedAt,
  onRefreshLiens,
  isLiensFetching,
  openLiensTotalBilling,
  openLiensTotalBalance,
  onSetupReduction,
  onNoRecovery,
  onAddPayment,
}: {
  openLiens: (CaseLienItem & CaseLienItemMetadata)[];
  liensLoadedAt: Date | null;
  onRefreshLiens: () => void;
  isLiensFetching: boolean;
  openLiensTotalBilling: number;
  openLiensTotalBalance: number;
  onSetupReduction: () => void;
  onNoRecovery: () => void;
  onAddPayment: () => void;
}) {
  return (
    <CollapsibleSection title="Open Liens" icon="ri-stack-line">
      {openLiens.length === 0 ? (
        <div className="border border-gray-100 rounded-lg overflow-hidden">
          <LienTableToolbar
            loadedAt={liensLoadedAt}
            onRefresh={onRefreshLiens}
            isRefreshing={isLiensFetching}
          />
          <div className="text-center py-8">
            <i className="ri-stack-line text-2xl text-gray-300" />
            <p className="text-sm text-gray-400 mt-2">No open liens</p>
          </div>
        </div>
      ) : (
        <>
          <LienTable
            liens={openLiens}
            columns={lienDisplayColumns}
            footer={[
              {
                colSpan: 2,
                content: (
                  <span className="text-xs font-semibold text-gray-500 uppercase tracking-wide">
                    Totals ({openLiens.length} lien
                    {openLiens.length !== 1 ? "s" : ""})
                  </span>
                ),
              },
              {
                align: "right",
                content: (
                  <span className="text-sm font-semibold text-gray-700 tabular-nums">
                    {formatCurrency(openLiensTotalBilling)}
                  </span>
                ),
              },
              {
                align: "right",
                content: <span className="text-sm text-gray-400">---</span>,
              },
              {
                align: "right",
                content: <span className="text-sm text-gray-400">---</span>,
              },
              {
                align: "right",
                content: (
                  <span className="text-sm font-semibold text-gray-700 tabular-nums">
                    {formatCurrency(openLiensTotalBalance)}
                  </span>
                ),
              },
            ]}
            loadedAt={liensLoadedAt}
            onRefresh={onRefreshLiens}
            isRefreshing={isLiensFetching}
          />
          <div className="mt-3 flex items-center gap-2">
            <button
              onClick={onSetupReduction}
              className="px-3 py-1.5 text-xs font-medium text-primary bg-primary/5 border border-primary/20 rounded-md hover:bg-primary/10 transition-colors inline-flex items-center gap-1"
            >
              <i className="ri-percent-line text-sm" />
              Setup Reduction
            </button>
            <button
              onClick={onNoRecovery}
              className="px-3 py-1.5 text-xs font-medium text-red-600 bg-red-50 border border-red-200 rounded-md hover:bg-red-100 transition-colors inline-flex items-center gap-1"
            >
              <i className="ri-close-circle-line text-sm" />
              No Recovery
            </button>
            <button
              onClick={onAddPayment}
              className="px-3 py-1.5 text-xs font-medium text-primary bg-primary/5 border border-primary/20 rounded-md hover:bg-primary/10 transition-colors inline-flex items-center gap-1"
            >
              <i className="ri-money-dollar-circle-line text-sm" />
              Add Payment
            </button>
          </div>
        </>
      )}
    </CollapsibleSection>
  );
}
