import { LienDetail, LienStatusHistoryItem } from "@/types/lien-selling";
import { LienStatusBadge } from "../../lien/lien-status-badge";

interface LienDetailPanelProps {
  lien: LienDetail;
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

function Section({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <section className="border-t border-gray-100 pt-5 mt-5 first:border-0 first:pt-0 first:mt-0">
      <h3 className="text-md font-semibold mb-4">{title}</h3>
      {children}
    </section>
  );
}
export function LienInformationPanel({ lien }: LienDetailPanelProps) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg">
      <div className="px-6 py-5">
        <Section title="Lien Information">
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
          </div>
        </Section>
      </div>
    </div>
  );
}
