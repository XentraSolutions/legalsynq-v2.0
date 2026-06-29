"use client";

import { useState, useEffect, useCallback, useMemo } from "react";
import Link from "next/link";
import { PageHeader } from "@/components/lien/page-header";
import { FilterToolbar } from "@/components/lien/filter-toolbar";
import { ActionMenu } from "@/components/lien/action-menu";
import { SideDrawer } from "@/components/lien/side-drawer";
import { AddContactForm } from "@/components/lien/forms/add-contact-form";
import { useLienStore } from "@/stores/lien-store";
import { useRoleAccess } from "@/hooks/use-role-access";
import type { LookupData } from "@/lib/lookup/lookup.types";
import { contactsService, type ContactListItem } from "@/lib/contacts";
import { lookupService } from "@/lib/lookup";
import { useSessionContext } from "@/providers/session-provider";
import { ConfirmDialog } from "@/components/lien/modal";
import { useRouter } from "next/navigation";
import { facilityService } from "@/lib/facility";
import type { LegacyFacilityItem } from "@/lib/facility/facility.types";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";

export const dynamic = "force-dynamic";

export default function ContactsPage() {
  const addToast = useLienStore((s) => s.addToast);
  const router = useRouter();
  const ra = useRoleAccess();

  const [contacts, setContacts] = useState<ContactListItem[]>([]);
  const [contactData, setContactData] = useState<ContactListItem>();
  const [legacyFacilities, setLegacyFacilities] = useState<LegacyFacilityItem[]>([]);
  const [contactTypes, setContactTypes] = useState<LookupData[]>([]);
  const { lookup } = useSessionContext();
  const [states, setStates] = useState(lookup?.State);

  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const isLegacyTab = typeFilter === "MedicalFacility";
  const [showCreate, setShowCreate] = useState<{
    open: boolean;
    mode?: "create" | "edit" | undefined;
  }>({ open: false, mode: "create" });
  const [previewId, setPreviewId] = useState<string | null>(null);

  const [confirmAction, setConfirmAction] = useState<{
    id: string;
    action: string;
    label: string;
  } | null>(null);

  const [reassignTarget, setReassignTarget] = useState<ContactListItem | LegacyFacilityItem | null>(null);
  const [reassignSelectedId, setReassignSelectedId] = useState("");

  const fetchContacts = useCallback(async () => {
    try {
      setLoading(true);

      if (isLegacyTab) {
        const [contactTypesRes, facilityRes] = await Promise.allSettled([
          lookupService.getContactTypes(),
          facilityService.getFacilityList(),
        ]);
        if (contactTypesRes.status === "fulfilled") setContactTypes(contactTypesRes.value.items);
        if (facilityRes.status === "fulfilled") {
          setLegacyFacilities(facilityRes.value.items);
          setTotalCount(facilityRes.value.totalCount);
        }
        return;
      }

      const [contactTypesRes, result] = await Promise.allSettled([
        await lookupService.getContactTypes(),
        await contactsService.getContacts({
          keyword: search || undefined,
          ContactType: typeFilter || undefined,
          pageSize: 100,
        }),
      ]);
      if (result.status == "fulfilled") {
        setContacts(result.value.items);
      }
      if (contactTypesRes.status == "fulfilled") {
        setContactTypes(contactTypesRes.value.items);
      }
      setTotalCount(result.value.pagination.totalCount);
    } catch (err) {
      addToast({
        type: "error",
        title: "Load Failed",
        description:
          err instanceof Error ? err.message : "Failed to load contacts",
      });
    } finally {
      setLoading(false);
    }
  }, [search, typeFilter, isLegacyTab, addToast]);

  useEffect(() => {
    fetchContacts();
  }, [fetchContacts]);

  const previewContact = previewId
    ? contacts.find((c) => c.id === previewId)
    : null;

  const showCreateForm = async (mode: "create" | "edit") => {
    setStates(lookup?.State);
    setShowCreate({ open: true, mode: mode });
  };

  const exportContacts = async () => {
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

  const handleToggleActive = async (c: ContactListItem) => {
    try {
      if (c.isActive) {
        await contactsService.deactivateContact(c.id);
        addToast({ type: "success", title: "Deactivated", description: c.displayName });
      } else {
        await contactsService.reactivateContact(c.id);
        addToast({ type: "success", title: "Activated", description: c.displayName });
      }
      fetchContacts();
    } catch (err) {
      addToast({
        type: "error",
        title: "Action Failed",
        description: err instanceof Error ? err.message : "Failed to update status",
      });
    }
  };

  const handleConfirmAction = async () => {
    if (!confirmAction) return;
    try {
      if (confirmAction.action === "delete") {
        await contactsService.deleteContact(confirmAction.id);
        addToast({
          type: "success",
          title: confirmAction.label,
          description: `Contact has been deleted`,
        });
        setConfirmAction(null);
        fetchContacts();
      }
    } catch (err) {
      addToast({
        type: "error",
        title: "Action Failed",
        description:
          err instanceof Error ? err.message : "Failed to update status",
      });
      setConfirmAction(null);
    }
  };

  const activeContactTypes = useMemo(
    () => contactTypes.filter((t) => t.isActive).sort((a, b) => a.sortOrder - b.sortOrder),
    [contactTypes],
  );

  const contactTypeMap = useMemo(
    () => Object.fromEntries(activeContactTypes.map((t) => [t.code, t.name])),
    [activeContactTypes],
  );

  const KNOWN_TAB_CODES = ["LawFirm", "MedicalFacility", "Provider", "FundingCompany", "Lead"];

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
    if (isLegacyTab) {
      return legacyFacilities
        .filter((f) => f.id !== reassignTarget.id)
        .map((f) => ({ id: f.id, label: f.name }));
    }
    const target = reassignTarget as ContactListItem;
    return contacts
      .filter((c) => c.contactType === target.contactType && c.id !== target.id)
      .map((c) => ({ id: c.id, label: c.displayName }));
  }, [reassignTarget, contacts, legacyFacilities, isLegacyTab]);

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
        onSearch={setSearch}
        filters={
          typeFilter === ""
            ? [
                {
                  label: "All Types",
                  value: typeFilter,
                  onChange: setTypeFilter,
                  options: contactTypes.map((v) => ({
                    value: v.code,
                    label: v.name,
                  })),
                },
              ]
            : []
        }
      >
        <button
          onClick={() => exportContacts()}
          className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors"
        >
          Export
        </button>
      </FilterToolbar>

      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        {loading ? (
          <div className="p-10 text-center text-sm text-gray-400">
            Loading {isLegacyTab ? "facilities" : "contacts"}...
          </div>
        ) : isLegacyTab ? (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{nameColumnLabel}</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Email</TableHead>
                <TableHead>Active Cases</TableHead>
                <TableHead>Status</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {legacyFacilities.map((f) => (
                <TableRow key={f.id}>
                  <TableCell>
                    <Link href={`/lien/contacts/legacy/${f.id}`} className="text-sm font-medium text-gray-700 hover:text-primary">
                      {f.name}
                    </Link>
                  </TableCell>
                  <TableCell>
                    <span className="inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium bg-gray-50 text-gray-600 border-gray-200">
                      {contactTypeMap["MedicalFacility"] ?? "Medical Facility"}
                    </span>
                  </TableCell>
                  <TableCell className="text-sm text-gray-500">{f.email || "—"}</TableCell>
                  <TableCell className="text-sm text-gray-500">0</TableCell>
                  <TableCell>
                    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${f.isActive ? "bg-green-50 text-green-700 border border-green-200" : "bg-gray-100 text-gray-500 border border-gray-200"}`}>
                      {f.isActive ? "Active" : "Inactive"}
                    </span>
                  </TableCell>
                  <TableCell className="text-right">
                    <ActionMenu
                      items={[
                        { label: "View Details", icon: "ri-eye-line", onClick: () => router.push(`/lien/contacts/legacy/${f.id}`) },
                        { label: "Reassign", icon: "ri-exchange-line", onClick: () => { setReassignTarget(f); setReassignSelectedId(""); } },
                        { label: "Edit Contact", icon: "ri-pencil-line", onClick: () => addToast({ type: "info", title: "Edit", description: "Edit not available for legacy facilities" }) },
                        { label: f.isActive ? "Deactivate" : "Activate", icon: f.isActive ? "ri-user-unfollow-line" : "ri-user-follow-line", onClick: () => addToast({ type: "info", title: "Status", description: "Status change not available for legacy facilities" }) },
                        { label: "Delete", icon: "ri-delete-bin-line", onClick: () => setConfirmAction({ id: f.id, action: "delete", label: "Delete" }) },
                      ]}
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{nameColumnLabel}</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Email</TableHead>
                <TableHead>Active Cases</TableHead>
                <TableHead>Status</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {contacts.map((c) => (
                <TableRow
                  key={c.id}
                  className="cursor-pointer"
                  onClick={() => setPreviewId(c.id)}
                >
                  <TableCell>
                    <Link
                      href={`/lien/contacts/${c.id}`}
                      onClick={(e) => e.stopPropagation()}
                      className="text-sm font-medium text-gray-700 hover:text-primary"
                    >
                      {c.displayName}
                    </Link>
                  </TableCell>
                  <TableCell>
                    <span className="inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium bg-gray-50 text-gray-600 border-gray-200">
                      {contactTypeMap[c.contactType] ?? c.contactType}
                    </span>
                  </TableCell>
                  <TableCell className="text-sm text-gray-500">{c.email || "—"}</TableCell>
                  <TableCell className="text-sm text-gray-500">0</TableCell>
                  <TableCell>
                    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${c.isActive ? "bg-green-50 text-green-700 border border-green-200" : "bg-gray-100 text-gray-500 border border-gray-200"}`}>
                      {c.isActive ? "Active" : "Inactive"}
                    </span>
                  </TableCell>
                  <TableCell className="text-right" onClick={(e) => e.stopPropagation()}>
                    <ActionMenu
                      items={[
                        { label: "View Details", icon: "ri-eye-line", onClick: () => router.push(`/lien/contacts/${c.id}`) },
                        { label: "Reassign", icon: "ri-exchange-line", onClick: () => { setReassignTarget(c); setReassignSelectedId(""); } },
                        { label: "Edit Contact", icon: "ri-pencil-line", onClick: () => { setContactData(c); showCreateForm("edit"); } },
                        { label: c.isActive ? "Deactivate" : "Activate", icon: c.isActive ? "ri-user-unfollow-line" : "ri-user-follow-line", onClick: () => handleToggleActive(c) },
                        { label: "Delete", icon: "ri-delete-bin-line", onClick: () => setConfirmAction({ id: c.id, action: "delete", label: "Delete" }) },
                      ]}
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
        {!loading && (isLegacyTab ? legacyFacilities.length === 0 : contacts?.length === 0) && (
          <div className="p-10 text-center text-sm text-gray-400">
            {isLegacyTab ? "No facilities found." : "No contacts found."}
          </div>
        )}
      </div>

      {showCreate.open && (
        <AddContactForm
          open={showCreate.open}
          mode={showCreate.mode}
          defaultContactType={typeFilter || undefined}
          data={{ addressLine1: "", postalCode: "", ...contactData, contactTypes: activeContactTypes, states: states ?? [] }}
          onClose={() => setShowCreate({ open: false })}
          onCreated={fetchContacts}
        />
      )}

      {confirmAction && (
        <ConfirmDialog
          open
          onClose={() => setConfirmAction(null)}
          onConfirm={handleConfirmAction}
          title={confirmAction.label}
          description={`Are you sure you want to ${confirmAction.label.toLowerCase()} this contact?`}
          confirmLabel={confirmAction.label}
          confirmVariant="primary"
        />
      )}

      {/* Reassign Modal */}
      {reassignTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="bg-white rounded-xl shadow-xl p-6 w-full max-w-md space-y-4">
            <div>
              <h2 className="text-base font-semibold text-gray-800">Re-Assign Case</h2>
              <p className="text-sm text-gray-500 mt-1">
                Select another{" "}
                {isLegacyTab
                  ? "facility"
                  : contactTypeMap[(reassignTarget as ContactListItem).contactType]?.toLowerCase() ?? "contact"}{" "}
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
                <p className="text-xs text-gray-400 mt-1">No other contacts of this type available.</p>
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
                  addToast({ type: "success", title: "Case Assigned", description: "Case has been reassigned." });
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
              {contactTypeMap[previewContact.contactType] ?? previewContact.contactType}
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
