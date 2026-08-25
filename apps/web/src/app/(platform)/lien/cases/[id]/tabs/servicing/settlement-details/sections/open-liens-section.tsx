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
      <span className="text-sm text-gray-600 whitespace-nowrap">
        {l.lienNumber}
      </span>
    ),
  },
  {
    id: "facilityName",
    header: "Medical Facility",
    cell: (l) => (
      <span className="text-sm text-gray-600 truncate max-w-40 block">
        {l.facilityName || ""}
      </span>
    ),
  },
  {
    id: "billing",
    header: "Billing Amount",
    align: "right",
    cell: (l) => (
      <span className="text-sm text-gray-700 tabular-nums">
        {formatCurrency(l.originalAmount)}
      </span>
    ),
  },
  {
    id: "purchaseAmount",
    header: "Purchase Amount",
    align: "right",
    cell: (l) => (
      <span className="text-sm text-gray-700 tabular-nums">
        {formatCurrency(l.purchaseAmount)}
      </span>
    ),
  },
  {
    id: "reduction",
    header: "Reduction Amount",
    align: "right",
    cell: (l) => (
      <span className="text-sm text-gray-500 tabular-nums">
        {formatCurrency(l.reductionAmount)}
      </span>
    ),
  },
  {
    id: "reductionDate",
    header: "Reduction Date",
    align: "right",
    cell: (l) => (
      <span className="text-sm text-gray-500 tabular-nums">
        {l.reductionDate}
      </span>
    ),
  },
  {
    id: "balance",
    header: "Amount to Settle",
    align: "right",
    cell: (l) => (
      <span className="text-sm text-gray-700 font-medium tabular-nums">
        {formatCurrency(l.balance)}
      </span>
    ),
  },
  {
    id: "payment",
    header: "Amount Received",
    align: "right",
    cell: (l) => (
      <span className="text-sm text-gray-700 font-medium tabular-nums">
        {formatCurrency(l.paymentAmount)}
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
  openLiensTotalPurchase,
  openLiensTotalReduction,
  openLiensTotalBalance,
  openLiensTotalPayment,
  onSetupReduction,
  onNoRecovery,
  onAddPayment,
}: {
  openLiens: (CaseLienItem & CaseLienItemMetadata)[];
  liensLoadedAt: Date | null;
  onRefreshLiens: () => void;
  isLiensFetching: boolean;
  openLiensTotalBilling: number;
  openLiensTotalPurchase: number;
  openLiensTotalReduction: number;
  openLiensTotalBalance: number;
  openLiensTotalPayment: number;
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
            expandable={false}
            footer={[
              {
                colSpan: 2,
                content: (
                  <span className="text-sm font-semibold text-gray-700 uppercase tabular-nums">
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
                content: (
                  <span className="text-sm font-semibold text-gray-700 tabular-nums">
                    {formatCurrency(openLiensTotalPurchase)}
                  </span>
                ),
              },
              {
                align: "right",
                content: (
                  <span className="text-sm font-semibold text-gray-700 tabular-nums">
                    {formatCurrency(openLiensTotalReduction)}
                  </span>
                ),
              },
              {
                align: "right",
                content: (
                  <span className="text-sm font-semibold text-gray-700 tabular-nums">
                    ---
                  </span>
                ),
              },
              {
                align: "right",
                content: (
                  <span className="text-sm font-semibold text-gray-700 tabular-nums">
                    {formatCurrency(openLiensTotalBalance)}
                  </span>
                ),
              },
              {
                align: "right",
                content: (
                  <span className="text-sm font-semibold text-gray-700 tabular-nums">
                    {formatCurrency(openLiensTotalPayment)}
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
