import {
  LienDetail,
  LienDetailsResult,
  LienStatusHistoryItem,
} from "@/types/lien-selling";
import { LienStatusBadge } from "../lien/lien-status-badge";
import { LienInformationPanel } from "./lien-detail/lien-information-panel";
import { FundingCompanyAndCaseInformationPanel } from "./lien-detail/funding-company-information-panel";
import { MedicalCodesInformationPanel } from "./lien-detail/medical-codes-information-panel";

interface LienDetailPanelProps {
  lien: LienDetailsResult;
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
      <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4">
        {title}
      </h3>
      {children}
    </section>
  );
}
export function PortfolioDetailPanel({ lien }: LienDetailPanelProps) {
  return (
    <>
      <LienInformationPanel lien={lien.lienInformation}></LienInformationPanel>
      <FundingCompanyAndCaseInformationPanel
        lien={{
          ...lien.caseInformation,
          fundingCompany: lien.fundingCompany,
          contactPerson: lien.contactPerson,
          email: lien.email,
        }}
      ></FundingCompanyAndCaseInformationPanel>
      <MedicalCodesInformationPanel
        lien={lien.medicalPricing.rows}
      ></MedicalCodesInformationPanel>
    </>
  );
}
