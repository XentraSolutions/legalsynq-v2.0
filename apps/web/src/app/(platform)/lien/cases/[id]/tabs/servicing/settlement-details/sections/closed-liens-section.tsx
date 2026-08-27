import { LienTable, LienTableToolbar } from "@/components/lien/lien-table";
import type { LienColumnDef } from "@/components/lien/lien-table";
import type { CaseLienItem, CaseLienItemMetadata } from "@/lib/cases";
import { DateDisplay } from "@/components/ui/date-display";
import { CollapsibleSection } from "../../../../components/collapsible-section";
import { formatCurrency } from "../../../../utils/case-detail-utils";

const closedLienDisplayColumns: LienColumnDef[] = [
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
      <span className="text-sm text-green-600 tabular-nums">
        {formatCurrency(l.reductionAmount)}
      </span>
    ),
  },
  {
    id: "reductionDate",
    header: "Reduction Date",
    align: "right",
    cell: (l) => (
      <span className="text-sm text-green-600 tabular-nums">
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
      <span className="text-sm text-gray-700 tabular-nums">
        {formatCurrency(l.paymentAmount)}
      </span>
    ),
  },
  {
    id: "dateClosed",
    header: "Date Closed",
    cell: (l) => (
      <span className="text-xs text-gray-500 whitespace-nowrap">
        <DateDisplay value={l.closedAtUtc} format="date" fallback="" />
      </span>
    ),
  },
];

export function ClosedLiensSection({
  closedLiens,
  liensLoadedAt,
  onRefreshLiens,
  isLiensFetching,
  closedLiensTotalBilling,
  closedLiensTotalPurchase,
  closedLiensTotalReduction,
  closedLiensTotalBalance,
  closedLiensTotalPayment,
}: {
  closedLiens: (CaseLienItem & CaseLienItemMetadata)[];
  liensLoadedAt: Date | null;
  onRefreshLiens: () => void;
  isLiensFetching: boolean;
  closedLiensTotalBilling: number;
  closedLiensTotalPurchase: number;
  closedLiensTotalReduction: number;
  closedLiensTotalBalance: number;
  closedLiensTotalPayment: number;
}) {
  console.log(closedLiens);
  return (
    <CollapsibleSection title="Closed Liens" icon="ri-checkbox-circle-line">
      {closedLiens.length === 0 ? (
        <div className="border border-gray-100 rounded-lg overflow-hidden">
          <LienTableToolbar
            loadedAt={liensLoadedAt}
            onRefresh={onRefreshLiens}
            isRefreshing={isLiensFetching}
          />
          <div className="text-center py-8">
            <i className="ri-checkbox-circle-line text-2xl text-gray-300" />
            <p className="text-sm text-gray-400 mt-2">No closed liens</p>
          </div>
        </div>
      ) : (
        <LienTable
          liens={closedLiens}
          columns={closedLienDisplayColumns}
          expandable={false}
          footer={[
            {
              colSpan: 2,
              content: (
                <span className="text-xs font-semibold text-gray-500 uppercase tracking-wide">
                  Totals ({closedLiens.length} lien
                  {closedLiens.length !== 1 ? "s" : ""})
                </span>
              ),
            },
            {
              align: "right",
              content: (
                <span className="text-sm font-semibold text-gray-700 tabular-nums">
                  {formatCurrency(closedLiensTotalBilling)}
                </span>
              ),
            },
            {
              align: "right",
              content: (
                <span className="text-sm font-semibold text-gray-700 tabular-nums">
                  {formatCurrency(closedLiensTotalPurchase)}
                </span>
              ),
            },
            {
              align: "right",
              content: (
                <span className="text-sm font-semibold text-green-600 tabular-nums">
                  {formatCurrency(closedLiensTotalReduction)}
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
                  {formatCurrency(closedLiensTotalBalance)}
                </span>
              ),
            },
            {
              align: "right",
              content: (
                <span className="text-sm font-semibold text-gray-700 tabular-nums">
                  {formatCurrency(closedLiensTotalPayment)}
                </span>
              ),
            },
            {
              align: "right",
              content: <span className="text-sm text-gray-400">---</span>,
            },
          ]}
          loadedAt={liensLoadedAt}
          onRefresh={onRefreshLiens}
          isRefreshing={isLiensFetching}
        />
      )}
    </CollapsibleSection>
  );
}
