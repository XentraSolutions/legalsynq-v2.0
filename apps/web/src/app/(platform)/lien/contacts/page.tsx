"use client";

import { useState, useEffect, useMemo } from "react";
import { useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { toast } from "sonner";
import { toast } from "sonner";
import { PageHeader } from "@/components/lien/page-header";
import { FilterToolbar } from "@/components/lien/filter-toolbar";
import { ActionMenu } from "@/components/lien/action-menu";
import { SideDrawer } from "@/components/lien/side-drawer";
import { AddContactForm } from "@/components/lien/forms/add-contact-form";
import { useLienStore } from "@/stores/lien-store";
import { useRoleAccess } from "@/hooks/use-role-access";
import { contactsService, type ContactListItem } from "@/lib/contacts";
import {
  useContacts,
  useContactTypes,
  useDeleteContact,
} from "@/hooks/use-contacts";
import { useSessionContext } from "@/providers/session-provider";
import { ConfirmDialog } from "@/components/lien/modal";
import { useRouter } from "next/navigation";
import {
  Table,
  TableHeader,
  TableBody,
  TableRow,
  TableHead,
  TableCell,
} from "@/components/ui/table";
import { Pagination } from "@/components/ui/pagination";

const PAGE_SIZE = 10;

export const dynamic = "force-dynamic";

export default function ContactsPage() {
  const addToast = useLienStore((s) => s.addToast);
  const router = useRouter();
  const ra = useRoleAccess();
  const queryClient = useQueryClient();

  const [contactData, setContactData] = useState<ContactListItem>();
  const { lookup } = useSessionContext();
  const [states, setStates] = useState(lookup?.State);

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
  const [reassignSelectedId, setReassignSelectedId] = useState("");

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
    setStates(lookup?.State);
    setShowCreate({ open: true, mode: mode });
  };

  const exportContacts = async () => {
    if (typeFilter == "Facility") return exportFacilityContacts();
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

  const KNOWN_TAB_CODES = [
    "LawFirm",
    "MedicalFacility",
    "Provider",
    "FundingCompany",
    "Lead",
  ];

  const tabs = useMemo(
    () => [
      { key: "", label: "All" },
      ...activeContactTypes
        .filter((t) => KNOWN_TAB_CODES.includes(t.code))
        .map((t) => ({ key: t.code, label: t.name })),
    ],
    [activeContactTypes],
  );

  const nameColumnLabel = typeFilter
    ? (contactTypeMap[typeFilter] ?? "Contact Name")
    : "Contact Name";

  const reassignPool = useMemo(() => {
    if (!reassignTarget) return [];
    return contacts
      .filter(
        (c) =>
          c.contactType === reassignTarget.contactType &&
          c.id !== reassignTarget.id,
      )
      .map((c) => ({ id: c.id, label: c.displayName }));
  }, [reassignTarget, contacts]);

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

      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden relative">
        {refreshing && (
          <div className="absolute top-2 right-3 flex items-center gap-1.5 text-xs text-gray-400">
            <i className="ri-loader-4-line animate-spin text-sm" />
            Refreshing...
          </div>
        )}
        {loading ? (
          <div className="p-10 text-center text-sm text-gray-400">
            Loading contacts...
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{nameColumnLabel}</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Email</TableHead>
                <TableHead>Active Cases</TableHead>
                {/* Status column removed: as far as we know, legacy has no active/inactive vs delete differentiation, so only active contacts ever show up. */}
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {contacts.map((c) => {
                const isDeleting =
                  deleteContactMutation.isPending &&
                  deleteContactMutation.variables === c.id;
                return (
                  <TableRow
                    key={c.id}
                    className={`cursor-pointer transition-opacity duration-200 ${
                      isDeleting
                        ? "opacity-40 bg-red-50/60 pointer-events-none"
                        : ""
                    }`}
                    onClick={() => setPreviewId(c.id)}
                  >
                    <TableCell>
                      <Link
                        href={`/lien/contacts/${c.id}`}
                        onClick={(e) => e.stopPropagation()}
                        className={`text-sm font-medium text-gray-700 hover:text-primary ${
                          isDeleting ? "line-through" : ""
                        }`}
                      >
                        {c.displayName}
                      </Link>
                    </TableCell>
                    <TableCell>
                      <span className="inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium bg-gray-50 text-gray-600 border-gray-200">
                        {contactTypeMap[c.contactType] ?? c.contactType}
                      </span>
                    </TableCell>
                    <TableCell className="text-sm text-gray-500">
                      {c.email || "—"}
                    </TableCell>
                    <TableCell className="text-sm text-gray-500">0</TableCell>
                    <TableCell
                      className="text-right"
                      onClick={(e) => e.stopPropagation()}
                    >
                      {isDeleting ? (
                        <i className="ri-loader-4-line animate-spin text-gray-400" />
                      ) : (
                        <ActionMenu
                          items={[
                            {
                              label: "View Details",
                              icon: "ri-eye-line",
                              onClick: () =>
                                router.push(`/lien/contacts/${c.id}`),
                            },
                            {
                              label: "Reassign",
                              icon: "ri-exchange-line",
                              onClick: () => {
                                setReassignTarget(c);
                                setReassignSelectedId("");
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
                      )}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        )}
        {!loading && contacts?.length === 0 && (
          <div className="p-10 text-center text-sm text-gray-400">
            No contacts found.
          </div>
        )}
      </div>

      {!loading && totalCount > 0 && (
        <div className="flex items-center justify-between">
          <p className="text-xs text-gray-500">
            Page {page} of {totalPages} · {totalCount} total
          </p>
          <Pagination
            page={page}
            totalPages={totalPages}
            onPageChange={setPage}
          />
        </div>
      )}

      {showCreate.open && (
        <AddContactForm
          open={showCreate.open}
          mode={showCreate.mode}
          defaultContactType={typeFilter || undefined}
          data={{
            addressLine1: "",
            postalCode: "",
            ...contactData,
            contactTypes: activeContactTypes,
            states: states ?? [],
          }}
          onClose={() => setShowCreate({ open: false })}
          onCreated={() =>
            queryClient.invalidateQueries({ queryKey: ["contacts"] })
          }
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

      {/* Reassign Modal */}
      {reassignTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="bg-white rounded-xl shadow-xl p-6 w-full max-w-md space-y-4">
            <div>
              <h2 className="text-base font-semibold text-gray-800">
                Re-Assign Case
              </h2>
              <p className="text-sm text-gray-500 mt-1">
                Select another{" "}
                {contactTypeMap[reassignTarget.contactType]?.toLowerCase() ??
                  "contact"}{" "}
                to assign this case to.
              </p>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Assign to
              </label>
              <select
                value={reassignSelectedId}
                onChange={(e) => setReassignSelectedId(e.target.value)}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
              >
                <option value="">Select contact...</option>
                {reassignPool.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.label}
                  </option>
                ))}
              </select>
              {reassignPool.length === 0 && (
                <p className="text-xs text-gray-400 mt-1">
                  No other contacts of this type available.
                </p>
              )}
            </div>
            <div className="flex justify-end gap-2 pt-2">
              <button
                onClick={() => setReassignTarget(null)}
                className="px-4 py-2 text-sm text-gray-600 border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors"
              >
                Cancel
              </button>
              <button
                disabled={!reassignSelectedId}
                onClick={() => {
                  addToast({
                    type: "success",
                    title: "Case Assigned",
                    description: "Case has been reassigned.",
                  });
                  setReassignTarget(null);
                }}
                className="px-4 py-2 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg disabled:opacity-40 transition-colors"
              >
                Assign Case
              </button>
            </div>
          </div>
        </div>
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
                <p className="text-gray-700">0</p>
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
