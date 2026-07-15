"use client";

import { useState, useEffect, useCallback } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { useLienStore } from "@/stores/lien-store";
import { useRoleAccess } from "@/hooks/use-role-access";
import { useDeleteContact } from "@/hooks/use-contacts";
import { CONTACT_TYPE_LABELS } from "@/types/lien";
import { contactsService, CASE_REASSIGN_CONFIG, type ContactDetail } from "@/lib/contacts";
import { ApiError } from "@/lib/api-client";
import { AddContactModal } from "@/components/lien/add-contact-modal";
import { ConfirmDialog } from "@/components/lien/modal";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { ContactDetailContextProvider } from "./contact-detail-context";

// Contact types with a known case-lookup API (mirrors ContactCasesSection) —
// the Cases tab only makes sense for these.
const CASES_SUPPORTED_TYPES = new Set(Object.keys(CASE_REASSIGN_CONFIG));

function staffTabLabel(contact: ContactDetail): string | null {
  if (contact.contactType === "MedicalFacility" && !contact.contactSubtype) {
    return "Medical Facility Staff";
  }
  if (contact.contactType === "LawFirm") {
    return "Legal Contacts";
  }
  return null;
}

export function ContactDetailShell({
  id,
  children,
}: {
  id: string;
  children: React.ReactNode;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const queryClient = useQueryClient();
  const addToast = useLienStore((s) => s.addToast);
  const ra = useRoleAccess();
  const deleteContactMutation = useDeleteContact();

  const [contact, setContact] = useState<ContactDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [editOpen, setEditOpen] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);

  const fetchContact = useCallback(async () => {
    try {
      setLoading(true);
      const detail = await contactsService.getContact(id);
      setContact(detail);
    } catch (err) {
      addToast({
        type: "error",
        title: "Load Failed",
        description:
          err instanceof Error ? err.message : "Failed to load contact",
      });
    } finally {
      setLoading(false);
    }
  }, [id, addToast]);

  useEffect(() => {
    fetchContact();
  }, [fetchContact]);

  const canEdit = ra.can("contact:edit");

  if (loading) {
    return <ContactDetailShellSkeleton />;
  }

  if (!contact) {
    return (
      <div className="p-10 text-center space-y-3">
        <i className="ri-error-warning-line text-3xl text-gray-300" />
        <p className="text-sm text-gray-500">Contact not found.</p>
        <Link
          href="/lien/contacts"
          className="text-sm text-primary hover:underline"
        >
          Back to Contacts
        </Link>
      </div>
    );
  }

  const staffLabel = staffTabLabel(contact);
  const tabs = [
    { key: "overview", label: "Overview" },
    ...(CASES_SUPPORTED_TYPES.has(contact.contactType)
      ? [{ key: "cases", label: "Cases" }]
      : []),
    { key: "activities", label: "Activities" },
    ...(staffLabel ? [{ key: "staff", label: staffLabel }] : []),
  ];

  const handleDelete = async () => {
    try {
      await deleteContactMutation.mutateAsync(id);
      addToast({
        type: "success",
        title: "Contact Deleted",
        description: `${contact.displayName} has been deleted.`,
      });
      queryClient.invalidateQueries({ queryKey: ["contacts"] });
      router.push("/lien/contacts");
    } catch (err) {
      addToast({
        type: "error",
        title: "Delete Failed",
        description:
          err instanceof ApiError ? err.message : "An unexpected error occurred",
      });
      setConfirmDelete(false);
    }
  };

  return (
    <div className="space-y-5">
      <div className="text-xs text-gray-400 flex items-center gap-1">
        <Link
          href="/lien/contacts"
          className="hover:text-gray-600 transition-colors"
        >
          Contacts
        </Link>
        <i className="ri-arrow-right-s-line text-sm" />
        <span className="text-gray-500">
          {CONTACT_TYPE_LABELS[contact.contactType] ?? contact.contactType}
        </span>
      </div>

      <div className="bg-white border border-gray-200 rounded-xl">
        <div className="px-6 py-5">
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 lg:items-center">
            <div className="min-w-0">
              <div className="flex items-center gap-2.5">
                <h1 className="text-2xl font-bold text-gray-900 truncate">
                  {contact.displayName}
                </h1>
                <span className="inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-semibold bg-gray-50 text-gray-600 border-gray-200 shrink-0">
                  {CONTACT_TYPE_LABELS[contact.contactType] ?? contact.contactType}
                </span>
              </div>
              {contact.organization && (
                <p className="text-sm text-gray-500 mt-1">
                  {contact.organization}
                </p>
              )}
              <p className="text-sm text-gray-400 mt-1.5 truncate">
                Contact ID: {contact.id}
              </p>
            </div>

            <div className="grid grid-cols-3 gap-x-8 gap-y-4">
              <HeaderStat icon="ri-mail-line" label="Email" value={contact.email} />
              <HeaderStat icon="ri-phone-line" label="Phone" value={contact.phone} />
              <HeaderStat
                icon="ri-map-pin-line"
                label="Address"
                value={[contact.city, contact.state].filter(Boolean).join(", ")}
              />
              <HeaderStat
                icon="ri-folder-2-line"
                label="Active Cases"
                value={String(contact.activeCases)}
              />

              <div className="flex items-center">
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <button className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors">
                      Actions
                      <i className="ri-arrow-down-s-line text-base" />
                    </button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    {canEdit && (
                      <DropdownMenuItem onClick={() => setEditOpen(true)}>
                        <i className="ri-pencil-line" />
                        Edit Contact
                      </DropdownMenuItem>
                    )}
                    {contact.email && (
                      <DropdownMenuItem asChild>
                        <a href={`mailto:${contact.email}`}>
                          <i className="ri-mail-line" />
                          Send Email
                        </a>
                      </DropdownMenuItem>
                    )}
                    <DropdownMenuSeparator />
                    <DropdownMenuItem
                      onClick={() => setConfirmDelete(true)}
                      className="text-red-600 focus:bg-red-50"
                    >
                      <i className="ri-delete-bin-line" />
                      Delete Contact
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </div>
            </div>
          </div>
        </div>

        <div className="border-t border-gray-100 px-6">
          <nav className="flex flex-wrap gap-4 -mb-px">
            {tabs.map((tab) => {
              const href = `/lien/contacts/${id}/${tab.key}`;
              const isActive = pathname?.startsWith(href);
              return (
                <Link
                  key={tab.key}
                  href={href}
                  className={[
                    "px-4 py-2.5 text-sm font-medium border-b-2 transition-colors whitespace-nowrap",
                    isActive
                      ? "border-primary text-primary"
                      : "border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300",
                  ].join(" ")}
                >
                  {tab.label}
                </Link>
              );
            })}
          </nav>
        </div>
      </div>

      <ContactDetailContextProvider
        value={{ id, contact, canEdit, refetch: fetchContact }}
      >
        {children}
      </ContactDetailContextProvider>

      {editOpen && (
        <AddContactModal
          open={editOpen}
          title="Edit Contact"
          editTarget={contact}
          onClose={() => setEditOpen(false)}
          onSaved={() => {
            setEditOpen(false);
            fetchContact();
          }}
        />
      )}

      {confirmDelete && (
        <ConfirmDialog
          open
          onClose={() => setConfirmDelete(false)}
          onConfirm={handleDelete}
          title="Delete Contact"
          description={`Are you sure you want to delete ${contact.displayName}? This action cannot be undone.`}
          confirmLabel="Delete"
          confirmVariant="danger"
          loading={deleteContactMutation.isPending}
        />
      )}
    </div>
  );
}

function HeaderStat({
  icon,
  label,
  value,
}: {
  icon: string;
  label: string;
  value?: React.ReactNode;
}) {
  return (
    <div className="min-w-0 flex items-start gap-2">
      <i className={`${icon} text-base text-gray-300 mt-0.5`} />
      <div className="min-w-0">
        <p className="text-[11px] text-gray-400 uppercase tracking-wide leading-tight">
          {label}
        </p>
        <p className="text-sm text-gray-700 font-medium mt-1 truncate">
          {value || "---"}
        </p>
      </div>
    </div>
  );
}

// Mirrors the loaded layout's shape (breadcrumb, header card, tab bar) so
// the page never renders as an empty screen while the contact fetch is in
// flight — only the values inside are placeholders.
function ContactDetailShellSkeleton() {
  return (
    <div className="space-y-5 animate-pulse">
      <div className="h-3.5 w-32 bg-gray-100 rounded" />

      <div className="bg-white border border-gray-200 rounded-xl">
        <div className="px-6 py-5">
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 lg:items-center">
            <div className="min-w-0 space-y-2">
              <div className="h-6 w-56 bg-gray-100 rounded" />
              <div className="h-3.5 w-24 bg-gray-100 rounded" />
            </div>
            <div className="grid grid-cols-3 gap-x-8 gap-y-4">
              {Array.from({ length: 5 }).map((_, i) => (
                <div key={i} className="space-y-2">
                  <div className="h-2.5 w-12 bg-gray-100 rounded" />
                  <div className="h-4 w-20 bg-gray-100 rounded" />
                </div>
              ))}
            </div>
          </div>
        </div>
        <div className="border-t border-gray-100 px-6 py-4">
          <div className="flex gap-6">
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="h-3.5 w-16 bg-gray-100 rounded" />
            ))}
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
        <div className="lg:col-span-2 h-64 bg-white border border-gray-200 rounded-xl" />
        <div className="h-64 bg-white border border-gray-200 rounded-xl" />
      </div>
    </div>
  );
}
