import { useEffect } from "react";
import { SellingEntitySelect } from "@/components/selling/selling-entity-select";
import { useContactPersons } from "@/hooks/selling/use-selling-companies";

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
        <label className="block text-sm font-medium text-gray-700 mb-1">
          Contact Person
        </label>
        {value.fundingCompanyId ? (
          <FundingCompanyContactSelect
            companyId={value.fundingCompanyId}
            value={value.fundingCompanyContactId}
            onChange={(contactId, contactName) =>
              onChange({
                fundingCompanyContactId: contactId,
                fundingCompanyContact: contactName,
              })
            }
          />
        ) : (
          <p className="text-xs text-gray-400 border border-gray-200 rounded-lg px-3 py-2">
            Select a funding company first
          </p>
        )}
      </div>
    </div>
  );
}

/**
 * Contact person picker for a funding company. Auto-selects the (single)
 * contact once it loads, same as before — a funding company only ever has
 * one contact on file today — but renders as a `SellingEntitySelect`
 * dropdown so its empty/selected states match the sibling fields on this
 * form instead of the standalone alert-style card used by the sell-lien
 * buyer-selection step.
 */
function FundingCompanyContactSelect({
  companyId,
  value,
  onChange,
}: {
  companyId: string;
  value?: string;
  onChange: (contactId: string, contactName: string) => void;
}) {
  const contactPersonsQuery = useContactPersons(companyId, true);
  const firstContact = contactPersonsQuery.data?.[0] ?? null;

  useEffect(() => {
    if (contactPersonsQuery.isLoading) return;
    if (firstContact) {
      if (value !== firstContact.id) onChange(firstContact.id, firstContact.displayName);
    } else if (value) {
      onChange("", "");
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [contactPersonsQuery.isLoading, firstContact?.id]);

  return (
    <SellingEntitySelect
      isContactPerson
      entityType="FundingCompany"
      companyId={companyId}
      value={value}
      onChange={(v, option) => onChange(v, option?.label ?? "")}
      placeholder="Select contact person..."
      searchPlaceholder="Search contact persons..."
      allowCreate
      createLabel="Add Contact Person"
      // Intentional: a funding company is meant to have exactly one contact
      // person, so once one exists the field only displays it — it doesn't
      // let the user swap it for another. If that constraint is dropped,
      // remove this and let the dropdown stay interactive once populated.
      disabled={Boolean(firstContact)}
    />
  );
}
