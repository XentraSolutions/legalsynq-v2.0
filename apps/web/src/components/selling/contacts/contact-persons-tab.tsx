"use client";

import { useState } from "react";
import { toast } from "sonner";
import { ActionMenu } from "@/components/lien/action-menu";
import { ConfirmDialog } from "@/components/selling/modal";
import {
  useContactPersons,
  useDeactivateContactPerson,
  useReactivateContactPerson,
} from "@/hooks/use-selling-companies";
import { ContactPersonFormModal } from "@/components/selling/forms/contact-person-form-modal";
import { ContactsEmptyState } from "@/components/selling/contacts/contacts-empty-state";
import type { ContactPerson } from "@/lib/selling/companies.types";
import { useCompanyDetailContext } from "./context";
import { Button } from "@/components/ui/button";

const PRIMARY_BUTTON_CLASSNAME = "bg-[#EE7132] hover:bg-[#EE7132]/90 text-white";

export function CompanyContactPersonsTab() {
  const { id: companyId, company, canEdit } = useCompanyDetailContext();

  const [showActive, setShowActive] = useState(true);
  const contactPersonsQuery = useContactPersons(companyId, showActive);

  const [createOpen, setCreateOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<ContactPerson | null>(null);
  const [confirmAction, setConfirmAction] = useState<{
    action: "deactivate" | "reactivate";
    contact: ContactPerson;
  } | null>(null);

  const deactivateMutation = useDeactivateContactPerson();
  const reactivateMutation = useReactivateContactPerson();

  const contacts = contactPersonsQuery.data ?? [];

  const handleToggleActive = () => {
    if (!confirmAction) return;
    const { action, contact } = confirmAction;
    const mutation = action === "deactivate" ? deactivateMutation : reactivateMutation;
    const toastId = toast.loading(
      `${action === "deactivate" ? "Deactivating" : "Reactivating"} ${contact.displayName}...`,
    );
    setConfirmAction(null);
    mutation.mutate(
      { companyId, contactId: contact.id },
      {
        onSuccess: () => {
          toast.success(`Contact person ${action === "deactivate" ? "deactivated" : "reactivated"}`, {
            id: toastId,
            description: contact.displayName,
          });
        },
        onError: (err) => {
          toast.error(`Couldn't ${action} contact person`, {
            id: toastId,
            description: err instanceof Error ? err.message : contact.displayName,
          });
        },
      },
    );
  };

  return (
    <div className="bg-white border border-gray-200 rounded-xl">
      <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
        <div className="flex items-center gap-1 bg-[#FAFAFA] rounded-md p-1">
          <button
            onClick={() => setShowActive(true)}
            className={`px-3 py-1.5 text-xs font-medium rounded-lg transition-colors ${
              showActive ? "bg-white shadow-sm text-gray-900" : "text-gray-500 hover:text-gray-700"
            }`}
          >
            Active
          </button>
          <button
            onClick={() => setShowActive(false)}
            className={`px-3 py-1.5 text-xs font-medium rounded-lg transition-colors ${
              !showActive ? "bg-white shadow-sm text-gray-900" : "text-gray-500 hover:text-gray-700"
            }`}
          >
            Inactive
          </button>
        </div>

        {canEdit && (
          <Button
            className={`${PRIMARY_BUTTON_CLASSNAME}`}
            iconDivider
            rightIcon={<i className="ri-add-line text-base" />}
            onClick={() => setCreateOpen(true)}
          >
            Add Contact Person
          </Button>
        )}
      </div>

      {contactPersonsQuery.isLoading && (
        <div className="px-6 py-8 text-center text-sm text-gray-400">Loading...</div>
      )}

      {!contactPersonsQuery.isLoading && contacts.length === 0 && (
        showActive ? (
          <ContactsEmptyState
            icon="ri-contacts-line"
            title="No Contact Person Yet"
            description="No contact persons have been added yet. Add your first contact person to get started."
            actionLabel={canEdit ? "Add Contact Person" : undefined}
            onAction={canEdit ? () => setCreateOpen(true) : undefined}
          />
        ) : (
          <div className="px-6 py-8 text-center text-sm text-gray-400">No inactive contact persons.</div>
        )
      )}

      {!contactPersonsQuery.isLoading && contacts.length > 0 && (
        <div className="divide-y divide-gray-100">
          {contacts.map((contact) => (
            <div key={contact.id} className="flex items-center justify-between px-6 py-3.5">
              <div className="min-w-0">
                <p className="text-sm font-medium text-gray-800 truncate">{contact.displayName}</p>
                <p className="text-xs text-gray-400 mt-0.5">
                  {contact.contactPersonTypeName}
                  {contact.email ? ` · ${contact.email}` : ""}
                  {contact.phone ? ` · ${contact.phone}` : ""}
                </p>
              </div>
              {canEdit && (
                <ActionMenu
                  items={[
                    {
                      label: "Edit",
                      icon: "ri-pencil-line",
                      onClick: () => setEditTarget(contact),
                    },
                    contact.isActive
                      ? {
                          label: "Deactivate",
                          icon: "ri-forbid-line",
                          onClick: () => setConfirmAction({ action: "deactivate", contact }),
                        }
                      : {
                          label: "Reactivate",
                          icon: "ri-check-line",
                          onClick: () => setConfirmAction({ action: "reactivate", contact }),
                        },
                  ]}
                />
              )}
            </div>
          ))}
        </div>
      )}

      {createOpen && (
        <ContactPersonFormModal
          open={createOpen}
          title="Add Contact Person"
          companyId={companyId}
          companyName={company.name}
          companyTypeId={company.companyTypeId}
          onClose={() => setCreateOpen(false)}
          onSaved={() => setCreateOpen(false)}
        />
      )}

      {editTarget && (
        <ContactPersonFormModal
          open
          title="Edit Contact Person"
          companyId={companyId}
          companyName={company.name}
          companyTypeId={company.companyTypeId}
          editTarget={editTarget}
          onClose={() => setEditTarget(null)}
          onSaved={() => setEditTarget(null)}
        />
      )}

      {confirmAction && (
        <ConfirmDialog
          open
          onClose={() => setConfirmAction(null)}
          onConfirm={handleToggleActive}
          title={
            confirmAction.action === "deactivate" ? "Deactivate Contact Person" : "Reactivate Contact Person"
          }
          description={`Are you sure you want to ${confirmAction.action} ${confirmAction.contact.displayName}?`}
          confirmLabel={confirmAction.action === "deactivate" ? "Deactivate" : "Reactivate"}
          confirmVariant={confirmAction.action === "deactivate" ? "danger" : "primary"}
          loading={deactivateMutation.isPending || reactivateMutation.isPending}
        />
      )}
    </div>
  );
}
