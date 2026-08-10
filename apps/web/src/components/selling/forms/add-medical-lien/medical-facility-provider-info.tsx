import React, { useEffect, useState } from "react";
import { ContactEntitySelect } from "@/components/lien/contact-entity-select";
import { SellingEntitySelect } from "@/components/selling/selling-entity-select";
import Field from "@/components/lien/field";

export interface MedicalFacilityProviderInfoProps {
  caseId?: string;
  lienId?: string;
  data?: any;
  onFormValid?: (valid: boolean, data?: any) => void;
  openAddFundingCompanyModal?: () => void;
}

interface MedicalFacilityFormState {
  liensId: string;
  facilityId: string;
  facility: string;
  facilityContactId: string;
  facilityContact: string;
  email: string;
  medicalProviderId: string;
  medicalProvider: string;
}

const INITIAL_FORM = {
  liensId: "",
  facilityId: "",
  facility: "",
  facilityContactId: "",
  facilityContact: "",
  email: "",
  medicalProviderId: "",
  medicalProvider: "",
};

const FACILITY_CONTACT_SUBTYPE = "FacilityContactPerson";

export default function MedicalFacilityProviderInfo(
  props: MedicalFacilityProviderInfoProps,
) {
  const { data = {}, lienId, onFormValid } = props;
  const [form, setForm] = useState<MedicalFacilityFormState>(
    data ? { ...data, lienId: lienId } : { ...INITIAL_FORM, lienId: lienId },
  );

  function validateForm() {
    const isValid = !!form.facilityId;
    onFormValid?.(isValid, form);
  }

  const [isDirty, setIsDirty] = useState(false);

  const updateForm = (updates: Partial<MedicalFacilityFormState>) => {
    setIsDirty(true);

    setForm((prev) => ({
      ...prev,
      ...updates,
    }));
  };

  useEffect(() => {
    if (!isDirty) return;

    validateForm();
  }, [form, isDirty]);

  return (
    <div className="container-fluid">
      <div className="row border-bottom border-solid">
        <div className="col-12 mb-2">
          <span className="inline-block w-[30px] text-center text-white mr-2 rounded bg-primary">
            <i className="ri-stethoscope-line text-light" />
          </span>
          <span className="font-semibold mb-2 mt-1">
            Medical Facility and Provider Information{" "}
          </span>
        </div>

        <div className="col-12 mb-2 mt-4 font-semibold">
          <span className="fw-semibold mb-2">Medical Facility</span>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mt-4 mx-2">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Facility Name<span className="text-red-500 ml-0.5">*</span>
            </label>
            <SellingEntitySelect
              entityType="Facility"
              value={form.facilityId}
              onChange={(v, option) =>
                updateForm({
                  ...form,
                  facilityId: v,
                  facility: option.label,
                  facilityContactId: "",
                  facilityContact: "",
                })
              }
              placeholder="Select facility..."
              searchPlaceholder="Search facilities..."
              allowCreate
              createLabel="Add New Medical Facility"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Select Contact Person
            </label>
            <ContactEntitySelect
              contactType="MedicalFacility"
              contactSubtype={FACILITY_CONTACT_SUBTYPE}
              facilityId={form.facilityId}
              requireParent
              parentHint="Select a facility first"
              value={form.facilityContactId}
              onChange={(v, option) =>
                updateForm({
                  ...form,
                  facilityContactId: v,
                  facilityContact: option.label,
                })
              }
              placeholder="Select contact person..."
              searchPlaceholder="Search contacts..."
              allowCreate
              createLabel="Add New Contact Person"
            />
          </div>
        </div>
        <div className="grid grid-cols-1 gap-4 mt-4 mx-2">
          <Field
            type="email"
            label="Email Address"
            value={form.email}
            onChange={(v) => updateForm({ ...form, email: v.toString() })}
          />
        </div>

        <div className="col-12 mb-2 mt-6 font-semibold">
          <span className="fw-semibold mb-2 mt-1">Medical Provider</span>
        </div>

        <div className="grid grid-cols-1 gap-4 mt-4 mx-2">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Provider Name
            </label>
            <ContactEntitySelect
              contactType="Provider"
              value={form.medicalProviderId}
              onChange={(v, option) =>
                updateForm({
                  ...form,
                  medicalProviderId: v,
                  medicalProvider: option.label,
                })
              }
              placeholder="Select provider..."
              searchPlaceholder="Search providers..."
              allowCreate
              createLabel="Add New Provider"
            />
          </div>
        </div>
      </div>
    </div>
  );
}
