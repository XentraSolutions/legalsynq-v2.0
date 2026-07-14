import { LienTable, LienTableToolbar } from "@/components/lien/lien-table";
import type { LienColumnDef } from "@/components/lien/lien-table";
import type { CaseLienItem, CaseLienItemMetadata } from "@/lib/cases";
import { CollapsibleSection } from "../../../../components/collapsible-section";
import { formatCurrency } from "../../../../utils/case-detail-utils";

const closedLienDisplayColumns: LienColumnDef[] = [
  {
    id: "lienId",
    header: "Lien ID",
    cell: (l) => (
      <span className="text-xs font-mono text-gray-500">{l.lienNumber}</span>
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
      <span className="text-sm text-green-600 tabular-nums">
        {formatCurrency(l.reductionAmount)}
      </span>
    ),
  },
  {
    id: "payment",
    header: "Payment",
    align: "right",
    cell: (l) => (
      <span className="text-sm text-gray-700 tabular-nums">
        {formatCurrency(l.paymentAmount)}
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

export function ClosedLiensSection({
  closedLiens,
  liensLoadedAt,
  onRefreshLiens,
  isLiensFetching,
  closedLiensTotalBilling,
  closedLiensTotalReduction,
  closedLiensTotalPayment,
}: {
  closedLiens: (CaseLienItem & CaseLienItemMetadata)[];
  liensLoadedAt: Date | null;
  onRefreshLiens: () => void;
  isLiensFetching: boolean;
  closedLiensTotalBilling: number;
  closedLiensTotalReduction: number;
  closedLiensTotalPayment: number;
}) {
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
                <span className="text-sm font-semibold text-green-600 tabular-nums">
                  {formatCurrency(closedLiensTotalReduction)}
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
              content: (
                <span className="text-sm font-semibold text-gray-700 tabular-nums">
                  {formatCurrency(0)}
                </span>
              ),
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
