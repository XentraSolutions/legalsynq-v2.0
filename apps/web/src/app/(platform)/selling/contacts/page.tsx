"use client";

import { useState, useEffect, useMemo } from "react";
import Link from "next/link";
import { toast } from "sonner";
import { Building2 } from "lucide-react";
import { PageHeader } from "@/components/lien/page-header";
import { FilterToolbar } from "@/components/lien/filter-toolbar";
import { ActionMenu } from "@/components/lien/action-menu";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { SideDrawer } from "@/components/lien/side-drawer";
import { CompanyFormModal } from "@/components/selling/forms/company-form-modal";
import { ContactPersonFormModal } from "@/components/selling/forms/contact-person-form-modal";
import { ContactsEmptyState } from "@/components/selling/contacts/contacts-empty-state";
import { ContactPersonsDirectoryView } from "@/components/selling/contacts/contact-persons-directory-view";
import { useRoleAccess } from "@/hooks/use-role-access";
import {
  useCompanies,
  useCompanyTypes,
  useDeactivateCompany,
  useReactivateCompany,
} from "@/hooks/use-selling-companies";
import { ConfirmDialog } from "@/components/selling/modal";
import { useRouter, useSearchParams } from "next/navigation";
import type { ColumnDef } from "@tanstack/react-table";
import { BaseTable } from "@/components/ui/base-table";
import { Button } from "@/components/ui/button";
import type { Company } from "@/lib/selling/companies.types";

const DEFAULT_PAGE_SIZE = 10;
const PAGE_SIZE_OPTIONS = [10, 25, 50, 100];

// Selling's brand accent — used on every primary action button on this page
// and everything opened from it, instead of the tenant bg-primary blue used
// on the lien side.
const PRIMARY_BUTTON_CLASSNAME = "bg-[#EE7132] hover:bg-[#EE7132]/90 text-white";

const VIEWS = [
  { key: "companies", label: "Companies" },
  { key: "contacts", label: "Contact Person" },
] as const;

export const dynamic = "force-dynamic";

export default function SellingContactsPage() {
  const searchParams = useSearchParams();
  const view = searchParams.get("view") === "contacts" ? "contacts" : "companies";

  return (
    <div className="space-y-5">
      <PageHeader
        title="Contacts"
        subtitle="Keep your company and contact person records organized and easily accessible."
      />

      <div className="flex flex-wrap gap-1 bg-[#FAFAFA] rounded-md p-1">
        {VIEWS.map((tab) => (
          <Link
            key={tab.key}
            href={tab.key === "companies" ? "/selling/contacts" : `/selling/contacts?view=${tab.key}`}
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

      {view === "companies" ? <CompaniesListView /> : <ContactPersonsDirectoryView />}
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
  const [contactPersonTarget, setContactPersonTarget] = useState<Company | null>(null);
  const [previewId, setPreviewId] = useState<string | null>(null);
  const [confirmAction, setConfirmAction] = useState<{
    action: "deactivate" | "reactivate";
    company: Company;
  } | null>(null);

  const deactivateCompanyMutation = useDeactivateCompany();
  const reactivateCompanyMutation = useReactivateCompany();

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

  const previewCompany = previewId
    ? companies.find((c) => c.id === previewId)
    : null;

  const runDeactivate = (company: Company) => {
    const toastId = toast.loading(`Deactivating ${company.name}...`);
    deactivateCompanyMutation.mutate(company.id, {
      onSuccess: () => {
        toast.success("Company deactivated", { id: toastId, description: company.name });
      },
      onError: (err) => {
        toast.error("Couldn't deactivate company", {
          id: toastId,
          description: err instanceof Error ? err.message : company.name,
        });
      },
    });
  };

  const runReactivate = (company: Company) => {
    const toastId = toast.loading(`Reactivating ${company.name}...`);
    reactivateCompanyMutation.mutate(company.id, {
      onSuccess: () => {
        toast.success("Company reactivated", { id: toastId, description: company.name });
      },
      onError: (err) => {
        toast.error("Couldn't reactivate company", {
          id: toastId,
          description: err instanceof Error ? err.message : company.name,
        });
      },
    });
  };

  const handleConfirmAction = () => {
    if (!confirmAction) return;
    const { action, company } = confirmAction;
    setConfirmAction(null);
    if (action === "deactivate") runDeactivate(company);
    else runReactivate(company);
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
              className="text-sm font-medium text-gray-700 hover:text-primary"
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
          <span className="text-sm text-gray-500">{row.original.email || "—"}</span>
        ),
      },
      {
        id: "type",
        header: "Type",
        cell: ({ row }) => (
          <span className="text-sm text-gray-500">{row.original.companyTypeName || "—"}</span>
        ),
      },
      {
        id: "activeCases",
        header: "Active Cases",
        // No case-linkage API exists for companies yet — placeholder until it does.
        cell: () => <span className="text-sm text-gray-400">—</span>,
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
                    label: "View Details",
                    icon: "ri-eye-line",
                    onClick: () => router.push(`/selling/contacts/${c.id}`),
                  },
                  c.isActive
                    ? {
                        label: "Deactivate",
                        icon: "ri-forbid-line",
                        onClick: () => setConfirmAction({ action: "deactivate", company: c }),
                      }
                    : {
                        label: "Reactivate",
                        icon: "ri-check-line",
                        onClick: () => setConfirmAction({ action: "reactivate", company: c }),
                      },
                ]}
              />
            </div>
          );
        },
      },
    ],
    [router],
  );

  const hasActiveFilters = Boolean(search) || Boolean(typeFilter);
  const showEmptyState = !loading && totalCount === 0 && !hasActiveFilters;

  const toolbar = (
    <FilterToolbar
      bare
      searchPlaceholder="Search companies by name, email..."
      onSearch={setSearchInput}
      filters={[
        {
          label: "All Types",
          value: typeFilter,
          onChange: setTypeFilter,
          options: companyTypes.map((t) => ({ value: t.id, label: t.name })),
        },
      ]}
    >
      <Button
        variant="secondary"
        iconDivider
        rightIcon={<i className="ri-upload-2-line text-base" />}
        onClick={() =>
          toast.info("Coming Soon", {
            description: "Exporting companies isn't available yet.",
          })
        }
      >
        Export
      </Button>
      {ra.can("contact:create") && (
        <Button
          iconDivider
          rightIcon={<i className="ri-add-line text-base" />}
          className={PRIMARY_BUTTON_CLASSNAME}
          onClick={() => setShowCreate(true)}
        >
          Add Company
        </Button>
      )}
    </FilterToolbar>
  );

  return (
    <>
      {showEmptyState ? (
        <div className="bg-white border border-gray-200 rounded-xl">
          <ContactsEmptyState
            icon="ri-building-4-line"
            title="No Company Yet"
            description="No companies have been added yet. Add your first company to get started."
            actionLabel={ra.can("contact:create") ? "Add Company" : undefined}
            onAction={ra.can("contact:create") ? () => setShowCreate(true) : undefined}
          />
        </div>
      ) : (
        <div className="relative">
          {refreshing && (
            <div className="absolute top-2 right-3 z-10 flex items-center gap-1.5 text-xs text-gray-400">
              <i className="ri-loader-4-line animate-spin text-sm" />
              Refreshing...
            </div>
          )}
          <BaseTable
            data={companies}
            columns={columns}
            getRowId={(c) => c.id}
            isLoading={loading}
            emptyMessage="No companies found."
            onRowClick={(c) => setPreviewId(c.id)}
            toolbar={toolbar}
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
            className="bg-white border-gray-200 rounded-xl"
            primaryButtonClassName={PRIMARY_BUTTON_CLASSNAME}
          />
        </div>
      )}

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
          primaryButtonClassName={PRIMARY_BUTTON_CLASSNAME}
        />
      )}

      {contactPersonTarget && (
        <ContactPersonFormModal
          open
          title="Add Contact Person"
          companyId={contactPersonTarget.id}
          companyName={contactPersonTarget.name}
          companyTypeId={contactPersonTarget.companyTypeId}
          allowCompanySelect
          onClose={() => setContactPersonTarget(null)}
          onSaved={() => setContactPersonTarget(null)}
        />
      )}

      {confirmAction && (
        <ConfirmDialog
          open
          onClose={() => setConfirmAction(null)}
          onConfirm={handleConfirmAction}
          title={confirmAction.action === "deactivate" ? "Deactivate Company" : "Reactivate Company"}
          description={
            <>
              Are you sure you want to{" "}
              {confirmAction.action === "deactivate" ? "deactivate" : "reactivate"}{" "}
              <span className="font-semibold text-primary">{confirmAction.company.name}</span>?
            </>
          }
          confirmLabel={confirmAction.action === "deactivate" ? "Deactivate" : "Reactivate"}
          confirmVariant={confirmAction.action === "deactivate" ? "danger" : "primary"}
          loading={deactivateCompanyMutation.isPending || reactivateCompanyMutation.isPending}
        />
      )}

      <SideDrawer
        open={!!previewCompany}
        onClose={() => setPreviewId(null)}
        title={previewCompany?.name || ""}
        subtitle={previewCompany?.companyTypeName}
      >
        {previewCompany && (
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-3 text-sm">
              <div>
                <p className="text-xs text-gray-400">Email</p>
                <p className="text-gray-700">{previewCompany.email || "—"}</p>
              </div>
              <div>
                <p className="text-xs text-gray-400">Phone</p>
                <p className="text-gray-700">{previewCompany.phone || "—"}</p>
              </div>
              <div>
                <p className="text-xs text-gray-400">Location</p>
                <p className="text-gray-700">
                  {previewCompany.city}
                  {previewCompany.city && previewCompany.state ? ", " : ""}
                  {previewCompany.state || "—"}
                </p>
              </div>
              <div>
                <p className="text-xs text-gray-400">Status</p>
                <p className="text-gray-700">{previewCompany.isActive ? "Active" : "Inactive"}</p>
              </div>
            </div>
            <Link
              href={`/selling/contacts/${previewCompany.id}`}
              className={`block text-center text-sm px-4 py-2 rounded-lg ${PRIMARY_BUTTON_CLASSNAME}`}
            >
              View Full Details
            </Link>
          </div>
        )}
      </SideDrawer>
    </>
  );
}
