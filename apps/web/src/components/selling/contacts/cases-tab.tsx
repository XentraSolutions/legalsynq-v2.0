"use client";

import { CompanyStatsCards } from "./company-stats-cards";
import { ContactsEmptyState } from "./contacts-empty-state";

// Companies have no cases endpoint yet — this renders the designed shell
// (stats + empty state) until a company-scoped cases API exists.
export function CompanyCasesTab() {
  return (
    <div className="space-y-5">
      <CompanyStatsCards />

      <div className="bg-white border border-gray-200 rounded-xl">
        <ContactsEmptyState
          icon="ri-briefcase-line"
          title="No Cases Yet"
          description="No cases are available. Cases associated with this company will be displayed here."
        />
      </div>
    </div>
  );
}
