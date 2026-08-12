"use client";

import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import { Repeat, Settings2, SquarePen, Trash2 } from "lucide-react";
import { ActionMenu, type ActionMenuItem } from "@/components/selling/action-menu";
import { FilterToolbar } from "@/components/lien/filter-toolbar";
import { ConfirmDialog } from "@/components/selling/modal";
import { BaseTable, type BaseTableFooterCell } from "@/components/ui/base-table";
import type { ColumnDef } from "@tanstack/react-table";
import { Pagination } from "@/components/ui/pagination";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  useContactPersons,
  useContactPersonTypes,
  useDeactivateContactPerson,
  useExportCompanyContacts,
} from "@/hooks/use-selling-companies";
import { ContactPersonFormModal } from "@/components/selling/forms/contact-person-form-modal";
import { ReassignContactPersonModal } from "@/components/selling/forms/reassign-contact-person-modal";
import { ContactsEmptyState } from "@/components/selling/contacts/contacts-empty-state";
import { CompanyStatsCards } from "@/components/selling/contacts/company-stats-cards";
import type { ContactPerson } from "@/lib/selling/companies.types";
import { useCompanyDetailContext } from "./context";
import { Button } from "@/components/ui/button";
import { downloadBlob } from "@/lib/utils";

const PRIMARY_BUTTON_CLASSNAME = "bg-[#EE7132] hover:bg-[#EE7132]/90 text-white";
const PAGE_SIZE_OPTIONS = [10, 25, 50];

export function CompanyContactPersonsTab() {
  const { id: companyId, company, canEdit } = useCompanyDetailContext();

  const [view, setView] = useState<"grid" | "list">("list");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(PAGE_SIZE_OPTIONS[0]);
  const [search, setSearch] = useState("");
  const [searchInput, setSearchInput] = useState("");
  const [typeFilter, setTypeFilter] = useState("");

  const contactPersonsQuery = useContactPersons(companyId, true);
  const contactPersonTypesQuery = useContactPersonTypes(company.companyTypeId);

  const [createOpen, setCreateOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<ContactPerson | null>(null);
  const [confirmAction, setConfirmAction] = useState<ContactPerson | null>(null);
  const [reassignTarget, setReassignTarget] = useState<ContactPerson | null>(null);

  const deactivateMutation = useDeactivateContactPerson();
  const exportMutation = useExportCompanyContacts();

  // Debounce search input before it drives the filter, so we don't re-filter on every keystroke.
  useEffect(() => {
    const timeout = setTimeout(() => setSearch(searchInput), 300);
    return () => clearTimeout(timeout);
  }, [searchInput]);

  useEffect(() => {
    setPage(1);
  }, [search, typeFilter, pageSize]);

  const allContacts = contactPersonsQuery.data ?? [];
  // No backend query params for search/type on the list endpoint yet — filter client-side.
  const contacts = useMemo(() => {
    const q = search.trim().toLowerCase();
    return allContacts.filter((c) => {
      const matchesSearch =
        !q ||
        c.displayName.toLowerCase().includes(q) ||
        (c.email ?? "").toLowerCase().includes(q);
      const matchesType = !typeFilter || c.contactPersonTypeId === typeFilter;
      return matchesSearch && matchesType;
    });
  }, [allContacts, search, typeFilter]);
  const totalRows = contacts.length;
  const totalPages = Math.max(Math.ceil(totalRows / pageSize), 1);
  const currentPage = Math.min(page, totalPages);
  const firstRow = totalRows === 0 ? 0 : (currentPage - 1) * pageSize + 1;
  const lastRow = Math.min(totalRows, currentPage * pageSize);
  const pagedContacts = useMemo(
    () => contacts.slice((currentPage - 1) * pageSize, currentPage * pageSize),
    [contacts, currentPage, pageSize],
  );

  const handleExport = () => {
    exportMutation.mutate(
      {
        companyId,
        query: { search: search || undefined, contactPersonTypeId: typeFilter || undefined, isActive: true },
      },
      {
        onSuccess: (blob) => {
          downloadBlob(
            blob,
            `${company.name}-contacts-${new Date().toISOString().slice(0, 10)}.csv`,
          );
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
    if (!confirmAction) return;
    const contact = confirmAction;
    const toastId = toast.loading(`Deleting ${contact.displayName}...`);
    setConfirmAction(null);
    deactivateMutation.mutate(
      { companyId, contactId: contact.id },
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

  const contactActions = (contact: ContactPerson): ActionMenuItem[] => [
    {
      label: "Reassign",
      icon: Repeat,
      disabled: !canEdit,
      onClick: () => setReassignTarget(contact),
    },
    {
      label: "Edit",
      icon: SquarePen,
      disabled: !canEdit,
      onClick: () => setEditTarget(contact),
    },
    {
      label: "Delete",
      icon: Trash2,
      variant: "danger",
      disabled: !canEdit,
      onClick: () => setConfirmAction(contact),
    },
  ];

  const columns: ColumnDef<ContactPerson, any>[] = [
    { accessorKey: "displayName", header: "Name" },
    { accessorKey: "email", header: "Email", cell: ({ getValue }) => getValue<string>() || "—" },
    { accessorKey: "phone", header: "Phone", cell: ({ getValue }) => getValue<string>() || "—" },
    { accessorKey: "contactPersonTypeName", header: "Role" },
    {
      // No per-contact case-count endpoint yet — placeholder until it exists.
      id: "activeCases",
      header: "Active Cases",
      cell: () => "—",
    },
    {
      id: "actions",
      header: "",
      enableSorting: false,
      cell: ({ row }) => (
        <div className="flex justify-end">
          <ActionMenu items={contactActions(row.original)} />
        </div>
      ),
      meta: { align: "right", width: "56px" },
    },
  ];

  const footerCells: BaseTableFooterCell[] = [];

  return (
    <div className="space-y-5">
      <CompanyStatsCards />

      <div className="bg-white border border-gray-200 rounded-xl">
        <FilterToolbar
          bare
          searchPlaceholder="Search contact persons by name, email..."
          onSearch={setSearchInput}
          filters={[
            {
              label: "Filter",
              value: typeFilter,
              onChange: setTypeFilter,
              options: contactPersonTypesQuery.options,
              icon: <Settings2 className="h-4 w-4" />,
            },
          ]}
        >
          <div className="flex items-center border border-gray-200 rounded-lg overflow-hidden">
            <button
              aria-label="Grid view"
              onClick={() => setView("grid")}
              className={`w-8 h-8 flex items-center justify-center transition-colors ${
                view === "grid" ? "bg-[#EE7132] text-white" : "text-gray-400 hover:text-gray-600"
              }`}
            >
              <i className="ri-grid-fill text-base" />
            </button>
            <button
              aria-label="List view"
              onClick={() => setView("list")}
              className={`w-8 h-8 flex items-center justify-center transition-colors ${
                view === "list" ? "bg-[#EE7132] text-white" : "text-gray-400 hover:text-gray-600"
              }`}
            >
              <i className="ri-list-unordered text-base" />
            </button>
          </div>

          <Button
            variant="secondary"
            iconDivider
            rightIcon={<i className="ri-upload-2-line text-base" />}
            disabled={exportMutation.isPending}
            onClick={handleExport}
          >
            {exportMutation.isPending ? "Exporting..." : "Export"}
          </Button>

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
        </FilterToolbar>

        {contactPersonsQuery.isLoading && (
          <div className="px-6 py-8 text-center text-sm text-gray-400">Loading...</div>
        )}

        {!contactPersonsQuery.isLoading && allContacts.length === 0 && (
          <ContactsEmptyState
            icon="ri-contacts-line"
            title="No Contact Person Yet"
            description="No contact persons have been added yet to this company. Add your first contact person."
            actionLabel={canEdit ? "Add Contact Person" : undefined}
            onAction={canEdit ? () => setCreateOpen(true) : undefined}
          />
        )}

        {!contactPersonsQuery.isLoading && allContacts.length > 0 && contacts.length === 0 && (
          <ContactsEmptyState
            icon="ri-search-line"
            title="No Matching Contact Person"
            description="No contact persons match your search or filter. Try adjusting them."
          />
        )}

        {!contactPersonsQuery.isLoading && contacts.length > 0 && view === "list" && (
          <BaseTable
            columns={columns}
            data={pagedContacts}
            getRowId={(row) => row.id}
            enablePagination={false}
            footerCells={footerCells.length ? footerCells : undefined}
            className="border-0 rounded-none"
            primaryButtonClassName={PRIMARY_BUTTON_CLASSNAME}
          />
        )}

        {!contactPersonsQuery.isLoading && contacts.length > 0 && view === "grid" && (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 p-6">
            {pagedContacts.map((contact) => (
              <div key={contact.id} className="border border-gray-200 rounded-xl p-4">
                <div className="flex items-start justify-between">
                  <div className="min-w-0">
                    <p className="text-sm font-semibold text-gray-900 truncate">
                      {contact.displayName}
                    </p>
                    <p className="text-xs text-gray-400 mt-0.5">{contact.contactPersonTypeName}</p>
                  </div>
                  <ActionMenu items={contactActions(contact)} />
                </div>
                <div className="mt-3 space-y-1.5">
                  <p className="text-xs text-gray-500 flex items-center gap-1.5">
                    <i className="ri-mail-line text-gray-300" />
                    {contact.email || "—"}
                  </p>
                  <p className="text-xs text-gray-500 flex items-center gap-1.5">
                    <i className="ri-phone-line text-gray-300" />
                    {contact.phone || "—"}
                  </p>
                </div>
                <div className="border-t border-gray-100 mt-3 pt-3">
                  <p className="text-xs text-gray-400">
                    Active Cases: <span className="text-gray-700 font-medium">—</span>
                  </p>
                </div>
              </div>
            ))}
          </div>
        )}

        {!contactPersonsQuery.isLoading && contacts.length > 0 && (
          <div className="flex items-center justify-between px-6 py-3 border-t border-gray-100">
            <div className="flex items-center gap-3 text-xs text-gray-500">
              <span className="flex items-center gap-2">
                Rows per page:
                <Select
                  value={String(pageSize)}
                  onValueChange={(value) => {
                    setPageSize(Number(value));
                    setPage(1);
                  }}
                >
                  <SelectTrigger className="h-7 w-[68px] px-2 text-xs" aria-label="Rows per page">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {PAGE_SIZE_OPTIONS.map((size) => (
                      <SelectItem key={size} value={String(size)} className="text-xs">
                        {size}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </span>
              <span>
                {totalRows === 0 ? "0" : `${firstRow}-${lastRow}`} of {totalRows}
              </span>
            </div>
            {totalPages > 1 && (
              <Pagination
                page={currentPage}
                totalPages={totalPages}
                onPageChange={setPage}
                primaryButtonClassName={PRIMARY_BUTTON_CLASSNAME}
              />
            )}
          </div>
        )}
      </div>

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

      {reassignTarget && (
        <ReassignContactPersonModal
          open
          companyId={companyId}
          contactPerson={reassignTarget}
          onClose={() => setReassignTarget(null)}
        />
      )}

      {confirmAction && (
        <ConfirmDialog
          open
          onClose={() => setConfirmAction(null)}
          onConfirm={handleDelete}
          title="Delete Contact Person"
          description={`Are you sure you want to delete ${confirmAction.displayName}? This action cannot be undone.`}
          confirmLabel="Delete"
          confirmVariant="danger"
          loading={deactivateMutation.isPending}
          warningTitle="Warning: Deleting this contact person will also remove:"
          warningItems={["All case associations", "All activity history"]}
        />
      )}
    </div>
  );
}
