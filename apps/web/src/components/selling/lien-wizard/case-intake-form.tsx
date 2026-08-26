"use client";

import { useEffect, useMemo, useState } from "react";
import Field from "@/components/lien/field";
import { SellingEntitySelect } from "@/components/selling/selling-entity-select";
import { useSessionContext } from "@/providers/session-provider";
import type {
  CreateSellingCaseDraftRequest,
  FinalizeSellingCaseDraftPlaintiffRequest,
} from "@/lib/selling/liens.types";

const EMPTY_CASE_FORM: CreateSellingCaseDraftRequest = {
  caseStatus: "",
  accidentTypeId: "",
  accidentState: "",
  dateOfLoss: "",
  handlingLawFirmId: "",
  caseManagerId: "",
  caseTrackingNotes: "",
};

const EMPTY_PLAINTIFF_FORM: FinalizeSellingCaseDraftPlaintiffRequest = {
  firstName: "",
  lastName: "",
  birthdate: "",
  email: "",
  phone: "",
  gender: "",
  address: "",
  city: "",
  state: "",
  zipcode: "",
};

function lookupOptions(items: { id: string; code: string; name: string }[]) {
  return items.map((item) => ({
    key: item.id,
    value: item.id,
    label: item.name,
  }));
}

export function CaseIntakeForm({
  onFormValid,
}: {
  onFormValid: (valid: boolean, form: CreateSellingCaseDraftRequest) => void;
}) {
  const { lookup } = useSessionContext();
  const [form, setForm] = useState<CreateSellingCaseDraftRequest>(EMPTY_CASE_FORM);

  const caseStatusOptions = useMemo(
    () =>
      (lookup?.CaseStatus ?? []).map((item) => ({
        key: item.id,
        value: item.code,
        label: item.name,
      })),
    [lookup?.CaseStatus],
  );
  const accidentTypeOptions = useMemo(
    () => lookupOptions(lookup?.AccidentType ?? []),
    [lookup?.AccidentType],
  );
  const stateOptions = useMemo(
    () =>
      (lookup?.State ?? []).map((item) => ({
        key: item.id,
        value: item.code,
        label: item.name || item.code,
      })),
    [lookup?.State],
  );

  useEffect(() => {
    onFormValid(Boolean(form.caseStatus), form);
  }, [form, onFormValid]);

  const update = (patch: Partial<CreateSellingCaseDraftRequest>) =>
    setForm((current) => ({ ...current, ...patch }));

  return (
    <div className="space-y-4 pb-3">
      <div>
        <h1 className="font-semibold text-2xl">Case Information</h1>
        <p className="mt-1 text-sm text-gray-600">
          Start by recording the case details before adding lien information.
        </p>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Field
          type="select"
          label="Case Status"
          required
          value={form.caseStatus}
          options={caseStatusOptions}
          placeholder="Select case status..."
          onChange={(value: string) => update({ caseStatus: value })}
        />
        <Field
          type="select"
          label="Accident Type"
          value={form.accidentTypeId}
          options={accidentTypeOptions}
          placeholder="Select accident type..."
          onChange={(value: string) => update({ accidentTypeId: value })}
        />
        <Field
          type="select"
          label="Accident State"
          value={form.accidentState}
          options={stateOptions}
          placeholder="Select accident state..."
          onChange={(value: string) => update({ accidentState: value })}
        />
        <Field
          type="date"
          label="Date of Loss"
          value={form.dateOfLoss}
          maxDate={new Date()}
          onChange={(value) => update({ dateOfLoss: value })}
        />
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">
            Law Firm
          </label>
          <SellingEntitySelect
            entityType="LawFirm"
            value={form.handlingLawFirmId}
            onChange={(value) =>
              update({ handlingLawFirmId: value, caseManagerId: "" })
            }
            placeholder="Select law firm..."
            searchPlaceholder="Search law firms..."
            allowCreate
            createLabel="Add New Law Firm"
          />
        </div>
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">
            Case Manager
          </label>
          <SellingEntitySelect
            entityType="LawFirm"
            contactType="CaseManager"
            companyId={form.handlingLawFirmId}
            isContactPerson
            requireParent
            parentHint="Select a law firm first"
            value={form.caseManagerId}
            onChange={(value) => update({ caseManagerId: value })}
            placeholder="Select case manager..."
            searchPlaceholder="Search case managers..."
            allowCreate
            createLabel="Add Case Manager"
          />
        </div>
      </div>
      <Field
        type="textarea"
        label="Case Tracking Notes"
        value={form.caseTrackingNotes}
        placeholder="Brief case notes (optional)"
        onChange={(value) => update({ caseTrackingNotes: value })}
      />
    </div>
  );
}

export function PlaintiffIntakeForm({
  onFormValid,
}: {
  onFormValid: (
    valid: boolean,
    form: FinalizeSellingCaseDraftPlaintiffRequest,
  ) => void;
}) {
  const { lookup } = useSessionContext();
  const [form, setForm] = useState<FinalizeSellingCaseDraftPlaintiffRequest>(
    EMPTY_PLAINTIFF_FORM,
  );
  const stateOptions = useMemo(
    () =>
      (lookup?.State ?? []).map((item) => ({
        key: item.id,
        value: item.code,
        label: item.name || item.code,
      })),
    [lookup?.State],
  );

  useEffect(() => {
    onFormValid(Boolean(form.firstName.trim() && form.lastName.trim()), form);
  }, [form, onFormValid]);

  const update = (patch: Partial<FinalizeSellingCaseDraftPlaintiffRequest>) =>
    setForm((current) => ({ ...current, ...patch }));

  return (
    <div className="space-y-4 pb-3">
      <div>
        <h1 className="font-semibold text-2xl">Plaintiff</h1>
        <p className="mt-1 text-sm text-gray-600">
          Add the plaintiff to finish creating the case.
        </p>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Field
          label="First Name"
          required
          value={form.firstName}
          placeholder="First name"
          onChange={(value) => update({ firstName: value })}
        />
        <Field
          label="Last Name"
          required
          value={form.lastName}
          placeholder="Last name"
          onChange={(value) => update({ lastName: value })}
        />
        <Field
          type="date"
          label="Birthdate"
          value={form.birthdate}
          maxDate={new Date()}
          onChange={(value) => update({ birthdate: value })}
        />
        <Field
          type="select"
          label="Gender"
          value={form.gender}
          options={[
            { key: "female", value: "Female", label: "Female" },
            { key: "male", value: "Male", label: "Male" },
            { key: "non-binary", value: "Non-binary", label: "Non-binary" },
            {
              key: "prefer-not-to-say",
              value: "Prefer not to say",
              label: "Prefer not to say",
            },
          ]}
          placeholder="Select gender..."
          onChange={(value: string) => update({ gender: value })}
        />
        <Field
          type="email"
          label="Email"
          value={form.email}
          placeholder="email@example.com"
          onChange={(value) => update({ email: value })}
        />
        <Field
          type="tel"
          label="Phone"
          value={form.phone ?? ""}
          onChange={(value) => update({ phone: value })}
        />
        <Field
          label="Address"
          value={form.address}
          placeholder="Street address"
          onChange={(value) => update({ address: value })}
        />
        <Field
          label="City"
          value={form.city}
          placeholder="City"
          onChange={(value) => update({ city: value })}
        />
        <Field
          type="select"
          label="State"
          value={form.state}
          options={stateOptions}
          placeholder="Select state..."
          onChange={(value: string) => update({ state: value })}
        />
        <Field
          label="Zipcode"
          value={form.zipcode}
          placeholder="Zipcode"
          onChange={(value) => update({ zipcode: value })}
        />
      </div>
    </div>
  );
}
