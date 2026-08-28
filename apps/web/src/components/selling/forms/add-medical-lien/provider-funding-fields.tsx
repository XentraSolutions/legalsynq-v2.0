import { SellingEntitySelect } from "@/components/selling/selling-entity-select";
import { FundingCompanyContactField } from "@/components/selling/funding-company-contact-field";

export interface ProviderFundingFieldsValue {
  medicalProviderId: string;
  medicalProvider?: string;
  facilityId?: string;
  facility?: string;
  fundingCompanyId: string;
  fundingCompany?: string;
  fundingCompanyContactId: string;
  fundingCompanyContact?: string;
}

// Shared by the provider-funding wizard step and detail-page modal. Case
// ownership belongs to the case-first intake flow; this endpoint only saves
// associations owned by the lien itself.
export function ProviderFundingFields({
  value,
  onChange,
}: {
  value: ProviderFundingFieldsValue;
  onChange: (patch: Partial<ProviderFundingFieldsValue>) => void;
}) {
  return (
    <div className="grid grid-cols-2 gap-4">
      <div>
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
          pendingName={value.medicalProviderId ? undefined : value.medicalProvider}
        />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">
          Medical Facility
        </label>
        <SellingEntitySelect
          entityType="MedicalFacility"
          value={value.facilityId ?? ""}
          onChange={(v, option) =>
            onChange({
              facilityId: v,
              facility: option?.label ?? "",
            })
          }
          placeholder="Select medical facility..."
          searchPlaceholder="Search medical facilities..."
          allowCreate
          createLabel="Add Medical Facility"
          pendingName={value.facilityId ? undefined : value.facility}
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
          pendingName={value.fundingCompanyId ? undefined : value.fundingCompany}
        />
      </div>
      <div>
        {value.fundingCompanyId ? (
          <FundingCompanyContactField
            companyId={value.fundingCompanyId}
            companyName={value.fundingCompany}
            value={value.fundingCompanyContactId}
            onChange={(contactId, contact) =>
              onChange({
                fundingCompanyContactId: contactId,
                fundingCompanyContact: contact?.displayName ?? "",
              })
            }
          />
        ) : (
          <>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Contact Person
            </label>
            <p className="text-xs text-gray-400 border border-gray-200 rounded-lg px-3 py-2">
              Select a funding company first
            </p>
          </>
        )}
      </div>
    </div>
  );
}
