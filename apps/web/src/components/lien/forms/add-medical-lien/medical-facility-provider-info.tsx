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

type DropdownData = {
  status: Array<Record<string, string>>;
};

export default function MedicalFacilityProviderInfo(
  props: MedicalFacilityProviderInfoProps,
) {
  const { data = {}, onFormValid, openAddFundingCompanyModal } = props;

  const [form, setForm] = useState(data ?? { ...INITIAL_FORM });
  const [errors, setErrors] = useState<Record<string, string>>({});

  const [facilityContactList, setFacilityContactList] = useState<any[]>([]);
  const [facilityList, setFacilityList] =
    useState<Array<Record<string, string>>>();
  const [providerList, setProviderList] =
    useState<Array<Record<string, string>>>();
  const [showCreate, setShowCreate] = useState<boolean>(false);

  useEffect(() => {
    loadFacilities();
    loadMedicalProviders();

    if (form.facility) {
      loadContactPersons();
    }
  }, [form.facility]);

  useEffect(() => {
    validateForm();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [form]);

  const loadContactPersons = useCallback(async () => {
    try {
      const contactsRes = await facilityService.getContactPersonByFacility(
        form.facilityId,
      );
      const list = contactsRes.map((c) => {
        return {
          key: c.id,
          value: c.id,
          label: `${c.firstName} ${c.lastName}`,
        };
      });
      console.log(list);
      setFacilityContactList(list ?? []);
    } catch (e) {
      setFacilityContactList([]);
    }
  }, [form.facility, facilityContactList]);

  async function loadFacilities() {
    try {
      const facilityRes = await lookupService.getMedicalFacility();
      const list = facilityRes.items.map((c) => {
        return { key: c.id, value: c.id, label: c.name };
      });
      setFacilityList(list ?? []);
    } catch (e) {
      setFacilityList([]);
    }
  }

  async function loadMedicalProviders() {
    try {
      const providerRes = await lookupService.getMedicalProviders();
      const list = providerRes.items.map((c) => {
        return { key: c.id, value: c.id, label: c.organization };
      });
      setProviderList(list ?? []);
    } catch (e) {
      setProviderList([]);
    }
  }

  function validateForm() {
    console.log(form);
    onFormValid?.(true, form);
  }

  const getFacilityName = (id) => {
    if (facilityList) {
      return facilityList.find((f) => f.value == id).label;
    }
  };

  const getContactName = (id) => {
    if (facilityContactList) {
      return facilityContactList.find((f) => f.value == id).label;
    }
  };

  const getProviderName = (id) => {
    if (providerList) {
      return providerList.find((f) => f.value == id).label;
    }
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
            value={form.facility}
            options={facilityList}
            onChange={(v) => {
              console.log(v);
              setForm({
                ...form,
                facility: getFacilityName(v.toString()),
                facilityId: v.toString(),
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
              setForm({
                ...form,
                facilityContact: getContactName(v.toString()),
                facilityContactId: v,
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
            onChange={(v) =>
              setForm({
                ...form,
                medicalProvider: getProviderName(v.toString()),
                medicalProviderId: v,
              })
            }
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
      {showCreate && (
        <CreateMedicalFacilityContactPerson
          open={showCreate}
          data={facilityList}
          onClose={() => setShowCreate(false)}
          onCreated={() => {
            loadFacilities();
            loadContactPersons();
            setShowCreate(false);
          }}
        />
      )}
    </div>
  );
}
