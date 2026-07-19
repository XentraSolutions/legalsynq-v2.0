"use client";

import { useState, useEffect, useMemo } from "react";
import { useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { toast } from "sonner";
import { PageHeader } from "@/components/lien/page-header";
import { FilterToolbar } from "@/components/lien/filter-toolbar";
import { ActionMenu } from "@/components/lien/action-menu";
import { SideDrawer } from "@/components/lien/side-drawer";
import { AddContactModal } from "@/components/lien/add-contact-modal";
import { useLienStore } from "@/stores/lien-store";
import { useRoleAccess } from "@/hooks/use-role-access";
import {
  contactsService,
  CASE_REASSIGN_CONFIG,
  type ContactListItem,
} from "@/lib/contacts";
import {
  useContacts,
  useContactTypes,
  useDeleteContact,
  useBatchReassignContact,
} from "@/hooks/use-contacts";
import { useSessionContext } from "@/providers/session-provider";
import { ConfirmDialog, Modal } from "@/components/lien/modal";
import { ContactPicker } from "@/components/lien/contact-picker";
import { useRouter } from "next/navigation";
import type { ColumnDef } from "@tanstack/react-table";
import { BaseTable } from "@/components/ui/base-table";

const PAGE_SIZE = 10;

function pluralize(name: string): string {
  if (/s$/i.test(name)) return name;
  if (/[^aeiou]y$/i.test(name)) return `${name.slice(0, -1)}ies`;
  return `${name}s`;
}

export const dynamic = "force-dynamic";

export default function ContactsPage() {
  const addToast = useLienStore((s) => s.addToast);
  const router = useRouter();
  const ra = useRoleAccess();
  const queryClient = useQueryClient();

  const [contactData, setContactData] = useState<ContactListItem>();
  const { lookup } = useSessionContext();

  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [searchInput, setSearchInput] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const [showCreate, setShowCreate] = useState<{
    open: boolean;
    mode?: "create" | "edit" | undefined;
  }>({ open: false, mode: "create" });
  const [previewId, setPreviewId] = useState<string | null>(null);

  const [confirmAction, setConfirmAction] = useState<{
    id: string;
    action: string;
    label: string;
    contact: ContactListItem;
  } | null>(null);

  const [reassignTarget, setReassignTarget] = useState<ContactListItem | null>(
    null,
  );
  const [newContactId, setNewContactId] = useState("");
  const [reassignModalOpen, setReassignModalOpen] = useState(false);
  const batchReassignMutation = useBatchReassignContact();

  // Debounce search input before it drives the query, so we don't refetch on every keystroke.
  useEffect(() => {
    const timeout = setTimeout(() => setSearch(searchInput), 300);
    return () => clearTimeout(timeout);
  }, [searchInput]);

  useEffect(() => {
    setPage(1);
  }, [search, typeFilter]);

  const contactsQuery = useContacts({
    search: search || undefined,
    ContactType: typeFilter || undefined,
    // Main list only shows top-level contacts, not sub-contacts (e.g. law
    // firm staff, facility staff) — those live under their parent's detail page.
    ContactSubtype: "",
    page,
    pageSize: PAGE_SIZE,
  });
  const contactTypesQuery = useContactTypes();
  const knownContactTypesQuery = useContactTypes({ knownOnly: true });
  const deleteContactMutation = useDeleteContact();

  const contacts = useMemo(
    () => contactsQuery.data?.items ?? [],
    [contactsQuery.data],
  );
  const totalCount = contactsQuery.data?.pagination.totalCount ?? 0;
  const totalPages = contactsQuery.data?.pagination.totalPages ?? 0;
  // isLoading only covers the very first fetch (no cached/placeholder data yet);
  // isFetching also covers refetches, so the table can stay mounted and just show a subtle refreshing state.
  const loading = contactsQuery.isLoading;
  const refreshing = contactsQuery.isFetching && !contactsQuery.isLoading;
  const contactTypes = useMemo(
    () => contactTypesQuery.data?.items ?? [],
    [contactTypesQuery.data],
  );
  const knownContactTypes = useMemo(
    () => knownContactTypesQuery.data?.items ?? [],
    [knownContactTypesQuery.data],
  );

  useEffect(() => {
    if (contactsQuery.isError) {
      addToast({
        type: "error",
        title: "Load Failed",
        description:
          contactsQuery.error instanceof Error
            ? contactsQuery.error.message
            : "Failed to load contacts",
      });
    }
  }, [contactsQuery.isError, contactsQuery.error, addToast]);

  const previewContact = previewId
    ? contacts.find((c) => c.id === previewId)
    : null;

  const showCreateForm = async (mode: "create" | "edit") => {
    setShowCreate({ open: true, mode: mode });
  };

  const exportContacts = async () => {
    if (typeFilter == "MedicalFacility") return exportFacilityContacts();
    const response = await contactsService.exportContacts(typeFilter);
    const csv = atob(response.data);

    const now = new Date();
    const date = now.toISOString().split("T")[0];
    const time = now.toTimeString().split(" ")[0].replace(/:/g, "-");
    const filename = `contacts_${date}_${time}.csv`;

    const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
    const link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.download = filename;
    link.click();
  };

  const exportFacilityContacts = async () => {
    const response = await contactsService.exportFacilityContacts("");
    const csv = atob(response.data);
    const now = new Date();
    const date = now.toISOString().split("T")[0];
    const time = now.toTimeString().split(" ")[0].replace(/:/g, "-");
    const filename = `contacts_${date}_${time}.csv`;
    const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
    const link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.download = filename;
    link.click();
  };

  // As far as we know, legacy does not differentiate between deactivating and
  // deleting a contact, so the active/inactive toggle is disabled for now.
  // const handleToggleActive = async (c: ContactListItem) => {
  //   try {
  //     if (c.isActive) {
  //       await contactsService.deactivateContact(c.id);
  //       addToast({ type: "success", title: "Deactivated", description: c.displayName });
  //     } else {
  //       await contactsService.reactivateContact(c.id);
  //       addToast({ type: "success", title: "Activated", description: c.displayName });
  //     }
  //     fetchContacts();
  //   } catch (err) {
  //     addToast({
  //       type: "error",
  //       title: "Action Failed",
  //       description: err instanceof Error ? err.message : "Failed to update status",
  //     });
  //   }
  // };

  // Runs (and retries) the delete request, driving a single toast through
  // loading -> success/error like a background job. Passing the same toast
  // id updates it in place; if the user already dismissed it, sonner just
  // creates a fresh one instead, so completion is never silently lost.
  const runDeleteContact = (contact: ContactListItem) => {
    const toastId = toast.loading(`Deleting ${contact.displayName}...`);
    deleteContactMutation.mutate(contact.id, {
      onSuccess: () => {
        toast.success("Contact deleted", {
          id: toastId,
          description: contact.displayName,
        });
      },
      onError: (err) => {
        toast.error("Couldn't delete contact", {
          id: toastId,
          description: err instanceof Error ? err.message : contact.displayName,
          action: {
            label: "Retry",
            onClick: () =>
              setConfirmAction({
                id: contact.id,
                action: "delete",
                label: "Delete",
                contact,
              }),
          },
        });
      },
    });
  };

  const handleConfirmAction = () => {
    if (!confirmAction) return;
    if (confirmAction.action === "delete") {
      const { contact } = confirmAction;
      // Close the dialog immediately so deletion feels instant; the toast
      // takes over as the processing indicator from here.
      setConfirmAction(null);
      runDeleteContact(contact);
    }
  };

  const runBatchReassign = () => {
    if (!reassignTarget || !newContactId) return;
    const target = reassignTarget;
    const targetTypeLabel =
      contactTypeMap[target.contactType]?.toLowerCase() ?? "contact";
    // Close the modal immediately so reassigning feels instant; the toast
    // takes over as the processing indicator. The selection is kept in
    // state (reassignTarget/newContactId aren't cleared) so that if it
    // fails, the modal can reopen pre-filled instead of losing the input.
    setReassignModalOpen(false);
    const toastId = toast.loading(
      `Reassigning cases from ${target.displayName}...`,
    );
    batchReassignMutation.mutate(
      {
        contactType: target.contactType,
        oldId: target.id,
        newId: newContactId,
      },
      {
        onSuccess: () => {
          toast.success("Cases reassigned", {
            id: toastId,
            description: `All cases have been moved to the new ${targetTypeLabel}.`,
          });
          setReassignTarget(null);
          setNewContactId("");
        },
        onError: (err) => {
          toast.error("Couldn't reassign cases", {
            id: toastId,
            description:
              err instanceof Error ? err.message : target.displayName,
          });
          // Reopen with the same selection so the user can retry — never
          // automatically resubmit the request.
          setReassignModalOpen(true);
        },
      },
    );
  };

  const activeContactTypes = useMemo(
    () =>
      contactTypes
        .filter((t) => t.isActive)
        .sort((a, b) => a.sortOrder - b.sortOrder),
    [contactTypes],
  );

  const contactTypeMap = useMemo(
    () => Object.fromEntries(activeContactTypes.map((t) => [t.code, t.name])),
    [activeContactTypes],
  );

  const activeKnownContactTypes = useMemo(
    () =>
      knownContactTypes
        .filter((t) => t.isActive)
        .sort((a, b) => a.sortOrder - b.sortOrder),
    [knownContactTypes],
  );

  const tabs = useMemo(
    () => [
      { key: "", label: "All" },
      ...activeKnownContactTypes.map((t) => ({
        key: t.code,
        label: pluralize(t.name),
      })),
    ],
    [activeKnownContactTypes],
  );

  const nameColumnLabel = typeFilter
    ? (contactTypeMap[typeFilter] ?? "Contact Name")
    : "Contact Name";

  const columns = useMemo<ColumnDef<ContactListItem, any>[]>(
    () => [
      {
        id: "name",
        header: nameColumnLabel,
        cell: ({ row }) => {
          const c = row.original;
          const isDeleting =
            deleteContactMutation.isPending &&
            deleteContactMutation.variables === c.id;
          return (
            <Link
              href={`/lien/contacts/${c.id}`}
              onClick={(e) => e.stopPropagation()}
              className={`text-sm font-medium text-gray-700 hover:text-primary ${
                isDeleting ? "line-through" : ""
              }`}
            >
              {c.displayName}
            </Link>
          );
        },
      },
      {
        id: "type",
        header: "Type",
        cell: ({ row }) => (
          <span className="inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium bg-gray-50 text-gray-600 border-gray-200">
            {contactTypeMap[row.original.contactType] ??
              row.original.contactType}
          </span>
        ),
      },
      {
        id: "email",
        header: "Email",
        cell: ({ row }) => (
          <span className="text-sm text-gray-500">
            {row.original.email || "—"}
          </span>
        ),
      },
      {
        id: "activeCases",
        header: "Active Cases",
        cell: ({ row }) => (
          <span className="text-sm text-gray-500">
            {row.original.activeCases}
          </span>
        ),
      },
      {
        id: "actions",
        header: "",
        meta: { align: "right" },
        cell: ({ row }) => {
          const c = row.original;
          const isDeleting =
            deleteContactMutation.isPending &&
            deleteContactMutation.variables === c.id;
          if (isDeleting) {
            return (
              <i className="ri-loader-4-line animate-spin text-gray-400" />
            );
          }
          return (
            <div onClick={(e) => e.stopPropagation()}>
              <ActionMenu
                items={[
                  {
                    label: "View Details",
                    icon: "ri-eye-line",
                    onClick: () => router.push(`/lien/contacts/${c.id}`),
                  },
                  {
                    label: "Reassign",
                    icon: "ri-exchange-line",
                    onClick: () => {
                      setReassignTarget(c);
                      setNewContactId("");
                      setReassignModalOpen(true);
                    },
                  },
                  {
                    label: "Edit Contact",
                    icon: "ri-pencil-line",
                    onClick: () => {
                      setContactData(c);
                      showCreateForm("edit");
                    },
                  },
                  // Activate/Deactivate disabled: as far as we know, legacy has no active/inactive vs delete differentiation.
                  // { label: c.isActive ? "Deactivate" : "Activate", icon: c.isActive ? "ri-user-unfollow-line" : "ri-user-follow-line", onClick: () => handleToggleActive(c) },
                  {
                    label: "Delete",
                    icon: "ri-delete-bin-line",
                    onClick: () =>
                      setConfirmAction({
                        id: c.id,
                        action: "delete",
                        label: "Delete",
                        contact: c,
                      }),
                  },
                ]}
              />
            </div>
          );
        },
      },
    ],
    [nameColumnLabel, contactTypeMap, deleteContactMutation, router],
  );

  return (
    <div className="space-y-5">
      <PageHeader
        title="Contacts"
        subtitle={`${totalCount} contacts`}
        actions={
          ra.can("contact:create") ? (
            <button
              onClick={() => showCreateForm("create")}
              className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors"
            >
              <i className="ri-add-line text-base" />
              Add Contact
            </button>
          ) : undefined
        }
      />

      {/* Contact type tabs */}
      <div className="flex items-center gap-1 overflow-x-auto border-b border-gray-200 pb-0">
        {tabs.map((tab) => (
          <button
            key={tab.key}
            onClick={() => setTypeFilter(tab.key)}
            className={`shrink-0 px-4 py-2.5 text-sm font-medium border-b-2 transition-colors ${
              typeFilter === tab.key
                ? "border-primary text-primary"
                : "border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300"
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      <FilterToolbar
        searchPlaceholder="Search contacts by name, org, or email..."
        onSearch={setSearchInput}
        filters={[]}
      >
        <button
          onClick={() => exportContacts()}
          className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors"
        >
          Export
        </button>
      </FilterToolbar>

      <div className="relative">
        {refreshing && (
          <div className="absolute top-2 right-3 z-10 flex items-center gap-1.5 text-xs text-gray-400">
            <i className="ri-loader-4-line animate-spin text-sm" />
            Refreshing...
          </div>
        )}
        <BaseTable
          data={contacts}
          columns={columns}
          getRowId={(c) => c.id}
          isLoading={loading}
          emptyMessage="No contacts found."
          onRowClick={(c) => setPreviewId(c.id)}
          getRowClassName={(c) =>
            deleteContactMutation.isPending &&
            deleteContactMutation.variables === c.id
              ? "opacity-40 bg-red-50/60 pointer-events-none"
              : undefined
          }
          manualPagination
          pageCount={totalPages}
          totalCount={totalCount}
          pagination={{ pageIndex: page - 1, pageSize: PAGE_SIZE }}
          onPaginationChange={(updater) => {
            const next =
              typeof updater === "function"
                ? updater({ pageIndex: page - 1, pageSize: PAGE_SIZE })
                : updater;
            setPage(next.pageIndex + 1);
          }}
          className="bg-white border-gray-200 rounded-xl"
        />
      </div>

      {showCreate.open && (
        <AddContactModal
          open={showCreate.open}
          title={showCreate.mode === "edit" ? "Edit Contact" : "Add New Contact"}
          contactType={
            showCreate.mode === "create" ? typeFilter || undefined : undefined
          }
          contactTypeOptions={activeKnownContactTypes}
          editTarget={showCreate.mode === "edit" ? contactData : null}
          onClose={() => setShowCreate({ open: false })}
          onSaved={() => {
            setShowCreate({ open: false });
            queryClient.invalidateQueries({ queryKey: ["contacts"] });
          }}
        />
      )}

      {confirmAction && confirmAction.action === "delete" && (
        <ConfirmDialog
          open
          onClose={() => setConfirmAction(null)}
          onConfirm={handleConfirmAction}
          title="Delete Contact"
          description={
            <>
              Are you sure you want to delete{" "}
              <span className="font-semibold text-primary">
                {confirmAction.contact.displayName}
              </span>
              ? This action cannot be undone and will permanently remove all
              associated data.
            </>
          }
          confirmLabel="Delete"
          confirmVariant="danger"
          loading={deleteContactMutation.isPending}
          warningTitle="Warning: Deleting this contact will also remove:"
          warningItems={[
            ...(["LawFirm", "MedicalFacility"].includes(
              confirmAction.contact.contactType,
            ) &&
            !(
              confirmAction.contact.lawFirmId ||
              confirmAction.contact.facilityId
            )
              ? ["All associated staff/sub-contacts"]
              : []),
            "All case associations",
            "All uploaded documents",
            "All activity history",
          ]}
        />
      )}

      {/* Batch Reassign Modal */}
      {reassignTarget && (
        <Modal
          open={reassignModalOpen}
          onClose={() => {
            setReassignModalOpen(false);
            setReassignTarget(null);
          }}
          title="Re-Assign Case"
          size="sm"
          footer={
            <div className="flex items-center justify-between w-full">
              <button
                onClick={() => {
                  setReassignModalOpen(false);
                  setReassignTarget(null);
                }}
                disabled={batchReassignMutation.isPending}
                className="text-sm font-medium text-primary hover:underline disabled:opacity-50"
              >
                Cancel
              </button>
              <button
                onClick={runBatchReassign}
                disabled={!newContactId || batchReassignMutation.isPending}
                className="px-4 py-2 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg disabled:opacity-50 transition-colors"
              >
                {batchReassignMutation.isPending
                  ? "Assigning..."
                  : "Assign Case"}
              </button>
            </div>
          }
        >
          <div className="flex items-center gap-2 mb-4">
            <span className="flex items-center justify-center w-7 h-7 rounded-lg bg-primary text-white shrink-0">
              <i className="ri-exchange-line text-sm" />
            </span>
            <span className="text-sm font-semibold text-primary">
              Case Assignment Details
            </span>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              <span className="text-red-500 mr-0.5">*</span>
              {CASE_REASSIGN_CONFIG[reassignTarget.contactType]?.fieldLabel ??
                `Available ${
                  contactTypeMap[reassignTarget.contactType] ?? "Contact"
                }`}
            </label>
            <ContactPicker
              contactType={reassignTarget.contactType}
              excludeId={reassignTarget.id}
              value={newContactId}
              onChange={setNewContactId}
              placeholder="Please select"
              disabled={batchReassignMutation.isPending}
            />
          </div>
        </Modal>
      )}

      <SideDrawer
        open={!!previewContact}
        onClose={() => setPreviewId(null)}
        title={previewContact?.displayName || ""}
        subtitle={previewContact?.organization}
      >
        {previewContact && (
          <div className="space-y-4">
            <span className="inline-flex items-center rounded-full border px-2.5 py-1 text-sm font-medium bg-gray-50 text-gray-600 border-gray-200">
              {contactTypeMap[previewContact.contactType] ??
                previewContact.contactType}
            </span>
            <div className="grid grid-cols-2 gap-3 text-sm">
              <div>
                <p className="text-xs text-gray-400">Email</p>
                <p className="text-gray-700">{previewContact.email || "—"}</p>
              </div>
              <div>
                <p className="text-xs text-gray-400">Phone</p>
                <p className="text-gray-700">{previewContact.phone || "—"}</p>
              </div>
              <div>
                <p className="text-xs text-gray-400">Location</p>
                <p className="text-gray-700">
                  {previewContact.city}
                  {previewContact.city && previewContact.state ? ", " : ""}
                  {previewContact.state || "—"}
                </p>
              </div>
              <div>
                <p className="text-xs text-gray-400">Active Cases</p>
                <p className="text-gray-700">{previewContact.activeCases}</p>
              </div>
            </div>
            <Link
              href={`/lien/contacts/${previewContact.id}`}
              className="block text-center text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90"
            >
              View Full Details
            </Link>
          </div>
        )}
      </SideDrawer>
    </div>
  );
}
