import { LienDetail } from "@/types/lien-selling";
import { Chip, type ChipProps } from "@/components/ui/chip";
import { PanelShell } from "./panel-shell";
import { sellerStatusLabel } from "@/lib/selling/selling-detail.mapper";

interface LienDetailPanelProps {
  lien: LienDetail;
  onEdit?: () => void;
}

function Field({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs font-medium text-gray-500 tracking-wide">
        {label}
      </dt>
      <dd className="mt-1 text-sm text-gray-900">{value ?? "—"}</dd>
    </div>
  );
}

const SELLER_STATUS_STYLES: Record<string, string> = {
  Draft: "bg-gray-50 text-gray-600 border-gray-200",
  Pending: "bg-amber-50 text-amber-700 border-amber-200",
  Internal: "bg-blue-50 text-blue-700 border-blue-200",
  Approval: "bg-amber-50 text-amber-700 border-amber-200",
  PreparedForSale: "bg-blue-50 text-blue-700 border-blue-200",
  SubmittedForSale: "bg-amber-50 text-amber-700 border-amber-200",
  Accepted: "bg-green-50 text-green-700 border-green-200",
  Declined: "bg-red-50 text-red-600 border-red-200",
  Sold: "bg-green-50 text-green-700 border-green-200",
  Withdrawn: "bg-red-50 text-red-600 border-red-200",
  Archived: "bg-gray-50 text-gray-500 border-gray-200",
};

function SellerStatusBadge({ status }: { status: string }) {
  const color = SELLER_STATUS_COLOR[status] ?? "default";
  return (
    <Chip variant="soft" size="lg" color={color}>
      {sellerStatusLabel(status)}
    </Chip>
  );
}

export function LienInformationPanel({ lien, onEdit }: LienDetailPanelProps) {
  return (
    <PanelShell title="Lien Information" onEdit={onEdit}>
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-2 gap-x-6 gap-y-5">
        <div>
          <dt className="text-xs font-medium text-gray-500 tracking-wide">
            Lien Status
          </dt>
          <dd className="mt-1">
            <SellerStatusBadge status={lien.sellerStatus} />
          </dd>
        </div>
        <Field label="Listing Visibility" value={lien.listingVisibility} />
        <Field label="Purchase Date" value={lien.purchaseDate} />
        <Field label="Initial Service Date" value={lien.initialServiceDate} />
        <Field label="End Service Date" value={lien.endServiceDate} />
        <Field label="Lien Notes" value={lien.notes} />
      </div>
    </PanelShell>
  );
}
