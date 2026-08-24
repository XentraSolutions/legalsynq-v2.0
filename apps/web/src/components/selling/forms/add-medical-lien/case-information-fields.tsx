import { SellingEntitySelect } from "@/components/selling/selling-entity-select";

export interface CaseInformationFieldsValue {
  medicalProviderId: string;
  medicalProvider?: string;
  fundingCompanyId: string;
  fundingCompany?: string;
  fundingCompanyContactId: string;
  fundingCompanyContact?: string;
  lawfirmId: string;
  caseManagerId: string;
}

// Shared by FundingCompanyInfo (add/edit step-1's "Case Information"
// section) and EditCaseInformationModal (lien detail page) — both capture
// the same medical provider / funding company / contact / law firm / case
// manager selections for a lien's case.
export function CaseInformationFields({
  value,
  onChange,
  // Law firm is required everywhere this drives step/form validity (both
  // the add/edit wizard and the lien detail page's edit modal). Funding
  // company is optional since it defaults to us when left unspecified.
  required = false,
}: {
  value: CaseInformationFieldsValue;
  onChange: (patch: Partial<CaseInformationFieldsValue>) => void;
  required?: boolean;
}) {
  return (
    <div className="grid grid-cols-2 gap-4">
      <div className="col-span-2">
        <label className="block text-sm font-medium text-gray-700 mb-1">
          Medical Provider
        </label>
        <SellingEntitySelect
          entityType="MedicalProvider"
          value={value.medicalProviderId}
          onChange={(v, option) =>
            onChange({
              medicalProviderId: v,
              medicalProvider: option?.label ?? "",
            })
          }
          placeholder="Select medical provider..."
          searchPlaceholder="Search medical providers..."
          allowCreate
          createLabel="Add Medical Provider"
        />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">
          Funding Company
        </label>
        <SellingEntitySelect
          entityType="FundingCompany"
          value={value.fundingCompanyId}
          onChange={(v, option) =>
            onChange({
              fundingCompanyId: v,
              fundingCompany: option?.label ?? "",
              fundingCompanyContactId: "",
              fundingCompanyContact: "",
            })
          }
          placeholder="Select funding company..."
          searchPlaceholder="Search funding companies..."
          allowCreate
          createLabel="Add Funding Company"
        />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">
          Contact Person
        </label>
        <SellingEntitySelect
          entityType="FundingCompany"
          companyId={value.fundingCompanyId}
          isContactPerson
          requireParent
          parentHint="Select a funding company first"
          value={value.fundingCompanyContactId}
          onChange={(v, option) =>
            onChange({
              fundingCompanyContactId: v,
              fundingCompanyContact: option?.label ?? "",
            })
          }
          placeholder="Select contact person..."
          searchPlaceholder="Search contacts..."
          allowCreate
          createLabel="Add New Contact Person"
        />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">
          Handling Law Firm
          {required && (
            <span className="text-red-500 ml-0.5">*</span>
          )}
        </label>
        <SellingEntitySelect
          entityType="LawFirm"
          value={value.lawfirmId}
          onChange={(v) => onChange({ lawfirmId: v, caseManagerId: "" })}
          placeholder="Select law firm..."
          searchPlaceholder="Search law firms..."
          allowCreate
          createLabel="Add New Law Firm"
        />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">
          Case Manager
        </label>
        <SellingEntitySelect
          entityType="LawFirm"
          contactType="CaseManager"
          companyId={value.lawfirmId}
          isContactPerson
          requireParent
          parentHint="Select a law firm first"
          value={value.caseManagerId}
          onChange={(v) => onChange({ caseManagerId: v })}
          placeholder="Select case manager..."
          searchPlaceholder="Search case managers..."
          allowCreate
          createLabel="Add Case Manager"
        />
      </div>
    </div>
  );
}
