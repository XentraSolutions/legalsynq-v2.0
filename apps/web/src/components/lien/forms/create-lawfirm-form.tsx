"use client";

import { useState } from "react";
import { FormModal } from "@/components/lien/modal";
import { useLienStore } from "@/stores/lien-store";
import { ApiError } from "@/lib/api-client";
import Field from "../field";
import { contactsService } from "@/lib/contacts";
import type { ContactDetail } from "@/lib/contacts";
import { useSessionContext } from "@/providers/session-provider";

interface CreateLawFirmFormProps {
  open: boolean;
  onClose: () => void;
  onCreated?: (created: ContactDetail) => void;
}

const INITIAL_FORM = {
  firstName: "",
  lastName: "",
  organization: "",
  email: "",
  phone: "",
  addressLine1: "",
  city: "",
  state: "",
  postalCode: "",
};

export function CreateLawFirmForm({
  open,
  onClose,
  onCreated,
}: CreateLawFirmFormProps) {
  const addToast = useLienStore((s) => s.addToast);
  const { lookup } = useSessionContext();
  const [form, setForm] = useState({ ...INITIAL_FORM });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  const states =
    lookup?.State?.map((s) => ({ key: s.id, value: s.code, label: s.name })) ??
    [];

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.firstName.trim()) e.firstName = "First name is required";
    if (!form.lastName.trim()) e.lastName = "Last name is required";
    if (form.email && !/\S+@\S+\.\S+/.test(form.email))
      e.email = "Invalid email format";
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async () => {
    if (!validate()) return;
    setSubmitting(true);
    try {
      const created = await contactsService.createContact({
        contactType: "LawFirm",
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        organization: form.organization.trim() || undefined,
        email: form.email.trim() || undefined,
        phone: form.phone.trim() || undefined,
        addressLine1: form.addressLine1.trim() || undefined,
        city: form.city.trim() || undefined,
        state: form.state || undefined,
        postalCode: form.postalCode.trim() || undefined,
        title: "",
        fax: undefined,
        website: "",
        notes: "",
      });
      addToast({
        type: "success",
        title: "Law Firm Created",
        description: form.organization.trim() || `${form.firstName} ${form.lastName}`,
      });
      setForm({ ...INITIAL_FORM });
      setErrors({});
      onCreated?.(created);
    } catch (err) {
      if (err instanceof ApiError) {
        addToast({
          type: "error",
          title: "Create Failed",
          description: err.message,
        });
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
    onClose();
  };

  return (
    <FormModal
      open={open}
      onClose={reset}
      onSubmit={handleSubmit}
      title="Add New Law Firm"
      submitLabel={submitting ? "Saving..." : "Save"}
      submitDisabled={submitting}
    >
      <div className="space-y-4">
        <div className="mb-2">
          <span className="inline-block w-[30px] text-center text-white mr-2 rounded bg-[#0a375a]">
            <i className="ri-bank-line text-light" />
          </span>
          <span className="font-semibold mb-2 mt-1">Law Firm</span>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <Field
            label="First Name"
            required
            value={form.firstName}
            onChange={(v) => setForm({ ...form, firstName: v.toString() })}
            error={errors.firstName}
            placeholder="First name"
          />
          <Field
            label="Last Name"
            required
            value={form.lastName}
            onChange={(v) => setForm({ ...form, lastName: v.toString() })}
            error={errors.lastName}
            placeholder="Last name"
          />
        </div>
        <Field
          label="Organization"
          value={form.organization}
          onChange={(v) => setForm({ ...form, organization: v.toString() })}
          error={errors.organization}
          placeholder="Organization"
        />
        <div className="grid grid-cols-2 gap-3">
          <Field
            label="Email Address"
            type="email"
            value={form.email}
            onChange={(v) => setForm({ ...form, email: v.toString() })}
            error={errors.email}
            placeholder=""
          />
          <Field
            label="Phone"
            value={form.phone}
            onChange={(v) => setForm({ ...form, phone: v.toString() })}
            error={errors.phone}
            placeholder=""
          />
        </div>
        <Field
          label="Address"
          value={form.addressLine1}
          onChange={(v) => setForm({ ...form, addressLine1: v.toString() })}
          error={errors.addressLine1}
          placeholder=""
        />
        <div className="grid grid-cols-3 gap-3">
          <Field
            label="City"
            value={form.city}
            onChange={(v) => setForm({ ...form, city: v.toString() })}
            error={errors.city}
            placeholder=""
          />
          <Field
            label="State"
            value={form.state}
            options={states}
            onChange={(v) => setForm({ ...form, state: v.toString() })}
            error={errors.state}
            placeholder=""
            type="select"
          />
          <Field
            label="Zipcode"
            value={form.postalCode}
            onChange={(v) => setForm({ ...form, postalCode: v.toString() })}
            error={errors.postalCode}
            placeholder=""
          />
        </div>
      </div>
    </FormModal>
  );
}
