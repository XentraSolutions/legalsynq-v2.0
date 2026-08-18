import {
  LienCaseDetail,
  LienFacilityDetail,
  LienFundingCompanyDetail,
  LienMedicalProviderDetail,
} from "@/types/lien-selling";
import { PanelShell } from "./panel-shell";

interface LienDetailPanelProps {
  fundingCompany: LienFundingCompanyDetail | null;
  facility: LienFacilityDetail | null;
  medicalProvider: LienMedicalProviderDetail | null;
  caseInformation: LienCaseDetail | null;
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

export function FundingCompanyAndCaseInformationPanel({
  fundingCompany,
  facility,
  medicalProvider,
  caseInformation,
  onEdit,
}: LienDetailPanelProps) {
  return (
    <PanelShell title="Case Information" onEdit={onEdit}>
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-2 gap-x-6 gap-y-5">
        <Field label="Case Code" value={caseInformation?.caseNumber} />
        <Field label="Facility Name" value={facility?.name} />
        <Field label="Medical Provider" value={medicalProvider?.name} />
        <Field label="Handling Law Firm" value={caseInformation?.lawFirm} />
        <Field label="Funding Company" value={fundingCompany?.name} />
        <Field label="Case Manager" value={caseInformation?.caseManagerName} />
        <Field label="Contact Person" value={fundingCompany?.contact?.name} />
      </div>
    </PanelShell>
  );
}
