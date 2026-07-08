"use client";

import { useEffect, useState } from "react";
import { FormModal } from "@/components/lien/modal";
import { useLienStore } from "@/stores/lien-store";
import { ApiError } from "@/lib/api-client";
import Field from "../field";
import { contactsService } from "@/lib/contacts";
import type { ContactDetail } from "@/lib/contacts";
import { lookupService } from "@/lib/lookup";
import { Combobox, type ComboboxOption } from "@/components/ui/combobox";

interface CreateCaseManagerFormProps {
  open: boolean;
  onClose: () => void;
  onCreated?: (created: ContactDetail) => void;
  defaultLawfirmId?: string;
}

const INITIAL_FORM = {
  firstName: "",
  lastName: "",
  email: "",
  phone: "",
  lawFirmId: "",
};

export function CreateCaseManagerForm({
  open,
  onClose,
  onCreated,
  defaultLawfirmId,
}: CreateCaseManagerFormProps) {
  const addToast = useLienStore((s) => s.addToast);
  const [form, setForm] = useState({
    ...INITIAL_FORM,
    lawFirmId: defaultLawfirmId ?? "",
  });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);
  const [lawFirms, setLawFirms] = useState<ComboboxOption[]>([]);

  useEffect(() => {
    if (!open) return;
    setForm({ ...INITIAL_FORM, lawFirmId: defaultLawfirmId ?? "" });
    setErrors({});
    lookupService
      .getLawfirm()
      .then((res) => {
        setLawFirms(
          res.items.map((c) => ({ value: c.id, label: c.displayName })),
        );
      })
      .catch(() => setLawFirms([]));
  }, [open, defaultLawfirmId]);

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.firstName.trim()) e.firstName = "First name is required";
    if (!form.lastName.trim()) e.lastName = "Last name is required";
    if (!form.lawFirmId) e.lawFirmId = "Law firm is required";
    if (form.email && !/\S+@\S+\.\S+/.test(form.email))
      e.email = "Invalid email format";
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async () => {
    if (!validate()) return;
    setSubmitting(true);
    try {
      const contactSubtype = await contactsService.getCaseManagerRoleCode();
      if (!contactSubtype) {
        addToast({
          type: "error",
          title: "Create Failed",
          description:
            "No \"Case Manager\" role is configured for law firm contacts.",
        });
        return;
      }
      const created = await contactsService.createContact({
        contactType: "LawFirm",
        contactSubtype,
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        email: form.email.trim() || undefined,
        phone: form.phone.trim() || undefined,
        lawFirmId: form.lawFirmId,
      });
      addToast({
        type: "success",
        title: "Case Manager Created",
        description: `${form.firstName} ${form.lastName}`,
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
      title="Add New Case Manager"
      submitLabel={submitting ? "Saving..." : "Save"}
      submitDisabled={submitting}
    >
      <div className="space-y-4">
        <div className="mb-2">
          <span className="inline-block w-[30px] text-center text-white mr-2 rounded bg-green-500">
            <i className="ri-briefcase-line text-light" />
          </span>
          <span className="font-semibold mb-2 mt-1">Case Manager</span>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <Field
            label="First Name"
            required
            value={form.firstName}
            onChange={(v) => setForm({ ...form, firstName: v.toString() })}
            error={errors.firstName}
            placeholder="First Name"
          />
          <Field
            label="Last Name"
            required
            value={form.lastName}
            onChange={(v) => setForm({ ...form, lastName: v.toString() })}
            error={errors.lastName}
            placeholder="Last Name"
          />
        </div>
        <div className="grid grid-cols-2 gap-3">
          <Field
            label="Telephone Number"
            value={form.phone}
            onChange={(v) => setForm({ ...form, phone: v.toString() })}
            error={errors.phone}
            placeholder="(000) 000-0000"
          />
          <Field
            label="Email Address"
            type="email"
            value={form.email}
            onChange={(v) => setForm({ ...form, email: v.toString() })}
            error={errors.email}
            placeholder="Email Address"
          />
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Law Firm<span className="text-red-500 ml-0.5">*</span>
          </label>
          <Combobox
            value={form.lawFirmId}
            onChange={(v) => setForm({ ...form, lawFirmId: v })}
            options={lawFirms}
            placeholder="Select a law firm"
            error={Boolean(errors.lawFirmId)}
          />
          {errors.lawFirmId && (
            <p className="text-xs text-red-500 mt-1">{errors.lawFirmId}</p>
          )}
        </div>
      </div>
    </FormModal>
  );
}
