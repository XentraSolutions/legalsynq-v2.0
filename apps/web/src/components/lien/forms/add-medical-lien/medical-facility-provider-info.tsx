import React, { useCallback, useEffect, useMemo, useState } from "react";
import Field from "../../field";
import { lookupService } from "@/lib/lookup";
import { facilityService } from "@/lib/facility";
import { CreateMedicalFacility } from "../add-medical-facility";
import { CreateMedicalFacilityContactPerson } from "../add-medical-facility-contact-person";

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

type DropdownOption = {
  key: string;
  value: string;
  label: string;
};

export default function MedicalFacilityProviderInfo(
  props: MedicalFacilityProviderInfoProps,
) {
  const { data = {}, lienId, onFormValid, openAddFundingCompanyModal } = props;
  const [form, setForm] = useState<MedicalFacilityFormState>(
    data ? { ...data, lienId: lienId } : { ...INITIAL_FORM, lienId: lienId },
  );
  const [errors, setErrors] = useState<Record<string, string>>({});

  const [facilityContactList, setFacilityContactList] = useState<
    DropdownOption[]
  >([]);
  const [facilityList, setFacilityList] = useState<DropdownOption[]>([]);
  const [providerList, setProviderList] = useState<DropdownOption[]>([]);
  const [showCreate, setShowCreate] = useState<boolean>(false);
  const [showCreateContact, setShowCreateContact] = useState<boolean>(false);

  useEffect(() => {
    loadFacilities();
    loadMedicalProviders();

    if (form.facilityId) {
      loadContactPersons();
    }
  }, [form.facilityId]);

  useEffect(() => {
    validateForm();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [form]);

  const loadContactPersons = useCallback(async () => {
    try {
      const contactsRes = await facilityService.getContactPersonByFacility(
        form.facilityId,
      );
      const list: DropdownOption[] = contactsRes.map((c) => ({
        key: c.id,
        value: c.id,
        label: `${c.firstName ?? ""} ${c.lastName ?? ""}`.trim(),
      }));
      if (form.facilityContactId) {
        const currentFacilityContact = list.find(
          (f) => f.key == form.facilityContactId,
        );
        if (currentFacilityContact)
          setForm((prev) => ({
            ...prev,
            facilityContact: currentFacilityContact?.label,
          }));
      }
      setFacilityContactList(list ?? []);
    } catch (e) {
      setFacilityContactList([]);
    }
  }, [form.facility, facilityContactList]);

  async function loadFacilities() {
    try {
      const facilityRes = await lookupService.getMedicalFacility();
      const list: DropdownOption[] = facilityRes.items.map((c) => ({
        key: c.id,
        value: c.id,
        label: String(c.name ?? ""),
      }));

      if (form.facilityId) {
        const currentFacility = list.find((f) => f.key == form.facilityId);
        if (currentFacility)
          setForm((prev) => ({ ...prev, facility: currentFacility?.label }));
      }
      setFacilityList(list ?? []);
    } catch (e) {
      setFacilityList([]);
    }
  }

  async function loadMedicalProviders() {
    try {
      const providerRes = await lookupService.getMedicalProviders();
      const list: DropdownOption[] = providerRes.items.map((c) => ({
        key: c.id,
        value: c.id,
        label: String(c.organization ?? ""),
      }));
      setProviderList(list ?? []);
    } catch (e) {
      setProviderList([]);
    }
  }

  function validateForm() {
    onFormValid?.(true, form);
  }

  const resolveSelectedValue = (value: string | string[] | boolean): string => {
    if (Array.isArray(value)) return value[0] ?? "";
    if (typeof value === "string") return value;
    return "";
  };

  const getFacilityName = (id?: string | null): string => {
    return facilityList.find((f) => f.value === id)?.label ?? "";
  };

  const getContactName = (id?: string | null): string => {
    return facilityContactList.find((f) => f.value === id)?.label ?? "";
  };

  const getProviderName = (id?: string | null): string => {
    return providerList.find((f) => f.value === id)?.label ?? "";
  };

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
          <Field
            label="Facility Name"
            required
            value={form.facility}
            options={facilityList}
            onChange={(v) => {
              const selectedValue = resolveSelectedValue(v);
              setForm({
                ...form,
                facility: getFacilityName(selectedValue),
                facilityId: selectedValue,
              });
            }}
            type="select"
          >
            <button
              type="button"
              onClick={() => {
                setShowCreate(!showCreate);
              }}
              className="inline-flex items-center justify-center rounded-lg px-2 py-2 text-sm font-semibold text-primary disabled:cursor-not-allowed disabled:bg-gray-300"
            >
              Add New Medical Facility
            </button>
          </Field>

          <Field
            label="Select Contact Person"
            value={form.facilityContact}
            options={facilityContactList}
            onChange={(v) => {
              const selectedValue = resolveSelectedValue(v);
              setForm({
                ...form,
                facilityContact: getContactName(selectedValue),
                facilityContactId: selectedValue,
              });
            }}
            type="select"
          >
            <button
              type="button"
              onClick={() => {
                setShowCreateContact(!showCreate);
              }}
              className="inline-flex items-center justify-center rounded-lg px-2 py-2 text-sm font-semibold text-primary disabled:cursor-not-allowed disabled:bg-gray-300"
            >
              Add New Contact Person
            </button>
          </Field>
        </div>
        <div className="grid grid-cols-1 gap-4 mt-4 mx-2">
          <Field
            type="email"
            label="Email Address"
            value={form.email}
            onChange={(v) => setForm({ ...form, email: v.toString() })}
          />
        </div>

        <div className="col-12 mb-2 mt-6 font-semibold">
          <span className="fw-semibold mb-2 mt-1">Medical Provider</span>
        </div>

        <div className="grid grid-cols-1 gap-4 mt-4 mx-2">
          <Field
            label="Provider Name"
            value={form.medicalProvider}
            options={providerList}
            onChange={(v) => {
              const selectedValue = resolveSelectedValue(v);
              setForm({
                ...form,
                medicalProvider: getProviderName(selectedValue),
                medicalProviderId: selectedValue,
              });
            }}
            type="select"
          />
        </div>
      </div>

      {showCreate && (
        <CreateMedicalFacility
          open={showCreate}
          onClose={() => setShowCreate(false)}
          onCreated={() => {
            loadFacilities();
            setShowCreate(false);
          }}
        />
      )}
      {showCreateContact && (
        <CreateMedicalFacilityContactPerson
          open={showCreateContact}
          data={facilityList}
          onClose={() => setShowCreateContact(false)}
          onCreated={() => {
            loadFacilities();
            loadContactPersons();
            setShowCreateContact(false);
          }}
        />
      )}
    </div>
  );
}
