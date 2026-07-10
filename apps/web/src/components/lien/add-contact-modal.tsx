"use client";

import { useEffect, useState } from "react";
import { FormModal } from "@/components/lien/modal";
import { useLienStore } from "@/stores/lien-store";
import { useSessionContext } from "@/providers/session-provider";
import { contactsService } from "@/lib/contacts";
import type { ContactDetail } from "@/lib/contacts";
import type { LookupData } from "@/lib/lookup/lookup.types";
import { ApiError } from "@/lib/api-client";

/** Minimal shape needed to prefill the edit form — satisfied by both
 * ContactResponseDto (contact detail sections) and ContactListItem
 * (contacts list page), which lacks addressLine1/postalCode/contactSubtype. */
interface EditableContact {
  id: string;
  firstName: string;
  lastName: string;
  contactType: string;
  email?: string | null;
  phone?: string | null;
  addressLine1?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  contactSubtype?: string | null;
}

interface AddContactModalProps {
  open: boolean;
  onClose: () => void;
  onSaved: (contact: ContactDetail) => void;
  /** Fixed contact type; omit when `contactTypeOptions` is used instead. */
  contactType?: string;
  /** When set, renders a Contact Type select (disabled while editing) instead of a fixed contactType. */
  contactTypeOptions?: LookupData[];
  lawFirmId?: string;
  facilityId?: string;
  /** Fixed sub-contact role. Ignored when `roleOptions` is passed. */
  contactSubtype?: string;
  /** Renders a Role select bound to contactSubtype. */
  roleOptions?: LookupData[];
  title: string;
  subtitle?: string;
  /** Presence => edit mode: prefills the form and calls updateContact. */
  editTarget?: EditableContact | null;
}

interface ContactTypeIconConfig {
  icon: string;
  iconBg: string;
  labelText: string;
  label: string;
}

const DEFAULT_ICON: ContactTypeIconConfig = {
  icon: "ri-contacts-book-line",
  iconBg: "bg-indigo-500",
  labelText: "text-indigo-700",
  label: "Contact Information",
};

/** Sub-role icons take priority over the parent contactType's icon. */
const SUBTYPE_ICONS: Record<string, ContactTypeIconConfig> = {
  facilitycontactperson: {
    icon: "ri-nurse-line",
    iconBg: "bg-blue-500",
    labelText: "text-blue-700",
    label: "Contact Person",
  },
  casemanager: {
    icon: "ri-briefcase-line",
    iconBg: "bg-green-500",
    labelText: "text-green-700",
    label: "Case Manager",
  },
};

const CONTACT_TYPE_ICONS: Record<string, ContactTypeIconConfig> = {
  MedicalFacility: {
    icon: "ri-stethoscope-line",
    iconBg: "bg-orange-500",
    labelText: "text-orange-700",
    label: "Medical Facility",
  },
  LawFirm: {
    icon: "ri-scales-line",
    iconBg: "bg-purple-500",
    labelText: "text-purple-700",
    label: "Law Firm",
  },
};

function getContactTypeIcon(contactType: string, contactSubtype: string): ContactTypeIconConfig {
  return (
    SUBTYPE_ICONS[contactSubtype.toLowerCase()] ??
    CONTACT_TYPE_ICONS[contactType] ??
    DEFAULT_ICON
  );
}

const EMPTY_FORM = {
  firstName: "",
  lastName: "",
  contactType: "",
  contactSubtype: "",
  email: "",
  phone: "",
  addressLine1: "",
  city: "",
  state: "",
  postalCode: "",
};

export function AddContactModal({
  open,
  onClose,
  onSaved,
  contactType,
  contactTypeOptions,
  lawFirmId,
  facilityId,
  contactSubtype,
  roleOptions,
  title,
  subtitle,
  editTarget,
}: AddContactModalProps) {
  const { lookup } = useSessionContext();
  const addToast = useLienStore((s) => s.addToast);
  const isEdit = Boolean(editTarget);

  const [form, setForm] = useState({
    ...EMPTY_FORM,
    contactType: contactType ?? "",
    contactSubtype: contactSubtype ?? "",
  });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  const states =
    lookup?.State?.map((s) => ({ key: s.id, value: s.code, label: s.code })) ?? [];

  useEffect(() => {
    if (!open) return;
    if (editTarget) {
      setForm({
        firstName: editTarget.firstName ?? "",
        lastName: editTarget.lastName ?? "",
        contactType: editTarget.contactType ?? contactType ?? "",
        contactSubtype: editTarget.contactSubtype ?? contactSubtype ?? "",
        email: editTarget.email ?? "",
        phone: editTarget.phone ?? "",
        addressLine1: editTarget.addressLine1 ?? "",
        city: editTarget.city ?? "",
        state: editTarget.state ?? "",
        postalCode: editTarget.postalCode ?? "",
      });
    } else {
      setForm({
        ...EMPTY_FORM,
        contactType: contactType ?? "",
        contactSubtype: contactSubtype ?? "",
      });
    }
    setErrors({});
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, editTarget]);

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.contactType) e.contactType = "Type is required";
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
      const payload = {
        contactType: form.contactType,
        contactSubtype: form.contactSubtype || undefined,
        lawFirmId: lawFirmId || undefined,
        facilityId: facilityId || undefined,
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        email: form.email.trim() || undefined,
        phone: form.phone.trim() || undefined,
        addressLine1: form.addressLine1.trim() || undefined,
        city: form.city.trim() || undefined,
        state: form.state || undefined,
        postalCode: form.postalCode.trim() || undefined,
      };

      const saved = isEdit
        ? await contactsService.updateContact(editTarget!.id, payload)
        : await contactsService.createContact(payload);

      addToast({
        type: "success",
        title: isEdit ? "Contact Updated" : "Contact Created",
        description: `${form.firstName} ${form.lastName}`,
      });
      onSaved(saved);
    } catch (err) {
      addToast({
        type: "error",
        title: isEdit ? "Update Failed" : "Create Failed",
        description:
          err instanceof ApiError ? err.message : "An unexpected error occurred",
      });
    } finally {
      setSubmitting(false);
    }
  };

  const inputCls = (field: string) =>
    `w-full border rounded-lg px-3 py-2 text-sm ${errors[field] ? "border-red-300" : "border-gray-200"} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`;

  const typeIcon = getContactTypeIcon(form.contactType, form.contactSubtype);

  return (
    <FormModal
      open={open}
      onClose={onClose}
      onSubmit={handleSubmit}
      title={title}
      subtitle={subtitle}
      submitLabel={submitting ? (isEdit ? "Saving..." : "Creating...") : "Save"}
      submitDisabled={submitting}
    >
      <div className="flex items-center gap-2.5 mb-4">
        <div className={`w-9 h-9 rounded-lg ${typeIcon.iconBg} text-white flex items-center justify-center shrink-0`}>
          <i className={`${typeIcon.icon} text-lg`} />
        </div>
        <span className={`text-sm font-semibold ${typeIcon.labelText}`}>{typeIcon.label}</span>
      </div>

      <div className="space-y-4">
        {contactTypeOptions && (
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Contact Type<span className="text-red-500 ml-0.5">*</span>
            </label>
            <select
              value={form.contactType}
              onChange={(e) => setForm({ ...form, contactType: e.target.value })}
              disabled={isEdit}
              className={inputCls("contactType")}
            >
              <option value="">Select...</option>
              {contactTypeOptions.map((t) => (
                <option key={t.id} value={t.code}>
                  {t.name}
                </option>
              ))}
            </select>
            {errors.contactType && (
              <p className="text-xs text-red-500 mt-1">{errors.contactType}</p>
            )}
          </div>
        )}

        {roleOptions && roleOptions.length > 0 && (
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Role
            </label>
            <select
              value={form.contactSubtype}
              onChange={(e) => setForm({ ...form, contactSubtype: e.target.value })}
              className={inputCls("contactSubtype")}
            >
              <option value="">— Select Role —</option>
              {roleOptions.map((r) => (
                <option key={r.id} value={r.code}>
                  {r.name}
                </option>
              ))}
            </select>
          </div>
        )}

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              First Name<span className="text-red-500 ml-0.5">*</span>
            </label>
            <input
              type="text"
              value={form.firstName}
              onChange={(e) => setForm({ ...form, firstName: e.target.value })}
              placeholder="First name"
              className={inputCls("firstName")}
            />
            {errors.firstName && (
              <p className="text-xs text-red-500 mt-1">{errors.firstName}</p>
            )}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Last Name<span className="text-red-500 ml-0.5">*</span>
            </label>
            <input
              type="text"
              value={form.lastName}
              onChange={(e) => setForm({ ...form, lastName: e.target.value })}
              placeholder="Last name"
              className={inputCls("lastName")}
            />
            {errors.lastName && (
              <p className="text-xs text-red-500 mt-1">{errors.lastName}</p>
            )}
          </div>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Email
            </label>
            <input
              type="email"
              value={form.email}
              onChange={(e) => setForm({ ...form, email: e.target.value })}
              placeholder="email@example.com"
              className={inputCls("email")}
            />
            {errors.email && (
              <p className="text-xs text-red-500 mt-1">{errors.email}</p>
            )}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Phone
            </label>
            <input
              type="text"
              value={form.phone}
              onChange={(e) => setForm({ ...form, phone: e.target.value })}
              placeholder="(555) 555-0000"
              className={inputCls("phone")}
            />
          </div>
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Address
          </label>
          <input
            type="text"
            value={form.addressLine1}
            onChange={(e) => setForm({ ...form, addressLine1: e.target.value })}
            placeholder="Address"
            className={inputCls("addressLine1")}
          />
        </div>

        <div className="grid grid-cols-3 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              City
            </label>
            <input
              type="text"
              value={form.city}
              onChange={(e) => setForm({ ...form, city: e.target.value })}
              placeholder="City"
              className={inputCls("city")}
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              State
            </label>
            <select
              value={form.state}
              onChange={(e) => setForm({ ...form, state: e.target.value })}
              className={inputCls("state")}
            >
              <option value="">Select...</option>
              {states.map((s) => (
                <option key={s.key} value={s.value}>
                  {s.label}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Zip Code
            </label>
            <input
              type="text"
              value={form.postalCode}
              onChange={(e) => setForm({ ...form, postalCode: e.target.value })}
              placeholder="Zip Code"
              className={inputCls("postalCode")}
            />
          </div>
        </div>
      </div>
    </FormModal>
  );
}
