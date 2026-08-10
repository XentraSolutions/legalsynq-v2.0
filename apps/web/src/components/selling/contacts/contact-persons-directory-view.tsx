"use client";

import { FilterToolbar } from "@/components/lien/filter-toolbar";
import { ContactsEmptyState } from "@/components/selling/contacts/contacts-empty-state";

// The backend has no cross-company contact person directory endpoint yet
// (contact persons are only listable scoped to a single company's
// /companies/{id}/contacts route). This view renders the designed shell —
// search/filter toolbar plus the empty state — until that API exists.
export function ContactPersonsDirectoryView() {
  return (
    <>
      <FilterToolbar
        searchPlaceholder="Search contact persons by name, email..."
        filters={[{ label: "All Types", value: "", onChange: () => {}, options: [] }]}
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
