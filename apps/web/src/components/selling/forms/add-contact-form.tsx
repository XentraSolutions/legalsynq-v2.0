"use client";

import { useState } from "react";
import { FormModal } from "@/components/selling/modal";
import { toast } from "sonner";
import { contactsService } from "@/lib/contacts";
import { facilityApi } from "@/lib/facility/facility.api";
import type { LookupData } from "@/lib/lookup/lookup.types";

interface AddContactFormProps {
  open: boolean;
  defaultContactType?: string;
  data: {
    id?: string;
    firstName?: string;
    lastName?: string;
    contactType?: string;
    organization?: string;
    email?: string;
    phone?: string;
    city?: string;
    state?: string;
    addressLine1?: string;
    postalCode?: string;
    contactTypes: LookupData[];
    states: LookupData[];
  };
  mode: "create" | "edit" | undefined;
  onClose: () => void;
  onCreated?: () => void;
}

const EMPTY_FORM = {
  name: "",
  firstName: "",
  lastName: "",
  contactType: "",
  organization: "",
  email: "",
  phone: "",
  city: "",
  state: "",
  addressLine1: "",
  postalCode: "",
};

// MedicalFacility routes through the legacy facility API,
// which uses a single "name" field rather than firstName/lastName.
const LEGACY_FACILITY_TYPES = ["MedicalFacility"];
const LEGACY_CONTACT_TYPE_OPTIONS = [
  { code: "LawFirm", name: "LAW FIRMS" },
  { code: "Provider", name: "MEDICAL PROVIDERS" },
  { code: "FundingCompany", name: "FUNDING COMPANIES" },
  { code: "MedicalFacility", name: "MEDICAL FACILITIES" },
  { code: "Lead", name: "LEADS" },
] as const;

const LEGACY_CONTACT_TYPE_CODE_ALIASES: Record<string, string> = {
  LienHolder: "FundingCompany",
  Facility: "MedicalFacility",
};

const FALLBACK_CONTACT_TYPE_LABELS: Record<string, string> = {
  CaseManager: "Case Manager",
  Facility: "Facility",
  FundingCompany: "FUNDING COMPANIES",
  InternalUser: "Internal User",
  LawFirm: "LAW FIRMS",
  Lead: "LEADS",
  LienHolder: "FUNDING COMPANIES",
  MedicalFacility: "MEDICAL FACILITIES",
  Provider: "MEDICAL PROVIDERS",
};

export function AddContactForm({
  open,
  data,
  defaultContactType,
  onClose,
  onCreated,
  mode = "create",
}: AddContactFormProps) {
  const [form, setForm] = useState(
    mode === "create"
      ? { ...EMPTY_FORM, contactType: defaultContactType ?? "" }
      : {
          ...EMPTY_FORM,
          firstName: data.firstName ?? "",
          lastName: data.lastName ?? "",
          contactType: data.contactType ?? "",
          organization: data.organization ?? "",
          email: data.email ?? "",
          phone: data.phone ?? "",
          city: data.city ?? "",
          state: data.state ?? "",
          addressLine1: data.addressLine1 ?? "",
          postalCode: data.postalCode ?? "",
        },
  );
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  const isLegacyFacilityCreate =
    mode === "create" && LEGACY_FACILITY_TYPES.includes(form.contactType);

  const normalizedContactTypes = LEGACY_CONTACT_TYPE_OPTIONS.flatMap(
    ({ code, name }) => {
      const match =
        data.contactTypes.find((option) => option.code === code) ??
        (code === "FundingCompany"
          ? data.contactTypes.find((option) => option.code === "LienHolder")
          : undefined) ??
        (code === "MedicalFacility"
          ? data.contactTypes.find((option) => option.code === "Facility")
          : undefined);

      if (!match) return [];

      return [{ ...match, code, name }];
    },
  );

  const selectedContactTypeValue =
    mode === "edit"
      ? LEGACY_CONTACT_TYPE_CODE_ALIASES[form.contactType] ?? form.contactType
      : form.contactType;

  const currentContactTypeOption =
    mode === "edit" &&
    form.contactType &&
    !normalizedContactTypes.some(
      (option) => option.code === selectedContactTypeValue,
    )
      ? data.contactTypes.find((option) => option.code === form.contactType) ?? {
          id: form.contactType,
          category: "ContactType",
          code: form.contactType,
          description: null,
          isActive: true,
          isSystem: true,
          name:
            FALLBACK_CONTACT_TYPE_LABELS[form.contactType] ?? form.contactType,
          sortOrder: Number.MAX_SAFE_INTEGER,
        }
      : null;

  const contactTypeOptions = currentContactTypeOption
    ? [...normalizedContactTypes, currentContactTypeOption]
    : normalizedContactTypes;

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.contactType) e.contactType = "Type is required";

    if (isLegacyFacilityCreate) {
      if (!form.name.trim()) e.name = "Name is required";
    } else {
      if (!form.firstName.trim()) e.firstName = "First name is required";
      if (!form.lastName.trim()) e.lastName = "Last name is required";
    }

    if (form.email && !/\S+@\S+\.\S+/.test(form.email))
      e.email = "Invalid email format";
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async () => {
    if (!validate()) return;
    try {
      setSubmitting(true);

      if (isLegacyFacilityCreate) {
        await facilityApi.createFacility({
          name: form.name.trim(),
          email: form.email || "",
          phone: form.phone || undefined,
          addressLine1: form.addressLine1 || "",
          city: form.city || "",
          state: form.state || "",
          postalCode: form.postalCode || "",
        });
        toast.success("Medical Facility Created", { description: form.name });
      } else if (mode === "create") {
        await contactsService.createContact({
          firstName: form.firstName,
          lastName: form.lastName,
          contactType: form.contactType,
          organization: form.organization || undefined,
          email: form.email || undefined,
          phone: form.phone || undefined,
          city: form.city || undefined,
          state: form.state || undefined,
          addressLine1: form.addressLine1 || undefined,
          postalCode: form.postalCode || undefined,
          title: "",
          fax: undefined,
          website: "",
          notes: "",
        });
        toast.success("Contact Created", { description: `${form.firstName} ${form.lastName}` });
      } else {
        await contactsService.updateContact(data?.id ?? "", {
          firstName: form.firstName,
          lastName: form.lastName,
          contactType: form.contactType,
          organization: form.organization || undefined,
          email: form.email || undefined,
          phone: form.phone || undefined,
          city: form.city || undefined,
          state: form.state || undefined,
          addressLine1: form.addressLine1 || undefined,
          postalCode: form.postalCode || undefined,
        });
        toast.success("Contact Updated", { description: `${form.firstName} ${form.lastName}` });
      }

      resetAndClose();
      onCreated?.();
    } catch (err) {
      toast.error(mode === "create" ? "Create Failed" : "Update Failed", {
        description:
          err instanceof Error
            ? err.message
            : `Failed to ${mode === "create" ? "create" : "update"} contact`,
      });
    } finally {
      setSubmitting(false);
    }
  };

  const resetAndClose = () => {
    setForm({ ...EMPTY_FORM });
    setErrors({});
    onClose();
  };

  const inputCls = (field: string) =>
    `w-full border rounded-lg px-3 py-2 text-sm ${errors[field] ? "border-red-300" : "border-gray-200"} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`;

  return (
    <FormModal
      open={open}
      onClose={resetAndClose}
      onSubmit={handleSubmit}
      title={mode === "create" ? "Add Contact" : "Edit Contact"}
      submitLabel={
        submitting
          ? mode === "create"
            ? "Creating..."
            : "Updating..."
          : mode === "create"
            ? "Create Contact"
            : "Save Changes"
      }
    >
      <div className="space-y-4">
        {/* Contact Type */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Contact Type<span className="text-red-500 ml-0.5">*</span>
          </label>
          <select
            value={selectedContactTypeValue}
            onChange={(e) => setForm({ ...form, contactType: e.target.value })}
            disabled={mode === "edit"}
            className={inputCls("contactType")}
          >
            <option value="">Select...</option>
            {contactTypeOptions.length > 0 &&
              contactTypeOptions.map((v) => (
                <option key={v.id} value={v.code}>
                  {v.name}
                </option>
              ))}
          </select>
          {errors.contactType && (
            <p className="text-xs text-red-500 mt-1">{errors.contactType}</p>
          )}
        </div>

        {isLegacyFacilityCreate ? (
          /* ── Legacy facility form (MedicalFacility + LawFirm route through facilityApi) ── */
          <>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Name<span className="text-red-500 ml-0.5">*</span>
              </label>
              <input
                type="text"
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
                placeholder="Facility name"
                className={inputCls("name")}
              />
              {errors.name && (
                <p className="text-xs text-red-500 mt-1">{errors.name}</p>
              )}
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
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
                onChange={(e) =>
                  setForm({ ...form, addressLine1: e.target.value })
                }
                placeholder="Address"
                className={inputCls("addressLine1")}
              />
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
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
                  {data?.states?.length > 0 &&
                    data.states.map((v) => (
                      <option key={v.id} value={v.code}>
                        {v.code}
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
                  onChange={(e) =>
                    setForm({ ...form, postalCode: e.target.value })
                  }
                  placeholder="Zip Code"
                  className={inputCls("postalCode")}
                />
              </div>
            </div>
          </>
        ) : (
          /* ── Standard contact form ── */
          <>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  First Name<span className="text-red-500 ml-0.5">*</span>
                </label>
                <input
                  type="text"
                  value={form.firstName}
                  onChange={(e) =>
                    setForm({ ...form, firstName: e.target.value })
                  }
                  placeholder="First name"
                  className={inputCls("firstName")}
                />
                {errors.firstName && (
                  <p className="text-xs text-red-500 mt-1">
                    {errors.firstName}
                  </p>
                )}
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Last Name<span className="text-red-500 ml-0.5">*</span>
                </label>
                <input
                  type="text"
                  value={form.lastName}
                  onChange={(e) =>
                    setForm({ ...form, lastName: e.target.value })
                  }
                  placeholder="Last name"
                  className={inputCls("lastName")}
                />
                {errors.lastName && (
                  <p className="text-xs text-red-500 mt-1">{errors.lastName}</p>
                )}
              </div>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
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
                onChange={(e) =>
                  setForm({ ...form, addressLine1: e.target.value })
                }
                placeholder="Address"
                className={inputCls("addressLine1")}
              />
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
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
                  {data?.states?.length > 0 &&
                    data.states.map((v) => (
                      <option key={v.id} value={v.code}>
                        {v.code}
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
                  onChange={(e) =>
                    setForm({ ...form, postalCode: e.target.value })
                  }
                  placeholder="Zip Code"
                  className={inputCls("postalCode")}
                />
              </div>
            </div>
          </>
        )}
      </div>
    </FormModal>
  );
}
