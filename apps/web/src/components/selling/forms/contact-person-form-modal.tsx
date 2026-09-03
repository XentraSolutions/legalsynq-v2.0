"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";
import { FormModal } from "@/components/selling/modal";
import { BaseSelect } from "@/components/ui/base-select";
import { SellingEntitySelect } from "@/components/selling/selling-entity-select";
import { formatPhoneInput, isValidPhone } from "@/lib/phone";
import { TriangleAlert } from "lucide-react";
import {
  useCompany,
  useCompanyTypes,
  useContactPersons,
  useContactPersonTypes,
  useCreateContactPerson,
  useUpdateContactPerson,
} from "@/hooks/selling/use-selling-companies";
import { AddContactPersonRoleModal } from "@/components/selling/forms/add-contact-person-role-modal";
import { Button } from "@/components/selling/button";
import type { ContactPerson } from "@/lib/selling/companies.types";

const FUNDING_COMPANY_TYPE_CODE = "FundingCompany";

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
  // Set locally when the user clicks "Edit Contact Person" from the
  // funding-company blocked state below — lets this same modal pivot into
  // editing the existing contact without the parent knowing anything changed.
  const [switchToEdit, setSwitchToEdit] = useState<ContactPerson | null>(null);
  const effectiveEditTarget = switchToEdit ?? editTarget ?? null;
  const isEdit = Boolean(effectiveEditTarget);
  const [form, setForm] = useState<ContactForm>(
    editTarget ? formFromContact(editTarget, companyId) : emptyForm(companyId),
  );
  const [errors, setErrors] = useState<Record<string, string>>({});
  const createContactPerson = useCreateContactPerson();
  const updateContactPerson = useUpdateContactPerson();
  const submitting = createContactPerson.isPending || updateContactPerson.isPending;

  // Looked up by id rather than found in a fetched list — the picker (see
  // the Company Name field below) can select a company from well beyond
  // whatever page a list query would hold, so a list-based lookup would go
  // stale/miss as soon as the user searches for something else.
  const selectedCompanyQuery = useCompany(form.companyId, {
    enabled: open && Boolean(allowCompanySelect) && Boolean(form.companyId),
  });
  const effectiveCompanyTypeId = allowCompanySelect
    ? (selectedCompanyQuery.data?.companyTypeId ?? companyTypeId)
    : companyTypeId;
  const selectedCompanyLabel = allowCompanySelect
    ? (selectedCompanyQuery.data?.name ?? companyName)
    : companyName;

  const contactPersonTypesQuery = useContactPersonTypes(effectiveCompanyTypeId, { enabled: open });
  const roleOptions = contactPersonTypesQuery.options;
  const lockedType = lockContactType
    ? contactPersonTypesQuery.data?.find((t) => t.code === lockContactType)
    : undefined;
  const [showAddRole, setShowAddRole] = useState(false);
  const nextRoleSortOrder =
    Math.max(0, ...(contactPersonTypesQuery.data ?? []).map((t) => t.sortOrder)) + 1;

  // A funding company may only ever have one contact person. When the
  // selected company already has one and we're not already editing it,
  // block the create form and offer editing the existing one instead.
  const companyTypesQuery = useCompanyTypes({ enabled: open });
  const isFundingCompany =
    companyTypesQuery.data?.find((t) => t.id === effectiveCompanyTypeId)?.code ===
    FUNDING_COMPANY_TYPE_CODE;
  const targetCompanyId = allowCompanySelect ? form.companyId : companyId;
  const isCreatingNew = !editTarget && !switchToEdit;
  const existingContactsQuery = useContactPersons(targetCompanyId || null, true, {
    enabled: open && isCreatingNew && isFundingCompany && Boolean(targetCompanyId),
  });
  const existingContact =
    isCreatingNew && isFundingCompany && !existingContactsQuery.isLoading
      ? (existingContactsQuery.data?.[0] ?? null)
      : null;
  // Still figuring out whether the selected company is a funding company
  // with an existing contact — block Create until that's settled so a fast
  // click can't slip a second contact past the check above.
  const checkingExistingContact =
    isCreatingNew &&
    Boolean(targetCompanyId) &&
    ((allowCompanySelect && selectedCompanyQuery.isLoading) ||
      companyTypesQuery.isLoading ||
      (isFundingCompany && existingContactsQuery.isLoading));

  // Snapshot of the most recently blocked company/contact — kept around
  // (and shown below the form) even after the company field is cleared
  // back out, since `existingContact` itself goes stale the moment that
  // happens.
  const [blockedNotice, setBlockedNotice] = useState<{
    companyId: string;
    companyName: string;
    contact: ContactPerson;
  } | null>(null);

  useEffect(() => {
    if (!existingContact) return;
    setBlockedNotice({
      companyId: targetCompanyId,
      companyName: selectedCompanyLabel,
      contact: existingContact,
    });
    // The company can only be reselected when there's a picker; when it's
    // fixed (allowCompanySelect off) there's nothing to clear back out.
    if (allowCompanySelect) {
      setForm((f) => ({ ...f, companyId: "", contactPersonTypeId: "" }));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [existingContact]);

  useEffect(() => {
    if (!open) return;
    setForm(editTarget ? formFromContact(editTarget, companyId) : emptyForm(companyId));
    setErrors({});
    setSwitchToEdit(null);
    setBlockedNotice(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, editTarget, companyId]);

  const handleEditExisting = () => {
    if (!blockedNotice) return;
    setSwitchToEdit(blockedNotice.contact);
    setForm(formFromContact(blockedNotice.contact, blockedNotice.companyId));
    setErrors({});
    setBlockedNotice(null);
  };

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

  // Keeps Create/Save disabled until the required fields are actually
  // filled, rather than only catching it on submit.
  const hasRequiredFields = Boolean(
    form.companyId && form.contactPersonTypeId && form.firstName.trim() && form.lastName.trim(),
  );

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
      addressLine1: effectiveEditTarget?.addressLine1 ?? undefined,
      city: effectiveEditTarget?.city ?? undefined,
      state: effectiveEditTarget?.state ?? undefined,
      postalCode: effectiveEditTarget?.postalCode ?? undefined,
      phone: form.phone.trim() || undefined,
      email: form.email.trim() || undefined,
    };
    try {
      const contact = isEdit
        ? await updateContactPerson.mutateAsync({
            companyId: form.companyId,
            contactId: effectiveEditTarget!.id,
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
      title={switchToEdit ? "Edit Contact Person" : title}
      subtitle={
        <>
          {switchToEdit ? "Update the contact information for" : "Provide the required information to add a contact to"}{" "}
          <span className="font-medium text-gray-700">{selectedCompanyLabel}</span>.
        </>
      }
      submitLabel={
        submitting
          ? "Saving..."
          : checkingExistingContact
            ? "Checking..."
            : isEdit
              ? "Save Changes"
              : "Create"
      }
      submitDisabled={Boolean(blockedNotice) || checkingExistingContact || !hasRequiredFields}
      loading={submitting}
    >
      <div className="space-y-4">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Company Name<span className="text-red-500 ml-0.5">*</span>
            </label>
            {allowCompanySelect ? (
              <SellingEntitySelect
                value={form.companyId}
                onChange={(v) => {
                  setField("companyId", v);
                  setBlockedNotice(null);
                }}
                placeholder="Select company"
                searchPlaceholder="Search companies..."
                error={Boolean(errors.companyId)}
                disabled={isEdit}
              />
            ) : (
              <BaseSelect
                value={companyId}
                onChange={() => {}}
                options={[{ value: companyId, label: companyName }]}
                placeholder="Select company"
                disabled
              />
            )}
            {errors.companyId && <p className="text-xs text-red-500 mt-1">{errors.companyId}</p>}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Role<span className="text-red-500 ml-0.5">*</span>
            </label>
            <BaseSelect
              value={form.contactPersonTypeId}
              onChange={(v) => setField("contactPersonTypeId", v)}
              options={roleOptions}
              isLoading={contactPersonTypesQuery.isLoading}
              error={Boolean(errors.contactPersonTypeId)}
              placeholder="Select role"
              searchPlaceholder="Search roles..."
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
              placeholder="e.g. user@example.com"
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

      {blockedNotice && (
        <div className="rounded-lg border border-amber-200 bg-amber-50 p-4 mt-4">
          <div className="flex items-start gap-3">
            <TriangleAlert className="h-5 w-5 shrink-0 text-amber-500 mt-0.5" />
            <div>
              <p className="text-sm font-semibold text-amber-800">
                {blockedNotice.companyName} currently has a contact person
              </p>
              <p className="text-sm text-amber-700 mt-1">
                A funding company can only have one contact person on file —{" "}
                {blockedNotice.contact.displayName} is currently theirs.{" "}
                {allowCompanySelect
                  ? "Edit the existing contact, or select a different company above."
                  : "Edit the existing contact instead."}
              </p>
            </div>
          </div>
          <div className="mt-3">
            <Button type="button" variant="primary" onClick={handleEditExisting}>
              Edit Contact Person
            </Button>
          </div>
        </div>
      )}

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
