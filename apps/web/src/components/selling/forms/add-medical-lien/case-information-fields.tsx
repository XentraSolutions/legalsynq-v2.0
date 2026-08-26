import { SellingEntitySelect } from "@/components/selling/selling-entity-select";

export interface CaseInformationFieldsValue {
  medicalProviderId: string;
  medicalProvider?: string;
  fundingCompanyId: string;
  fundingCompany?: string;
  fundingCompanyContactId: string;
  fundingCompanyContact?: string;
}

// Shared by the lien-associations wizard step and detail-page modal. Case
// ownership belongs to the case-first intake flow; this endpoint only saves
// associations owned by the lien itself.
export function CaseInformationFields({
  value,
  onChange,
}: {
  value: CaseInformationFieldsValue;
  onChange: (patch: Partial<CaseInformationFieldsValue>) => void;
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
    </div>
  );
}
