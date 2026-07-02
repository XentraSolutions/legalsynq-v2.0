"use client";

import { useState, useEffect, useCallback } from "react";
import { useLienStore } from "@/stores/lien-store";
import { contactsApi } from "@/lib/contacts/contacts.api";
import { lookupApi } from "@/lib/lookup/lookup.api";
import { type ContactResponseDto } from "@/lib/contacts/contacts.types";
import { type LookupData } from "@/lib/lookup/lookup.types";
import { ApiError } from "@/lib/api-client";
import { FormModal, ConfirmDialog } from "@/components/lien/modal";
import { ActionMenu } from "@/components/lien/action-menu";

interface Props {
  lawFirmId: string;
}

const CONTACT_TYPE = "LawFirm";
const INITIAL_FORM = {
  firstName: "",
  lastName: "",
  contactSubtype: "",
  organization: "",
  email: "",
  phone: "",
  addressLine1: "",
  city: "",
  state: "",
  postalCode: "",
};
const PAGE_SIZE = 12;

export function LawFirmContactSection({ lawFirmId }: Props) {
  const addToast = useLienStore((s) => s.addToast);
  const [contacts, setContacts] = useState<ContactResponseDto[]>([]);
  const [roles, setRoles] = useState<LookupData[]>([]);
  const [states, setStates] = useState<LookupData[]>([]);
  const [loading, setLoading] = useState(true);
  const [viewMode, setViewMode] = useState<"tile" | "list">("tile");
  const [page, setPage] = useState(1);
  const [modalOpen, setModalOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<ContactResponseDto | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<ContactResponseDto | null>(
    null,
  );
  const [form, setForm] = useState({ ...INITIAL_FORM });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  const fetchContacts = useCallback(async () => {
    try {
      setLoading(true);
      const { data } = await contactsApi.list({
        LawFirmId: lawFirmId,
        ContactType: CONTACT_TYPE,
      });
      setContacts(Array.isArray(data.items) ? data.items : []);
    } catch {
      setContacts([]);
    } finally {
      setLoading(false);
    }
  }, [lawFirmId]);

  useEffect(() => {
    fetchContacts();
    Promise.allSettled([
      lookupApi.getLawFirmContactRoles(),
      lookupApi.getStates(),
    ]).then(([rolesRes, statesRes]) => {
      if (rolesRes.status === "fulfilled")
        setRoles(Array.isArray(rolesRes.value.data) ? rolesRes.value.data : []);
      if (statesRes.status === "fulfilled")
        setStates(
          Array.isArray(statesRes.value.data) ? statesRes.value.data : [],
        );
    });
  }, [fetchContacts]);

  const openAdd = () => {
    setEditTarget(null);
    setForm({ ...INITIAL_FORM, contactSubtype: roles[0]?.code ?? "" });
    setErrors({});
    setModalOpen(true);
  };

  const openEdit = (c: ContactResponseDto) => {
    setEditTarget(c);
    setForm({
      firstName: c.firstName,
      lastName: c.lastName,
      contactSubtype: c.contactSubtype ?? "",
      organization: c.organization ?? "",
      email: c.email ?? "",
      phone: c.phone ?? "",
      addressLine1: c.addressLine1 ?? "",
      city: c.city ?? "",
      state: c.state ?? "",
      postalCode: c.postalCode ?? "",
    });
    setErrors({});
    setModalOpen(true);
  };

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.firstName.trim()) e.firstName = "First Name is required";
    if (!form.lastName.trim()) e.lastName = "Last Name is required";
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
        contactType: CONTACT_TYPE,
        lawFirmId,
        contactSubtype: form.contactSubtype || undefined,
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        organization: form.organization.trim() || undefined,
        email: form.email.trim() || undefined,
        phone: form.phone.trim() || undefined,
        addressLine1: form.addressLine1.trim() || undefined,
        city: form.city.trim() || undefined,
        state: form.state || undefined,
        postalCode: form.postalCode.trim() || undefined,
      };
      if (editTarget) {
        await contactsApi.update(editTarget.id, payload);
        addToast({
          type: "success",
          title: "Contact Updated",
          description: "Law firm contact has been updated.",
        });
      } else {
        await contactsApi.create(payload);
        addToast({
          type: "success",
          title: "Contact Added",
          description: "Law firm contact has been added.",
        });
      }
      setModalOpen(false);
      fetchContacts();
    } catch (err) {
      addToast({
        type: "error",
        title: editTarget ? "Update Failed" : "Create Failed",
        description:
          err instanceof ApiError
            ? err.message
            : "An unexpected error occurred",
      });
    } finally {
      setSubmitting(false);
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      await contactsApi.delete(deleteTarget.id);
      addToast({
        type: "success",
        title: "Contact Removed",
        description: `${deleteTarget.firstName} ${deleteTarget.lastName} has been removed.`,
      });
      setDeleteTarget(null);
      fetchContacts();
    } catch (err) {
      addToast({
        type: "error",
        title: "Delete Failed",
        description:
          err instanceof ApiError
            ? err.message
            : "An unexpected error occurred",
      });
      setDeleteTarget(null);
    }
  };

  const getRoleLabel = (code: string | null | undefined) => {
    if (!code) return "—";
    return roles.find((r) => r.code === code)?.name ?? code;
  };

  const inputCls = (field: string) =>
    `w-full border rounded-lg px-3 py-2 text-sm ${errors[field] ? "border-red-300" : "border-gray-200"} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`;

  const totalPages = Math.ceil(contacts.length / PAGE_SIZE);
  const paged = contacts.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  return (
    <div className="bg-white border border-gray-200 rounded-xl">
      <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
        <div className="flex items-center gap-2">
          <i className="ri-scales-3-line text-gray-500" />
          <h3 className="text-sm font-semibold text-gray-800">
            Law Firm Contacts
          </h3>
          {!loading && (
            <span className="text-xs text-gray-400 bg-gray-100 rounded-full px-2 py-0.5">
              {contacts.length}
            </span>
          )}
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => setViewMode("tile")}
            title="Tile view"
            className={`p-1.5 rounded-lg transition-colors ${viewMode === "tile" ? "bg-primary/10 text-primary" : "text-gray-400 hover:bg-gray-100"}`}
          >
            <i className="ri-layout-grid-line text-base" />
          </button>
          <button
            onClick={() => setViewMode("list")}
            title="List view"
            className={`p-1.5 rounded-lg transition-colors ${viewMode === "list" ? "bg-primary/10 text-primary" : "text-gray-400 hover:bg-gray-100"}`}
          >
            <i className="ri-list-unordered text-base" />
          </button>
          <button
            onClick={openAdd}
            className="flex items-center gap-1.5 text-sm px-3 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90"
          >
            <i className="ri-add-line" />
            Add Contact
          </button>
        </div>
      </div>

      <div className="p-5">
        {loading ? (
          <div className="text-center py-10 text-sm text-gray-400">
            Loading contacts...
          </div>
        ) : contacts.length === 0 ? (
          <div className="text-center py-10 text-sm text-gray-400">
            No contacts yet. Add the first one.
          </div>
        ) : viewMode === "tile" ? (
          <TileView
            contacts={paged}
            getRoleLabel={getRoleLabel}
            onEdit={openEdit}
            onDelete={setDeleteTarget}
          />
        ) : (
          <ListView
            contacts={paged}
            getRoleLabel={getRoleLabel}
            onEdit={openEdit}
            onDelete={setDeleteTarget}
          />
        )}

        {totalPages > 1 && (
          <div className="flex items-center justify-center gap-2 mt-4 pt-4 border-t border-gray-100">
            <button
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page === 1}
              className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg hover:bg-gray-50 disabled:opacity-40"
            >
              Previous
            </button>
            <span className="text-sm text-gray-500">
              Page {page} of {totalPages}
            </span>
            <button
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page === totalPages}
              className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg hover:bg-gray-50 disabled:opacity-40"
            >
              Next
            </button>
          </div>
        )}
      </div>

      <FormModal
        open={modalOpen}
        onClose={() => setModalOpen(false)}
        onSubmit={handleSubmit}
        title={editTarget ? "Edit Law Firm Contact" : "Add Law Firm Contact"}
        subtitle="Law Firm Contact"
        submitLabel={
          submitting ? (editTarget ? "Saving..." : "Creating...") : "Save"
        }
        submitDisabled={submitting || !form.firstName || !form.lastName}
      >
        <div className="space-y-4">
          {/* Role */}
          {roles.length > 0 && (
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Role
              </label>
              <select
                value={form.contactSubtype}
                onChange={(e) =>
                  setForm({ ...form, contactSubtype: e.target.value })
                }
                className={inputCls("contactSubtype")}
              >
                <option value="">— Select Role —</option>
                {roles.map((r) => (
                  <option key={r.id} value={r.code}>
                    {r.name}
                  </option>
                ))}
              </select>
            </div>
          )}

          {/* Name */}
          <div className="grid grid-cols-2 gap-4">
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

          {/* Organization */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Organization
            </label>
            <input
              type="text"
              value={form.organization}
              onChange={(e) =>
                setForm({ ...form, organization: e.target.value })
              }
              placeholder="Organization or company name"
              className={inputCls("organization")}
            />
          </div>

          {/* Contact */}
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

          {/* Address */}
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
                  <option key={s.id} value={s.code}>
                    {s.code}
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
        </div>
      </FormModal>

      {deleteTarget && (
        <ConfirmDialog
          open
          onClose={() => setDeleteTarget(null)}
          onConfirm={handleDelete}
          title="Delete Contact"
          description={`Are you sure you want to delete ${deleteTarget.firstName} ${deleteTarget.lastName}?`}
          confirmLabel="Delete"
          confirmVariant="danger"
        />
      )}
    </div>
  );
}

function TileView({
  contacts,
  getRoleLabel,
  onEdit,
  onDelete,
}: {
  contacts: ContactResponseDto[];
  getRoleLabel: (code: string | null | undefined) => string;
  onEdit: (c: ContactResponseDto) => void;
  onDelete: (c: ContactResponseDto) => void;
}) {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
      {contacts.map((c) => (
        <div
          key={c.id}
          className="border border-gray-200 rounded-xl p-4 hover:shadow-sm transition-shadow"
        >
          <div className="flex items-start justify-between mb-3">
            <div>
              <p className="text-sm font-semibold text-gray-900 leading-snug">
                {c.firstName} {c.lastName}
              </p>
              {c.contactSubtype && (
                <span className="inline-block mt-0.5 text-xs text-primary bg-primary/10 rounded-full px-2 py-0.5">
                  {getRoleLabel(c.contactSubtype)}
                </span>
              )}
            </div>
            <ActionMenu
              items={[
                {
                  label: "Edit Contact",
                  icon: "ri-edit-line",
                  onClick: () => onEdit(c),
                },
                {
                  label: "Delete",
                  icon: "ri-delete-bin-line",
                  onClick: () => onDelete(c),
                  variant: "danger",
                  divider: true,
                },
              ]}
            />
          </div>
          {c.organization && (
            <p className="text-xs text-gray-500 mb-1.5">{c.organization}</p>
          )}
          <div className="flex items-center gap-2 text-xs text-gray-500 mb-1.5">
            <i className="ri-mail-line text-gray-400 shrink-0" />
            <span className="truncate">{c.email || "--"}</span>
          </div>
          <div className="flex items-center gap-2 text-xs text-gray-500">
            <i className="ri-phone-line text-gray-400 shrink-0" />
            <span>{c.phone || "--"}</span>
          </div>
        </div>
      ))}
    </div>
  );
}

function ListView({
  contacts,
  getRoleLabel,
  onEdit,
  onDelete,
}: {
  contacts: ContactResponseDto[];
  getRoleLabel: (code: string | null | undefined) => string;
  onEdit: (c: ContactResponseDto) => void;
  onDelete: (c: ContactResponseDto) => void;
}) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-gray-100">
            <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500">
              Name
            </th>
            <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500">
              Role
            </th>
            <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500">
              Organization
            </th>
            <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500">
              Email
            </th>
            <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500">
              Phone
            </th>
            <th className="px-3 py-2.5" />
          </tr>
        </thead>
        <tbody>
          {contacts.map((c) => (
            <tr
              key={c.id}
              className="border-b border-gray-50 hover:bg-gray-50/50"
            >
              <td className="px-3 py-3 text-gray-900 font-medium">
                {c.firstName} {c.lastName}
              </td>
              <td className="px-3 py-3">
                {c.contactSubtype && (
                  <span className="text-xs text-primary bg-primary/10 rounded-full px-2 py-0.5">
                    {getRoleLabel(c.contactSubtype)}
                  </span>
                )}
              </td>
              <td className="px-3 py-3 text-gray-500">
                {c.organization || "—"}
              </td>
              <td className="px-3 py-3 text-gray-500">{c.email || "—"}</td>
              <td className="px-3 py-3 text-gray-500">{c.phone || "—"}</td>
              <td className="px-3 py-3">
                <ActionMenu
                  items={[
                    {
                      label: "Edit Contact",
                      icon: "ri-edit-line",
                      onClick: () => onEdit(c),
                    },
                    {
                      label: "Delete",
                      icon: "ri-delete-bin-line",
                      onClick: () => onDelete(c),
                      variant: "danger",
                      divider: true,
                    },
                  ]}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
