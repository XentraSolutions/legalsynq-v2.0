"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";
import { Settings2 } from "lucide-react";
import { FilterToolbar } from "@/components/lien/filter-toolbar";
import { ContactsEmptyState } from "@/components/selling/contacts/contacts-empty-state";
import { ContactsFilterModal } from "@/components/selling/contacts/contacts-filter-modal";
import { Button } from "@/components/ui/button";
import { downloadBlob } from "@/lib/utils";
import {
  useCompanyTypes,
  useContactPersonTypes,
  useExportContacts,
} from "@/hooks/use-selling-companies";

// The backend has no cross-company contact person directory endpoint yet
// (contact persons are only listable scoped to a single company's
// /companies/{id}/contacts route). This view renders the designed shell —
// search/filter toolbar plus the empty state — until that API exists.
// Export is already backed by a real endpoint, so it's wired up ahead of the list.
export function ContactPersonsDirectoryView() {
  const [search, setSearch] = useState("");
  const [searchInput, setSearchInput] = useState("");
  const [companyTypeFilter, setCompanyTypeFilter] = useState("");
  const [contactPersonTypeFilter, setContactPersonTypeFilter] = useState("");
  const [showFilter, setShowFilter] = useState(false);
  const activeFilterCount =
    (companyTypeFilter ? 1 : 0) + (contactPersonTypeFilter ? 1 : 0);

  useEffect(() => {
    const timeout = setTimeout(() => setSearch(searchInput), 300);
    return () => clearTimeout(timeout);
  }, [searchInput]);

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

  return (
    <>
      <FilterToolbar
        searchPlaceholder="Search contact persons by name, email..."
        onSearch={setSearchInput}
      >
        <Button
          variant="secondary"
          className="border-gray-300"
          leftIcon={<Settings2 className="h-4 w-4" />}
          onClick={() => setShowFilter(true)}
        >
          Filter
          {activeFilterCount > 0 && (
            <span className="ml-0.5 inline-flex items-center justify-center h-4 min-w-4 px-1 rounded-full bg-primary text-white text-[10px] font-semibold">
              {activeFilterCount}
            </span>
          )}
        </Button>
        <Button
          variant="secondary"
          iconDivider
          rightIcon={<i className="ri-upload-2-line text-base" />}
          disabled={exportContactsMutation.isPending}
          onClick={handleExport}
        >
          {exportContactsMutation.isPending ? "Exporting..." : "Export"}
        </Button>
      </FilterToolbar>

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

      <div className="bg-white border border-gray-200 rounded-xl">
        <ContactsEmptyState
          icon="ri-contacts-line"
          title="No Contact Person Yet"
          description="A cross-company contact person directory isn't available yet — add contact persons from within a company's detail page for now."
        />
      </div>
    </>
  );
}
