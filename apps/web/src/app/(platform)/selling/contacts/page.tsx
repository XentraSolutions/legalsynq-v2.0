"use client";

import { useState, useEffect, useMemo } from "react";
import Link from "next/link";
import { toast } from "sonner";
import {
  Building2,
  Eye,
  Loader,
  Repeat,
  Settings2,
  SquarePen,
  Trash2,
  UserCheck,
} from "lucide-react";
import { PageHeader } from "@/components/lien/page-header";
import { FilterToolbar } from "@/components/lien/filter-toolbar";
import { ActionMenu } from "@/components/selling/action-menu";
import { CompanyFormModal } from "@/components/selling/forms/company-form-modal";
import { ContactPersonFormModal } from "@/components/selling/forms/contact-person-form-modal";
import { ReassignCompanyModal } from "@/components/selling/forms/reassign-company-modal";
import { ContactsEmptyState } from "@/components/selling/contacts/contacts-empty-state";
import { ContactPersonsDirectoryView } from "@/components/selling/contacts/contact-persons-directory-view";
import { useRoleAccess } from "@/hooks/use-role-access";
import {
  useCompanies,
  useCompanyTypes,
  useDeactivateCompany,
  useExportCompanies,
} from "@/hooks/use-selling-companies";
import { ConfirmDialog } from "@/components/selling/modal";
import { useRouter, useSearchParams } from "next/navigation";
import type { ColumnDef } from "@tanstack/react-table";
import { BaseTable } from "@/components/ui/base-table";
import { Button } from "@/components/selling/button";
import type { Company } from "@/lib/selling/companies.types";
import { downloadBlob } from "@/lib/utils";
import {
  TABLE_CELL_CLASSNAME,
  TABLE_LINK_CLASSNAME,
  TABLE_HEADER_CLASSNAME,
  TABLE_HEADER_CELL_CLASSNAME,
} from "@/components/selling/table-cell-styles";

const DEFAULT_PAGE_SIZE = 10;
const PAGE_SIZE_OPTIONS = [10, 25, 50, 100];

// Still needed as a passthrough for BaseTable (src/components/ui/base-table),
// which renders a plain <button> (not the selling Button component) and
// exposes primaryButtonClassName as a generic override, not selling-specific.
const PRIMARY_BUTTON_CLASSNAME =
  "bg-[#EE7132] hover:bg-[#EE7132]/90 text-white";

const VIEWS = [
  { key: "companies", label: "Companies" },
  { key: "contacts", label: "Contact Person" },
] as const;

export const dynamic = "force-dynamic";

export default function SellingContactsPage() {
  const searchParams = useSearchParams();
  const view =
    searchParams.get("view") === "contacts" ? "contacts" : "companies";

  return (
    <div className="space-y-5">
      <PageHeader
        card={false}
        title="Contacts"
        subtitle="Keep your company and contact person records organized and easily accessible."
      />

      <div className="flex flex-wrap gap-1 bg-[#FAFAFA] rounded-md p-1">
        {VIEWS.map((tab) => (
          <Link
            key={tab.key}
            href={
              tab.key === "companies"
                ? "/selling/contacts"
                : `/selling/contacts?view=${tab.key}`
            }
            className={`flex-1 text-center px-4 py-2 text-sm font-medium rounded-lg transition-colors whitespace-nowrap ${
              view === tab.key
                ? "bg-white shadow-sm text-gray-900"
                : "text-gray-500 hover:text-gray-700"
            }`}
          >
            {tab.label}
          </Link>
        ))}
      </div>

      {view === "companies" ? (
        <CompaniesListView />
      ) : (
        <ContactPersonsDirectoryView />
      )}
    </div>
  );
}

function CompaniesListView() {
  const router = useRouter();
  const ra = useRoleAccess();

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [search, setSearch] = useState("");
  const [searchInput, setSearchInput] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const [showCreate, setShowCreate] = useState(false);
  const [newCompany, setNewCompany] = useState<Company | null>(null);
  const [contactPersonTarget, setContactPersonTarget] =
    useState<Company | null>(null);
  // After the contact person form saves, offer to loop back into it again
  // instead of just closing — set once a contact has been added for `contactPersonTarget`.
  const [contactAddedFor, setContactAddedFor] = useState<Company | null>(null);
  const [editTarget, setEditTarget] = useState<Company | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Company | null>(null);
  const [reassignTarget, setReassignTarget] = useState<Company | null>(null);

  const deactivateCompanyMutation = useDeactivateCompany();
  const exportCompaniesMutation = useExportCompanies();

  const handleExport = () => {
    exportCompaniesMutation.mutate(
      {
        search: search || undefined,
        companyTypeId: typeFilter || undefined,
        isActive: true,
      },
      {
        onSuccess: (blob) => {
          downloadBlob(
            blob,
            `companies-${new Date().toISOString().slice(0, 10)}.csv`,
          );
        },
        onError: (err) => {
          toast.error("Export failed", {
            description:
              err instanceof Error ? err.message : "Failed to export companies",
          });
        },
      },
    );
  };

  // Debounce search input before it drives the query, so we don't refetch on every keystroke.
  useEffect(() => {
    const timeout = setTimeout(() => setSearch(searchInput), 300);
    return () => clearTimeout(timeout);
  }, [searchInput]);

  useEffect(() => {
    setPage(1);
  }, [search, typeFilter, pageSize]);

  const companyTypesQuery = useCompanyTypes();
  const companyTypes = useMemo(
    () => companyTypesQuery.data ?? [],
    [companyTypesQuery.data],
  );

  const companiesQuery = useCompanies({
    search: search || undefined,
    companyTypeId: typeFilter || undefined,
    isActive: true,
    page,
    pageSize,
  });

  const companies = useMemo(
    () => companiesQuery.data?.items ?? [],
    [companiesQuery.data],
  );
  const totalCount = companiesQuery.data?.totalCount ?? 0;
  const totalPages = companiesQuery.data
    ? Math.ceil(companiesQuery.data.totalCount / pageSize)
    : 0;
  const loading = companiesQuery.isLoading;
  const refreshing = companiesQuery.isFetching && !companiesQuery.isLoading;

  useEffect(() => {
    if (companiesQuery.isError) {
      toast.error("Load failed", {
        description:
          companiesQuery.error instanceof Error
            ? companiesQuery.error.message
            : "Failed to load companies",
      });
    }
  }, [companiesQuery.isError, companiesQuery.error]);

  // The underlying endpoint is already a soft-delete (DELETE /companies/{id}),
  // and this list only ever queries isActive:true, so a deactivated company
  // simply drops out of view — "Delete" in the UI, deactivate under the hood.
  const handleDelete = () => {
    if (!deleteTarget) return;
    const company = deleteTarget;
    setDeleteTarget(null);
    const toastId = toast.loading(`Deleting ${company.name}...`);
    deactivateCompanyMutation.mutate(company.id, {
      onSuccess: () => {
        toast.success("Company deleted", {
          id: toastId,
          description: company.name,
        });
      },
      onError: (err) => {
        toast.error("Couldn't delete company", {
          id: toastId,
          description: err instanceof Error ? err.message : company.name,
        });
      },
    });
  };

  const columns = useMemo<ColumnDef<Company, any>[]>(
    () => [
      {
        id: "name",
        header: "Company Name",
        cell: ({ row }) => {
          const c = row.original;
          return (
            <Link
              href={`/selling/contacts/${c.id}`}
              onClick={(e) => e.stopPropagation()}
              className={TABLE_LINK_CLASSNAME}
            >
              {c.name}
            </Link>
          );
        },
      },
      {
        id: "email",
        header: "Email",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>
            {row.original.email || "—"}
          </span>
        ),
      },
      {
        id: "type",
        header: "Type",
        cell: ({ row }) => (
          <span className={TABLE_CELL_CLASSNAME}>
            {row.original.companyTypeName || "—"}
          </span>
        ),
      },
      {
        id: "activeCases",
        header: "Active Cases",
        // No case-linkage API exists for companies yet — placeholder until it does.
        cell: () => (
          <span className={TABLE_CELL_CLASSNAME}>
            —
          </span>
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
                    label: "View",
                    icon: Eye,
                    onClick: () => router.push(`/selling/contacts/${c.id}`),
                  },
                  {
                    label: "Reassign",
                    icon: Repeat,
                    disabled: !ra.can("contact:edit"),
                    onClick: () => setReassignTarget(c),
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
    [router, ra],
  );

  const showEmptyState = !loading && totalCount === 0;
  const selectedCompanyTypeName =
    companyTypes.find((t) => t.id === typeFilter)?.name ?? "Company";
  const selectedCompanyTypeNamePlural = /[^aeiou]y$/i.test(
    selectedCompanyTypeName,
  )
    ? `${selectedCompanyTypeName.slice(0, -1)}ies`
    : `${selectedCompanyTypeName}s`;

  const toolbar = (
    <FilterToolbar
      bare
      searchPlaceholder="Search companies by name, email..."
      onSearch={setSearchInput}
      filters={[
        {
          label: "Filter",
          value: typeFilter,
          onChange: setTypeFilter,
          options: companyTypes.map((t) => ({ value: t.id, label: t.name })),
          icon: <Settings2 className="h-4 w-4" />,
        },
      ]}
    >
      <Button
        variant="secondary"
        rightIcon="upload"
        disabled={exportCompaniesMutation.isPending}
        onClick={handleExport}
      >
        {exportCompaniesMutation.isPending ? "Exporting..." : "Export"}
      </Button>
      {ra.can("contact:create") && (
        <Button
          variant="primary"
          rightIcon="plus"
          onClick={() => setShowCreate(true)}
        >
          Add Company
        </Button>
      )}
    </FilterToolbar>
  );

  return (
    <>
      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        {toolbar}
        {showEmptyState ? (
          <ContactsEmptyState
            icon={Building2}
            title={`No ${selectedCompanyTypeName} Yet`}
            description={`No ${selectedCompanyTypeNamePlural.toLowerCase()} have been added yet. Add your first ${selectedCompanyTypeName.toLowerCase()} to get started.`}
            actionLabel={
              ra.can("contact:create")
                ? `Add ${selectedCompanyTypeName}`
                : undefined
            }
            onAction={
              ra.can("contact:create") ? () => setShowCreate(true) : undefined
            }
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
              data={companies}
              columns={columns}
              getRowId={(c) => c.id}
              isLoading={loading}
              emptyMessage="No companies found."
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

      {showCreate && (
        <CompanyFormModal
          open={showCreate}
          title="Add Company"
          companyTypeId={typeFilter || companyTypes[0]?.id}
          onClose={() => setShowCreate(false)}
          onSaved={(company) => {
            setShowCreate(false);
            setNewCompany(company);
          }}
        />
      )}

      {newCompany && (
        <ConfirmDialog
          open
          onClose={() => setNewCompany(null)}
          onConfirm={() => {
            setContactPersonTarget(newCompany);
            setNewCompany(null);
          }}
          icon={
            <div className="w-10 h-10 rounded-lg bg-gray-100 flex items-center justify-center shrink-0">
              <Building2 className="w-5 h-5 text-gray-700" />
            </div>
          }
          title="New Company Added"
          description="The new company has been added. Would you like to add contact person associated with this company? You can always do this later."
          confirmLabel="Add Contact Person"
          cancelLabel="Maybe Later"
        />
      )}

      {contactPersonTarget && (
        <ContactPersonFormModal
          open
          title="Add Contact Person"
          companyId={contactPersonTarget.id}
          companyName={contactPersonTarget.name}
          companyTypeId={contactPersonTarget.companyTypeId}
          onClose={() => setContactPersonTarget(null)}
          onSaved={() => {
            setContactAddedFor(contactPersonTarget);
            setContactPersonTarget(null);
          }}
        />
      )}

      {contactAddedFor && (
        <ConfirmDialog
          open
          onClose={() => setContactAddedFor(null)}
          onConfirm={() => {
            setContactPersonTarget(contactAddedFor);
            setContactAddedFor(null);
          }}
          icon={
            <div className="w-10 h-10 rounded-lg bg-gray-100 flex items-center justify-center shrink-0">
              <UserCheck className="w-5 h-5 text-gray-700" />
            </div>
          }
          title="New Contact Person Added"
          description="The new contact person has been added to the company. You can add another contact person if needed."
          confirmLabel="Add More"
          cancelLabel="Done"
        />
      )}

      {editTarget && (
        <CompanyFormModal
          open
          title="Edit Company"
          companyTypeId={editTarget.companyTypeId}
          editTarget={editTarget}
          onClose={() => setEditTarget(null)}
          onSaved={() => setEditTarget(null)}
        />
      )}

      {reassignTarget && (
        <ReassignCompanyModal
          open
          company={reassignTarget}
          onClose={() => setReassignTarget(null)}
        />
      )}

      {deleteTarget && (
        <ConfirmDialog
          open
          onClose={() => setDeleteTarget(null)}
          onConfirm={handleDelete}
          title="Delete Company"
          description={
            <>
              Are you sure you want to delete{" "}
              <span className="font-semibold text-primary">
                {deleteTarget.name}
              </span>
              ? This action cannot be undone.
            </>
          }
          confirmLabel="Delete"
          confirmVariant="danger"
          loading={deactivateCompanyMutation.isPending}
          warningTitle="Warning: Deleting this company will also remove:"
          warningItems={[
            "All associated contact persons",
            "All case associations",
            "All activity history",
          ]}
        />
      )}
    </>
  );
}
