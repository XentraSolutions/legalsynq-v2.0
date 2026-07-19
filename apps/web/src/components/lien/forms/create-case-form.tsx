"use client";

import { useCallback, useEffect, useState } from "react";
import { FormModal } from "@/components/lien/modal";
import { useLienStore } from "@/stores/lien-store";
import { type CreateCaseRequestDto } from "@/lib/cases";
import { ApiError } from "@/lib/api-client";
import { getCreateCaseFormErrors } from "./create-case-form-validator";
import Field from "../field";
import { contactsService } from "@/lib/contacts";
import { useSessionContext } from "@/providers/session-provider";
import { dateConverter } from "@/lib/cases/cases.mapper";
import { ContactEntitySelect } from "@/components/lien/contact-entity-select";
import { useCreateCase } from "@/hooks/use-case-liens";
import { LitigationStatusForm } from "./litigation-form";
import { DropdownOption } from "@/lib/lookup/lookup.types";

interface CreateCaseFormProps {
  caseNumber?: string;
  open: boolean;
  onClose: () => void;
  onCreated?: (data: any) => void;
}

const INITIAL_FORM = {
  caseNumber: "",
  clientFirstName: "",
  clientLastName: "",
  externalReference: "",
  title: "",
  clientDob: "",
  clientPhone: "",
  clientEmail: "",
  clientAddress: "",
  clientCity: "",
  clientState: "",
  clientZipcode: "",
  dateOfIncident: "",
  insuranceCarrier: "",
  policyNumber: "",
  claimNumber: "",
  description: "",
  notes: "",
  caseStatusId: "PreDemand",
  caseManagerId: "",
  lawfirmId: "",
  accidentTypeId: "",
  accidentStateId: "",
  isServicing: "true",
};

export function CreateCaseForm({
  caseNumber,
  open,
  onClose,
  onCreated,
}: CreateCaseFormProps) {
  const { lookup } = useSessionContext();
  const { mutateAsync: createCase, isPending } = useCreateCase();

  const addToast = useLienStore((s) => s.addToast);
  const [form, setForm] = useState({
    ...INITIAL_FORM,
    caseNumber: caseNumber ?? "",
  });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);
  const [data, setData] = useState<{
    state: Array<{ key: string; value: string; label: string }>;
    accidentState: Array<{ key: string; value: string; label: string }>;
    status: Array<{ key: string; value: string; label: string }>;
    accidentType: Array<{ key: string; value: string; label: string }>;
  }>({
    state: [],
    accidentState: [],
    status: [],
    accidentType: [],
  });

  const [caseManagerRoleCode, setCaseManagerRoleCode] = useState<string>();

  const [isValid, setIsValid] = useState(false);
  const [showLitigationForm, setShowLitigationForm] = useState(false);

  const [touched, setTouched] = useState<
    Record<keyof typeof INITIAL_FORM, boolean>
  >(
    Object.keys(INITIAL_FORM).reduce(
      (acc, key) => ({ ...acc, [key as keyof typeof INITIAL_FORM]: false }),
      {} as Record<keyof typeof INITIAL_FORM, boolean>,
    ),
  );

  const updateField = (field: keyof typeof INITIAL_FORM, value: string) => {
    console.log(field, value);
    setForm((prev) => ({ ...prev, [field]: value }));
    setTouched((prev) => ({ ...prev, [field]: true }));
  };

  const validate = () => {
    const newErrors = getCreateCaseFormErrors(form);

    setErrors(newErrors);
    const valid = Object.keys(newErrors).length === 0;
    setIsValid(valid);
    return valid;
  };

  const fetchData = useCallback(async () => {
    setData((prev) => ({
      ...prev,
      status:
        lookup?.CaseStatus?.map((c) => {
          return { key: c.id, value: c.code, label: c.name };
        }) ?? [],
      state:
        lookup?.State?.map((c) => {
          return { key: c.id, value: c.code, label: c.code };
        }) ?? [],
      accidentState:
        lookup?.State?.map((c) => {
          return { key: c.id, value: c.code, label: c.code };
        }) ?? [],
      accidentType:
        lookup?.AccidentType?.map((c) => {
          return { key: c.id, value: c.id, label: c.name };
        }) ?? [],
    }));
  }, [lookup]);

  useEffect(() => {
    if (!Object.values(touched).some(Boolean)) return;

    const debounceId = window.setTimeout(() => {
      validate();
    }, 250);

    return () => window.clearTimeout(debounceId);
  }, [form, touched]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  useEffect(() => {
    contactsService.getCaseManagerRoleCode().then(setCaseManagerRoleCode);
  }, []);

  const formatDate = (dateString: string) => {
    const input = dateString;
    const date = new Date(input); // parse string into Date
    const formatter = new Intl.DateTimeFormat("en-CA"); // en-CA gives YYYY-MM-DD
    return formatter.format(date);
  };

  const handleSubmit = async () => {
    if (!validate()) {
      setTouched(
        Object.keys(INITIAL_FORM).reduce(
          (acc, key) => ({ ...acc, [key as keyof typeof INITIAL_FORM]: true }),
          {} as Record<keyof typeof INITIAL_FORM, boolean>,
        ),
      );
      return;
    }
    setSubmitting(true);
    try {
      const request: CreateCaseRequestDto = {
        // caseNumber: form.caseNumber.trim(),
        firstname: form.clientFirstName.trim(),
        lastname: form.clientLastName.trim(),
        externalReference: form.externalReference.trim(),
        title: form.title.trim() || undefined,
        dob: dateConverter(form.clientDob) || undefined,
        phone: form.clientPhone.trim(),
        email: form.clientEmail.trim(),
        address: form.clientAddress.trim(),
        city: form.clientCity.trim(),
        state: form.clientState,
        zipcode: form.clientZipcode,
        dateOfLoss: dateConverter(form.dateOfIncident) || undefined,
        insuranceCarrier: form.insuranceCarrier.trim() || undefined,
        policyNumber: form.policyNumber.trim(),
        claimNumber: form.claimNumber.trim(),
        description: form.notes.trim() || undefined,
        notes: form.notes.trim(),
        caseStatusId: form.caseStatusId,
        lawfirmId: form.lawfirmId || undefined,
        accidentTypeId: form.accidentTypeId || undefined,
        accidentStateId:
          data.accidentState.find((s) => s.value == form.accidentStateId)
            ?.key ?? "",
        caseManagerId: form.caseManagerId || undefined,
        isServicing: form.isServicing == "true",
        caseType: form.accidentTypeId || undefined,
        dateOfIncident: dateConverter(form.dateOfIncident) || undefined,
        stateOfIncident: form.accidentStateId || undefined,
        minorComp: isMinor() ? "true" : "false",
      };

      const res = await createCase(request);
      addToast({
        type: "success",
        title: "Case Created",
        description: `Case has been created.`,
      });

      setTimeout(() => {
        onCreated?.(res.id);
        setForm({ ...INITIAL_FORM });
        setErrors({});
      }, 500);
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isConflict) {
          addToast({
            type: "error",
            title: "Create Failed",
            description: err.message,
          });
        } else {
          addToast({
            type: "error",
            title: "Create Failed",
            description: err.message,
          });
        }
      } else {
        addToast({
          type: "error",
          title: "Create Failed",
          description: "An unexpected error occurred",
        });
      }
    } finally {
      setSubmitting(false);
    }
  };

  const reset = () => {
    setForm({ ...INITIAL_FORM });
    setErrors({});
    setIsValid(false);
    setTouched(
      Object.keys(INITIAL_FORM).reduce(
        (acc, key) => ({ ...acc, [key as keyof typeof INITIAL_FORM]: false }),
        {} as Record<keyof typeof INITIAL_FORM, boolean>,
      ),
    );
    onClose();
  };

  const checkStatus = (caseStatus: string) => {
    if (caseStatus.toLowerCase().includes("litigation")) {
      setShowLitigationForm(true);
    }
  };

  const isMinor = () => {
    let result = false;
    let age = 0;
    // Convert strings to Date objects
    const dob = new Date(form.clientDob);
    const dol = new Date(form.dateOfIncident);
    if (isNaN(dob.getTime()) || isNaN(dol.getTime())) {
      age = 0; // invalid date
      return;
    }
    age = dol.getFullYear() - dob.getFullYear();
    // Adjust if the birthday hasn't occurred yet this year
    const m = dol.getMonth() - dob.getMonth();
    if (m < 0 || (m === 0 && dol.getDate() < dob.getDate())) {
      age--;
    }
    age = age;
    result = age < 18;
    return result;
  };

  const setLitigationStatus = (status: DropdownOption) => {
    setData((prev) => ({
      ...prev,
      status: prev.status.map((s) =>
        s.value.includes("Litigation")
          ? { ...s, value: status.value, label: `Litigation (${status.label})` }
          : s,
      ),
    }));
    updateField("caseStatusId", status.value);
    setShowLitigationForm(false);
  };

  return (
    <>
      <FormModal
        open={open}
        onClose={reset}
        onSubmit={handleSubmit}
        title="Create Case"
        subtitle="Add a new case to the system"
        submitLabel={submitting ? "Creating..." : "Create Case"}
        submitDisabled={!isValid}
      >
        <div className="space-y-4">
          <div className="col-12 mb-6 mt-2">
            <span className="inline-block w-[30px] text-center text-white mr-2 rounded bg-[#0a375a]">
              <i className="ri-user-3-line text-light" />
            </span>
            <span className="font-semibold mb-2 mt-1">
              Personal Information
            </span>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <Field
              label="First Name"
              required
              value={form.clientFirstName}
              onChange={(v) => updateField("clientFirstName", v.toString())}
              error={
                touched.clientFirstName ? errors.clientFirstName : undefined
              }
              placeholder="First name"
            />
            <Field
              label="Last Name"
              required
              value={form.clientLastName}
              onChange={(v) => updateField("clientLastName", v.toString())}
              error={touched.clientLastName ? errors.clientLastName : undefined}
              placeholder="Last name"
            />
          </div>
          <Field
            label="Date of Birth"
            required
            value={form.clientDob}
            onChange={(v) => {
              console.log(v);
              updateField("clientDob", v.toString());
            }}
            error={touched.clientDob ? errors.clientDob : undefined}
            type="date"
            maxDate={new Date()}
          />
          <div className="grid grid-cols-2 gap-3">
            <Field
              label="Address"
              value={form.clientAddress}
              onChange={(v) => updateField("clientAddress", v.toString())}
              error={touched.clientAddress ? errors.clientAddress : undefined}
              placeholder="Address"
            />
            <Field
              label="City"
              value={form.clientCity}
              onChange={(v) => updateField("clientCity", v.toString())}
              error={touched.clientCity ? errors.clientCity : undefined}
              placeholder="City"
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <Field
              label="State"
              value={form.clientState}
              options={data.state}
              onChange={(v: string) => updateField("clientState", v.toString())}
              error={touched.clientState ? errors.clientState : undefined}
              placeholder="State"
              type="select"
            />

            <Field
              label="Zipcode"
              value={form.clientZipcode}
              onChange={(v) => updateField("clientZipcode", v.toString())}
              error={touched.clientZipcode ? errors.clientZipcode : undefined}
              placeholder="Zipcode"
            />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <Field
              type="checkbox"
              label="SERVICING"
              isChecked={form.isServicing == "true"}
              onChange={(v) => setForm({ ...form, isServicing: v.toString() })}
            />
          </div>
          <div className="col-12 mb-6 mt-6">
            <span className="inline-block w-[30px] text-center text-white mr-2 rounded bg-[#EE712F]">
              <i className="ri-survey-line text-light" />
            </span>
            <span className="font-semibold mb-2 mt-1">Case Information</span>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <Field
              label="Status"
              required
              value={form.caseStatusId}
              options={data?.status}
              placeholder=""
              onChange={(v: string) => {
                checkStatus(v);
                setForm({
                  ...form,
                  caseStatusId: v.toString(),
                });
              }}
              type="select"
            />
            <Field
              label="Accident Type"
              required
              value={form.accidentTypeId}
              options={data?.accidentType}
              placeholder=""
              onChange={(v: string) => {
                setForm({
                  ...form,
                  accidentTypeId: v.toString(),
                });
              }}
              type="select"
            />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <Field
              label="Accident State"
              required
              value={form.accidentStateId}
              options={data?.accidentState}
              placeholder=""
              onChange={(v: string) => {
                setForm({
                  ...form,
                  accidentStateId: v.toString(),
                });
              }}
              type="select"
            />
            <Field
              label="Date of Loss"
              value={form.dateOfIncident}
              onChange={(v) => updateField("dateOfIncident", v.toString())}
              type="date"
              maxDate={new Date()}
            />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Law Firm<span className="text-red-500 ml-0.5">*</span>
              </label>
              <ContactEntitySelect
                contactType="LawFirm"
                value={form.lawfirmId}
                onChange={(v) => {
                  setForm((prev) => ({
                    ...prev,
                    lawfirmId: v,
                    caseManagerId: "",
                  }));
                  setTouched((prev) => ({ ...prev, lawfirmId: true }));
                }}
                error={Boolean(touched.lawfirmId && errors.lawfirmId)}
                placeholder="Select law firm..."
                searchPlaceholder="Search law firms..."
                allowCreate
                createLabel="Add New Law Firm"
              />
              {touched.lawfirmId && errors.lawfirmId && (
                <p className="text-xs text-red-500 mt-1">{errors.lawfirmId}</p>
              )}
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Case Manager
              </label>
              <ContactEntitySelect
                contactType="LawFirm"
                contactSubtype={caseManagerRoleCode}
                lawFirmId={form.lawfirmId}
                requireParent
                parentHint="Select a law firm first"
                value={form.caseManagerId}
                onChange={(v) => setForm({ ...form, caseManagerId: v })}
                placeholder="Select case manager..."
                searchPlaceholder="Search case managers..."
                allowCreate
                createLabel="Add Case Manager"
              />
            </div>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Notes
            </label>
            <textarea
              value={form.notes}
              onChange={(e) => updateField("notes", e.target.value)}
              placeholder="Brief case notes (optional)"
              rows={3}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary resize-none"
            />
          </div>
        </div>
      </FormModal>

      {showLitigationForm && (
        <LitigationStatusForm
          open={showLitigationForm}
          onClose={() => setShowLitigationForm(false)}
          onSubmitted={(v: DropdownOption) => {
            setLitigationStatus(v);
          }}
        />
      )}
    </>
  );
}
