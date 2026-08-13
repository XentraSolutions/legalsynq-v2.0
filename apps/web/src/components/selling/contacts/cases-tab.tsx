"use client";

import { ContactsEmptyState } from "./contacts-empty-state";

// Companies have no cases endpoint yet — this renders the designed shell
// (empty state) until a company-scoped cases API exists. Stats cards are
// rendered once by CompanyDetailShell and shared across all tabs.
export function CompanyCasesTab() {
  return (
    <div className="bg-white border border-gray-200 rounded-xl">
      <ContactsEmptyState
        icon="ri-briefcase-line"
        title="No Cases Yet"
        description="No cases are available. Cases associated with this company will be displayed here."
      />
    </div>
  );
}
