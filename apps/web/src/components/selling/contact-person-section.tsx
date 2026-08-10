"use client";

import { useState, useEffect, useCallback } from "react";
import { useLienStore } from "@/stores/lien-store";
import { contactsApi } from "@/lib/contacts/contacts.api";
import { lookupApi } from "@/lib/lookup/lookup.api";
import { type ContactResponseDto } from "@/lib/contacts/contacts.types";
import { type LookupData } from "@/lib/lookup/lookup.types";
import { ApiError } from "@/lib/api-client";
import { ConfirmDialog } from "@/components/selling/modal";
import { AddSubContactModal } from "@/components/lien/add-subcontact-modal";
import { ActionMenu } from "@/components/lien/action-menu";
import { Button } from "@/components/ui/button";

interface Props {
  /** The parent contact's own contactType (LawFirm, Provider, FundingCompany, Lead, ...). */
  contactType: string;
  /** The parent contact's id. */
  parentId: string;
  /** Overrides the default bg-primary styling on the Add Contact button and its modal/delete-confirm actions (selling's orange brand). */
  primaryButtonClassName?: string;
}

const PAGE_SIZE = 12;

/**
 * TODO(backend): generic "Contact Person" support for any contact type.
 * Today the contacts API only understands a parent link for two specific
 * types — LawFirmId (LawFirm, see LawFirmContactSection) and FacilityId
 * (MedicalFacility, see MedicalFacilityStaffSection). This section is the
 * Selling-only, generic stand-in for every other contact type (Provider,
 * FundingCompany, Lead, ...), built ahead of a backend change that adds a
 * generic ParentContactId filter (GET /lien/api/liens/contacts) and
 * parentContactId field (POST/PUT) — see ContactsQuery.ParentContactId and
 * CreateContactRequestDto.parentContactId. Until the backend lands, list/
 * create/update/delete here will not actually associate correctly; this is
 * a frontend-only placeholder so the UI is ready the moment it does.
 *
 * The Lien side intentionally does NOT use this component — its Staff tab
 * (components/lien/contact-detail/staff-tab.tsx) still only renders
 * LawFirmContactSection/MedicalFacilityStaffSection, unchanged.
 */
export function ContactPersonSection({ contactType, parentId, primaryButtonClassName }: Props) {
  const addToast = useLienStore((s) => s.addToast);
  const [contacts, setContacts] = useState<ContactResponseDto[]>([]);
  const [roles, setRoles] = useState<LookupData[]>([]);
  const [loading, setLoading] = useState(true);
  const [viewMode, setViewMode] = useState<"tile" | "list">("tile");
  const [page, setPage] = useState(1);
  const [modalOpen, setModalOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<ContactResponseDto | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<ContactResponseDto | null>(
    null,
  );

  const fetchContacts = useCallback(async () => {
    try {
      setLoading(true);
      // TODO(backend): ParentContactId isn't a real filter yet — see the
      // module doc above. This will return an empty/unfiltered list until
      // the API supports it.
      const { data } = await contactsApi.list({
        ParentContactId: parentId,
        ContactType: contactType,
      });
      setContacts(Array.isArray(data.items) ? data.items : []);
    } catch {
      setContacts([]);
    } finally {
      setLoading(false);
    }
  }, [parentId, contactType]);

  useEffect(() => {
    fetchContacts();
    lookupApi.getContactRoles().then((rolesRes) => {
      setRoles(Array.isArray(rolesRes.data) ? rolesRes.data : []);
    });
  }, [fetchContacts]);

  const openAdd = () => {
    setEditTarget(null);
    setModalOpen(true);
  };

  const openEdit = (c: ContactResponseDto) => {
    setEditTarget(c);
    setModalOpen(true);
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

  const totalPages = Math.ceil(contacts.length / PAGE_SIZE);
  const paged = contacts.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  return (
    <div className="bg-white border border-gray-200 rounded-xl">
      <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
        <div className="flex items-center gap-2">
          <i className="ri-contacts-line text-gray-500" />
          <h3 className="text-sm font-semibold text-gray-800">
            Contact Persons
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
          <Button
            className={`px-3 py-1.5 text-white ${primaryButtonClassName ?? "bg-primary hover:bg-primary/90"}`}
            rightIcon={<i className="ri-add-line" />}
            onClick={openAdd}
          >
            Add Contact
          </Button>
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
            <Button
              variant="secondary"
              className="px-3 py-1.5"
              disabled={page === 1}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
            >
              Previous
            </Button>
            <span className="text-sm text-gray-500">
              Page {page} of {totalPages}
            </span>
            <Button
              variant="secondary"
              className="px-3 py-1.5"
              disabled={page === totalPages}
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            >
              Next
            </Button>
          </div>
        )}
      </div>

      {modalOpen && (
        <AddSubContactModal
          open={modalOpen}
          onClose={() => setModalOpen(false)}
          title={editTarget ? "Edit Contact Person" : "Add Contact Person"}
          contactType={contactType}
          parentContactId={parentId}
          roleOptions={roles}
          allowCreateRole
          onRoleCreated={(role) => setRoles((prev) => [...prev, role])}
          editTarget={editTarget}
          primaryButtonClassName={primaryButtonClassName}
          onSaved={() => {
            setModalOpen(false);
            fetchContacts();
          }}
        />
      )}

      {deleteTarget && (
        <ConfirmDialog
          open
          onClose={() => setDeleteTarget(null)}
          onConfirm={handleDelete}
          title="Delete Contact"
          description={
            <>
              Are you sure you want to delete{" "}
              <span className="font-semibold text-primary">
                {deleteTarget.firstName} {deleteTarget.lastName}
              </span>
              ? This action cannot be undone and will permanently remove all
              associated data.
            </>
          }
          confirmLabel="Delete"
          confirmVariant="danger"
          primaryButtonClassName={primaryButtonClassName}
          warningTitle="Warning: Deleting this contact will also remove:"
          warningItems={[
            "All case associations",
            "All uploaded documents",
            "All activity history",
          ]}
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
