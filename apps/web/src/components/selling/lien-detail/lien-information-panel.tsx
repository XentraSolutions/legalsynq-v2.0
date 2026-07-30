import { LienDetail } from "@/types/lien-selling";
import { LienStatusBadge } from "../../lien/lien-status-badge";
import { PanelShell } from "./panel-shell";

interface LienDetailPanelProps {
  lien: LienDetail;
  onEdit?: () => void;
}

function Field({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs font-medium text-gray-500 uppercase tracking-wide">
        {label}
      </dt>
      <dd className="mt-1 text-sm text-gray-900">{value ?? "—"}</dd>
    </div>
  );
}

export function LienInformationPanel({ lien, onEdit }: LienDetailPanelProps) {
  return (
    <PanelShell title="Lien Information" onEdit={onEdit}>
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-x-6 gap-y-5">
        <div>
          <dt className="text-[11px] font-medium text-gray-400 leading-tight">
            Current Status
          </dt>
          <dd className="mt-1">
            <LienStatusBadge status={lien.status} />
          </dd>
        </div>
        <Field label="Listing Visibility" value={lien.listingVisibility} />
        <Field label="Initial Service Date" value={lien.initialServiceDate} />
        <Field label="End Service Date" value={lien.endServiceDate} />
        <Field label="Lien Notes" value={lien.notes} />
      </div>
    </PanelShell>
  );
}
