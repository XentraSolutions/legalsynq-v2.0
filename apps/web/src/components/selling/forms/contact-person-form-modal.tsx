"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";
import { FormModal } from "@/components/selling/modal";
import { Combobox } from "@/components/ui/combobox";
import { formatPhoneInput, isValidPhone } from "@/lib/phone";
import {
  useCompanies,
  useContactPersonTypes,
  useCreateContactPerson,
  useUpdateContactPerson,
} from "@/hooks/use-selling-companies";
import { AddContactPersonRoleModal } from "@/components/selling/forms/add-contact-person-role-modal";
import type { ContactPerson } from "@/lib/selling/companies.types";

interface ContactPersonFormModalProps {
  open: boolean;
  title: string;
  companyId: string;
  /** Display label for the parent company — shown in the subtitle and, when `allowCompanySelect` is off, as the (disabled) Company Name field. */
  companyName: string;
  /** The parent company's type — used to load the valid Role options (contact-person-types). Ignored once the user picks a different company via `allowCompanySelect`. */
  companyTypeId: string;
  /** Present in edit mode; the contact person being edited. */
  editTarget?: ContactPerson | null;
  onClose: () => void;
  onSaved: (contact: ContactPerson) => void;
  /** Renders an editable "Company Name" field instead of assuming a fixed parent company — e.g. the post-create "Add Contact Person" prompt, where the user may want to attach the contact to a different company. */
  allowCompanySelect?: boolean;
  /** When set, the Role field is locked to this contact-person-type code — e.g. creating a case manager from the lien form's entity select shouldn't let the user pick a different role. */
  lockContactType?: "CaseManager" | "Attorney";
}

interface ContactForm {
  companyId: string;
  contactPersonTypeId: string;
  firstName: string;
  lastName: string;
  phone: string;
  email: string;
}

type ValidatableField = "companyId" | "contactPersonTypeId" | "firstName" | "lastName" | "email" | "phone";
const FIELDS: readonly ValidatableField[] = [
  "companyId",
  "contactPersonTypeId",
  "firstName",
  "lastName",
  "email",
  "phone",
];

function formFromContact(contact: ContactPerson, companyId: string): ContactForm {
  return {
    companyId,
    contactPersonTypeId: contact.contactPersonTypeId,
    firstName: contact.firstName,
    lastName: contact.lastName,
    phone: contact.phone ?? "",
    email: contact.email ?? "",
  };
}

function emptyForm(companyId: string): ContactForm {
  return { companyId, contactPersonTypeId: "", firstName: "", lastName: "", phone: "", email: "" };
}

export function ContactPersonFormModal({
  open,
  title,
  companyId,
  companyName,
  companyTypeId,
  editTarget,
  onClose,
  onSaved,
  allowCompanySelect,
  lockContactType,
}: ContactPersonFormModalProps) {
  const isEdit = Boolean(editTarget);
  const [form, setForm] = useState<ContactForm>(
    editTarget ? formFromContact(editTarget, companyId) : emptyForm(companyId),
  );
  const [errors, setErrors] = useState<Record<string, string>>({});
  const createContactPerson = useCreateContactPerson();
  const updateContactPerson = useUpdateContactPerson();
  const submitting = createContactPerson.isPending || updateContactPerson.isPending;

  const companiesQuery = useCompanies(
    { isActive: true },
    { enabled: open && Boolean(allowCompanySelect) },
  );
  const companyOptions = allowCompanySelect
    ? companiesQuery.options
    : [{ value: companyId, label: companyName }];
  const selectedCompany = companiesQuery.data?.items.find((c) => c.id === form.companyId);
  const effectiveCompanyTypeId = allowCompanySelect
    ? (selectedCompany?.companyTypeId ?? companyTypeId)
    : companyTypeId;
  const selectedCompanyLabel = allowCompanySelect
    ? (selectedCompany?.name ?? companyName)
    : companyName;

  const contactPersonTypesQuery = useContactPersonTypes(effectiveCompanyTypeId, { enabled: open });
  const roleOptions = contactPersonTypesQuery.options;
  const lockedType = lockContactType
    ? contactPersonTypesQuery.data?.find((t) => t.code === lockContactType)
    : undefined;
  const [showAddRole, setShowAddRole] = useState(false);
  const nextRoleSortOrder =
    Math.max(0, ...(contactPersonTypesQuery.data ?? []).map((t) => t.sortOrder)) + 1;

  useEffect(() => {
    if (!open) return;
    setForm(editTarget ? formFromContact(editTarget, companyId) : emptyForm(companyId));
    setErrors({});
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, editTarget, companyId]);

  useEffect(() => {
    if (!open || isEdit || !lockedType) return;
    setForm((f) => (f.contactPersonTypeId === lockedType.id ? f : { ...f, contactPersonTypeId: lockedType.id }));
  }, [open, isEdit, lockedType]);

  const inputCls = (field: string) =>
    `w-full border rounded-lg px-3 py-2 text-sm ${errors[field] ? "border-red-300" : "border-gray-200"} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`;

  const validateField = (field: ValidatableField, f: ContactForm): string | undefined => {
    switch (field) {
      case "companyId":
        return f.companyId ? undefined : "Company is required";
      case "contactPersonTypeId":
        return f.contactPersonTypeId ? undefined : "Role is required";
      case "firstName":
        return f.firstName.trim() ? undefined : "First name is required";
      case "lastName":
        return f.lastName.trim() ? undefined : "Last name is required";
      case "email":
        return f.email && !/^\S+@\S+\.\S+$/.test(f.email)
          ? "Invalid email format"
          : undefined;
      case "phone":
        return f.phone && !isValidPhone(f.phone)
          ? "Phone number must be 10 digits"
          : undefined;
    }
  };

  const validate = () => {
    const e: Record<string, string> = {};
    for (const field of FIELDS) {
      const msg = validateField(field, form);
      if (msg) e[field] = msg;
    }
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const setField = <K extends keyof ContactForm>(field: K, value: string) => {
    const next = { ...form, [field]: value };
    // Role options depend on the selected company's type, so a company change
    // invalidates whatever role was previously chosen.
    if (field === "companyId") next.contactPersonTypeId = "";
    setForm(next);
    setErrors((e) => {
      const isValidatedField = (FIELDS as readonly string[]).includes(field);
      const msg = isValidatedField
        ? validateField(field as ValidatableField, next)
        : undefined;
      if (!msg) {
        if (!e[field]) return e;
        const { [field]: _removed, ...rest } = e;
        return rest;
      }
      return { ...e, [field]: msg };
    });
  };

  const handleSubmit = async () => {
    if (!validate()) return;
    const request = {
      contactPersonTypeId: form.contactPersonTypeId,
      firstName: form.firstName.trim(),
      lastName: form.lastName.trim(),
      addressLine1: editTarget?.addressLine1 ?? undefined,
      city: editTarget?.city ?? undefined,
      state: editTarget?.state ?? undefined,
      postalCode: editTarget?.postalCode ?? undefined,
      phone: form.phone.trim() || undefined,
      email: form.email.trim() || undefined,
    };
    try {
      const contact = isEdit
        ? await updateContactPerson.mutateAsync({
            companyId,
            contactId: editTarget!.id,
            request,
          })
        : await createContactPerson.mutateAsync({ companyId: form.companyId, request });
      toast.success(isEdit ? "Contact person updated" : "Contact person created", {
        description: contact.displayName,
      });
      onSaved(contact);
    } catch (err) {
      toast.error(isEdit ? "Couldn't update contact person" : "Couldn't create contact person", {
        description: err instanceof Error ? err.message : undefined,
      });
    }
  };

  return (
    <FormModal
      open={open}
      onClose={onClose}
      onSubmit={handleSubmit}
      title={title}
      subtitle={
        <>
          Provide the required information to add a contact to{" "}
          <span className="font-medium text-gray-700">{selectedCompanyLabel}</span>.
        </>
      }
      submitLabel={submitting ? "Saving..." : isEdit ? "Save Changes" : "Create"}
      loading={submitting}
    >
      <div className="space-y-4">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Company Name<span className="text-red-500 ml-0.5">*</span>
            </label>
            <Combobox
              value={form.companyId}
              onChange={(v) => setField("companyId", v)}
              options={companyOptions}
              placeholder="Select company"
              disabled={!allowCompanySelect || isEdit}
            />
            {errors.companyId && <p className="text-xs text-red-500 mt-1">{errors.companyId}</p>}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Role<span className="text-red-500 ml-0.5">*</span>
            </label>
            <Combobox
              value={form.contactPersonTypeId}
              onChange={(v) => setField("contactPersonTypeId", v)}
              options={roleOptions}
              error={Boolean(errors.contactPersonTypeId)}
              placeholder="Select role"
              disabled={Boolean(lockedType)}
              createAction={
                lockedType || !effectiveCompanyTypeId
                  ? undefined
                  : { label: "Add New Role", onSelect: () => setShowAddRole(true) }
              }
            />
            {errors.contactPersonTypeId && (
              <p className="text-xs text-red-500 mt-1">{errors.contactPersonTypeId}</p>
            )}
          </div>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              First Name<span className="text-red-500 ml-0.5">*</span>
            </label>
            <input
              type="text"
              value={form.firstName}
              onChange={(e) => setField("firstName", e.target.value)}
              placeholder="Enter first name"
              className={inputCls("firstName")}
            />
            {errors.firstName && <p className="text-xs text-red-500 mt-1">{errors.firstName}</p>}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Last Name<span className="text-red-500 ml-0.5">*</span>
            </label>
            <input
              type="text"
              value={form.lastName}
              onChange={(e) => setField("lastName", e.target.value)}
              placeholder="Enter last name"
              className={inputCls("lastName")}
            />
            {errors.lastName && <p className="text-xs text-red-500 mt-1">{errors.lastName}</p>}
          </div>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Email Address</label>
            <input
              type="email"
              value={form.email}
              onChange={(e) => setField("email", e.target.value)}
              placeholder="e.g. example@gmail.com"
              className={inputCls("email")}
            />
            {errors.email && <p className="text-xs text-red-500 mt-1">{errors.email}</p>}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Phone Number</label>
            <input
              type="text"
              value={form.phone}
              onChange={(e) => setField("phone", formatPhoneInput(e.target.value))}
              placeholder="(000) 000-0000"
              className={inputCls("phone")}
            />
            {errors.phone && <p className="text-xs text-red-500 mt-1">{errors.phone}</p>}
          </div>
        </div>
      </div>

      {showAddRole && effectiveCompanyTypeId && (
        <AddContactPersonRoleModal
          open={showAddRole}
          companyTypeId={effectiveCompanyTypeId}
          nextSortOrder={nextRoleSortOrder}
          onClose={() => setShowAddRole(false)}
          onCreated={(role) => {
            setField("contactPersonTypeId", role.id);
            setShowAddRole(false);
          }}
        />
      )}
    </FormModal>
  );
}
