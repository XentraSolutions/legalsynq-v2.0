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

const SELLER_STATUS_COLOR: Record<string, NonNullable<ChipProps["color"]>> = {
  Draft: "default",
  Pending: "warning",
  Internal: "info",
  Approval: "warning",
  PreparedForSale: "info",
  SubmittedForSale: "warning",
  Accepted: "success",
  Declined: "danger",
  Sold: "success",
  Withdrawn: "danger",
  Archived: "default",
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
        <Field label="Purchase Date" value={lien.purchaseDate} />
        <Field label="Initial Service Date" value={lien.initialServiceDate} />
        <Field label="End Service Date" value={lien.endServiceDate} />
        <Field label="Lien Notes" value={lien.notes} />
      </div>
    </PanelShell>
  );
}
