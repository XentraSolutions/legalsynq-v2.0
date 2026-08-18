"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { Contact, Eye, Loader, SquarePen, Trash2 } from "lucide-react";
import type { ColumnDef } from "@tanstack/react-table";
import { FilterToolbar } from "@/components/lien/filter-toolbar";
import { ActionMenu } from "@/components/selling/action-menu";
import { ConfirmDialog } from "@/components/selling/modal";
import { BaseTable } from "@/components/ui/base-table";
import { ContactsEmptyState } from "@/components/selling/contacts/contacts-empty-state";
import { ContactsFilterModal } from "@/components/selling/contacts/contacts-filter-modal";
import { ContactPersonFormModal } from "@/components/selling/forms/contact-person-form-modal";
import { Button } from "@/components/selling/button";
import { useRoleAccess } from "@/hooks/use-role-access";
import { downloadBlob } from "@/lib/utils";
import {
  useCompanyTypes,
  useContactPersonTypes,
  useContactPersonsDirectory,
  useDeactivateContactPerson,
  useExportContacts,
} from "@/hooks/use-selling-companies";
import type { ContactPersonDirectoryItem } from "@/lib/selling/companies.types";
import {
  TABLE_CELL_CLASSNAME,
  TABLE_LINK_CLASSNAME,
  TABLE_HEADER_CLASSNAME,
  TABLE_HEADER_CELL_CLASSNAME,
} from "@/components/selling/table-cell-styles";

const DEFAULT_PAGE_SIZE = 10;
// Still needed as a passthrough for BaseTable (src/components/ui/base-table),
// which renders a plain <button> (not the selling Button component) and
// exposes primaryButtonClassName as a generic override, not selling-specific.
const PRIMARY_BUTTON_CLASSNAME = "bg-[#EE7132] hover:bg-[#EE7132]/90 text-white";

export function ContactPersonsDirectoryView() {
  const ra = useRoleAccess();
  const router = useRouter();

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [search, setSearch] = useState("");
  const [searchInput, setSearchInput] = useState("");
  const [companyTypeFilter, setCompanyTypeFilter] = useState("");
  const [contactPersonTypeFilter, setContactPersonTypeFilter] = useState("");
  const [showFilter, setShowFilter] = useState(false);
  const [showCreate, setShowCreate] = useState(false);
  const [editTarget, setEditTarget] = useState<ContactPersonDirectoryItem | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<ContactPersonDirectoryItem | null>(null);
  const activeFilterCount =
    (companyTypeFilter ? 1 : 0) + (contactPersonTypeFilter ? 1 : 0);

  useEffect(() => {
    const timeout = setTimeout(() => setSearch(searchInput), 300);
    return () => clearTimeout(timeout);
  }, [searchInput]);

  useEffect(() => {
    setPage(1);
  }, [search, companyTypeFilter, contactPersonTypeFilter, pageSize]);

  const companyTypesQuery = useCompanyTypes();
  // Tracks the Company Type currently picked *inside* the still-open filter
  // modal (via onDraftChange below) — not just the last-applied
  // `companyTypeFilter` — so Role's options refresh as soon as someone picks
  // a Company Type, instead of only after Apply is clicked.
  const [draftCompanyType, setDraftCompanyType] = useState(companyTypeFilter);
  const contactPersonTypesQuery = useContactPersonTypes(draftCompanyType || undefined, {
    enabled: Boolean(draftCompanyType),
  });

  const exportContactsMutation = useExportContacts();
  const deactivateMutation = useDeactivateContactPerson();

  const contactsQuery = useContactPersonsDirectory({
    search: search || undefined,
    companyTypeId: companyTypeFilter || undefined,
    contactPersonTypeId: contactPersonTypeFilter || undefined,
    isActive: true,
    page,
    pageSize,
  });

  const contacts = useMemo(() => contactsQuery.data?.items ?? [], [contactsQuery.data]);
  const totalCount = contactsQuery.data?.totalCount ?? 0;
  const totalPages = contactsQuery.data ? Math.ceil(contactsQuery.data.totalCount / pageSize) : 0;
  const loading = contactsQuery.isLoading;
  const refreshing = contactsQuery.isFetching && !contactsQuery.isLoading;

  useEffect(() => {
    if (contactsQuery.isError) {
      toast.error("Load failed", {
        description:
          contactsQuery.error instanceof Error
            ? contactsQuery.error.message
            : "Failed to load contact persons",
      });
    }
  }, [contactsQuery.isError, contactsQuery.error]);

  const handleExport = () => {
    exportContactsMutation.mutate(
      {
        search: search || undefined,
        companyTypeId: companyTypeFilter || undefined,
        contactPersonTypeId: contactPersonTypeFilter || undefined,
        isActive: true,
      },
      {
        onSuccess: (blob) => {
          downloadBlob(blob, `contacts-${new Date().toISOString().slice(0, 10)}.csv`);
        },
        onError: (err) => {
          toast.error("Export failed", {
            description: err instanceof Error ? err.message : "Failed to export contact persons",
          });
        },
      },
    );
  };

  const handleDelete = () => {
    if (!deleteTarget) return;
    const contact = deleteTarget;
    setDeleteTarget(null);
    const toastId = toast.loading(`Deleting ${contact.displayName}...`);
    deactivateMutation.mutate(
      { companyId: contact.companyId, contactId: contact.id },
      {
        onSuccess: () => {
          toast.success("Contact person deleted", {
            id: toastId,
            description: contact.displayName,
          });
        },
        onError: (err) => {
          toast.error("Couldn't delete contact person", {
            id: toastId,
            description: err instanceof Error ? err.message : contact.displayName,
          });
        },
      },
    );
  };

  const columns = useMemo<ColumnDef<ContactPersonDirectoryItem, any>[]>(
    () => [
      {
        id: "name",
        header: "Name",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>
            {row.original.displayName}
          </span>
        ),
      },
      {
        id: "company",
        header: "Company",
        cell: ({ row }) => (
          <Link
            href={`/selling/contacts/${row.original.companyId}`}
            className={TABLE_LINK_CLASSNAME}
          >
            {row.original.companyName}
          </Link>
        ),
      },
      {
        id: "role",
        header: "Role",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>
            {row.original.contactPersonTypeName || "—"}
          </span>
        ),
      },
      {
        id: "email",
        header: "Email",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>{row.original.email || "—"}</span>
        ),
      },
      {
        id: "phone",
        header: "Phone",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>{row.original.phone || "—"}</span>
        ),
      },
      {
        id: "actions",
        header: "",
        meta: { align: "right" },
        cell: ({ row }) => {
          const c = row.original;
          return (
            <div onClick={(e) => e.stopPropagation()}>
              <ActionMenu
                items={[
                  {
                    label: "View Company",
                    icon: Eye,
                    onClick: () => router.push(`/selling/contacts/${c.companyId}`),
                  },
                  {
                    label: "Edit",
                    icon: SquarePen,
                    disabled: !ra.can("contact:edit"),
                    onClick: () => setEditTarget(c),
                  },
                  {
                    label: "Delete",
                    icon: Trash2,
                    variant: "danger",
                    disabled: !ra.can("contact:edit"),
                    onClick: () => setDeleteTarget(c),
                  },
                ]}
              />
            </div>
          );
        },
      },
    ],
    [ra, router],
  );

  const showEmptyState = !loading && totalCount === 0;

  return (
    <>
      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        <FilterToolbar
          bare
          searchPlaceholder="Search contact persons by name, email..."
          onSearch={setSearchInput}
          filterActions={
            <Button
              variant="secondary"
              className="border-gray-300"
              leftIcon="settings2"
              onClick={() => setShowFilter(true)}
            >
              Filter
              {activeFilterCount > 0 && (
                <span className="ml-0.5 inline-flex items-center justify-center h-4 min-w-4 px-1 rounded-full bg-primary text-white text-[10px] font-semibold">
                  {activeFilterCount}
                </span>
              )}
            </Button>
          }
        >
          <Button
            variant="secondary"
            rightIcon="upload"
            disabled={exportContactsMutation.isPending}
            onClick={handleExport}
          >
            {exportContactsMutation.isPending ? "Exporting..." : "Export"}
          </Button>
          {ra.can("contact:create") && (
            <Button
              variant="primary"
              rightIcon="plus"
              onClick={() => setShowCreate(true)}
            >
              Add Contact Person
            </Button>
          )}
        </FilterToolbar>

        {showEmptyState ? (
          <ContactsEmptyState
            icon={Contact}
            title="No Contact Person Yet"
            description="No contact persons match your search or filters yet."
          />
        ) : (
          <div className="relative">
            {refreshing && (
              <div className="absolute top-2 right-3 z-10 flex items-center gap-1.5 text-xs text-gray-400">
                <Loader className="h-4 w-4 animate-spin" />
                Refreshing...
              </div>
            )}
            <BaseTable
              data={contacts}
              columns={columns}
              getRowId={(c) => c.id}
              isLoading={loading}
              emptyMessage="No contact persons found."
              manualPagination
              pageCount={totalPages}
              totalCount={totalCount}
              pagination={{ pageIndex: page - 1, pageSize }}
              onPaginationChange={(updater) => {
                const next =
                  typeof updater === "function"
                    ? updater({ pageIndex: page - 1, pageSize })
                    : updater;
                setPage(next.pageIndex + 1);
                if (next.pageSize !== pageSize) setPageSize(next.pageSize);
              }}
              pageSizeOptions={[10, 25, 50, 100]}
              className="border-0 rounded-none"
              primaryButtonClassName={PRIMARY_BUTTON_CLASSNAME}
              headerClassName={TABLE_HEADER_CLASSNAME}
              headerCellClassName={TABLE_HEADER_CELL_CLASSNAME}
            />
          </div>
        )}
      </div>

      <ContactsFilterModal
        open={showFilter}
        onClose={() => setShowFilter(false)}
        title="Filter Contact Persons"
        fields={[
          {
            key: "companyType",
            label: "Company Type",
            options: companyTypesQuery.options,
          },
          {
            key: "contactPersonType",
            label: "Role",
            options: contactPersonTypesQuery.options,
          },
        ]}
        value={{
          companyType: companyTypeFilter,
          contactPersonType: contactPersonTypeFilter,
        }}
        onDraftChange={(draft) => setDraftCompanyType(draft.companyType ?? "")}
        onApply={(next) => {
          const nextCompanyType = next.companyType ?? "";
          setCompanyTypeFilter(nextCompanyType);
          // Roles are scoped to a company type, so a role picked under the
          // previous type shouldn't silently carry over to a new one.
          setContactPersonTypeFilter(
            nextCompanyType === companyTypeFilter ? (next.contactPersonType ?? "") : "",
          );
        }}
      />

      {showCreate && (
        <ContactPersonFormModal
          open
          title="Add Contact Person"
          companyId=""
          companyName=""
          companyTypeId=""
          allowCompanySelect
          onClose={() => setShowCreate(false)}
          onSaved={() => setShowCreate(false)}
        />
      )}

      {editTarget && (
        <ContactPersonFormModal
          open
          title="Edit Contact Person"
          companyId={editTarget.companyId}
          companyName={editTarget.companyName}
          companyTypeId={editTarget.companyTypeId}
          editTarget={editTarget}
          onClose={() => setEditTarget(null)}
          onSaved={() => setEditTarget(null)}
        />
      )}

      {deleteTarget && (
        <ConfirmDialog
          open
          onClose={() => setDeleteTarget(null)}
          onConfirm={handleDelete}
          title="Delete Contact Person"
          description={`Are you sure you want to delete ${deleteTarget.displayName}? This action cannot be undone.`}
          confirmLabel="Delete"
          confirmVariant="danger"
          loading={deactivateMutation.isPending}
          warningTitle="Warning: Deleting this contact person will also remove:"
          warningItems={["All case associations", "All activity history"]}
        />
      )}
    </>
  );
}
