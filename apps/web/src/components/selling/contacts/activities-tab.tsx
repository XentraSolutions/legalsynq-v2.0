"use client";

import { ContactsEmptyState } from "./contacts-empty-state";

// Companies have no activity feed endpoint yet — this renders the designed
// empty shell until a company-scoped activities API exists.
export function CompanyActivitiesTab() {
  return (
    <div className="bg-white border border-gray-200 rounded-xl">
      <ContactsEmptyState
        icon="ri-clipboard-line"
        title="No Recent Activities"
        description="There are no recent activities to display. New updates will appear here as they occur."
      />
    </div>
  );
}
