"use client";

import { useCompanyDetailContext } from "./context";
import { CompanyStatsCards } from "./company-stats-cards";
import { ContactsEmptyState } from "./contacts-empty-state";

function Field({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs font-medium text-gray-500 tracking-wide">{label}</dt>
      <dd className="mt-1 text-sm text-gray-900">{value ?? "—"}</dd>
    </div>
  );
}

export function CompanyOverviewTab() {
  const { company } = useCompanyDetailContext();

  return (
    <div className="space-y-5">
      <CompanyStatsCards />

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <div className="bg-white border border-gray-200 rounded-xl px-6 py-5">
          <h2 className="text-sm font-semibold text-gray-700 mb-4">Company Information</h2>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-5">
            <Field label="Company Type" value={company.companyTypeName} />
            <Field label="Company Name" value={company.name} />
            <Field label="Email Address" value={company.email} />
            <Field label="Phone Number" value={company.phone} />
            <Field label="Address" value={company.addressLine1} />
            <Field label="City" value={company.city} />
            <Field label="State" value={company.state} />
            <Field label="ZIP Code" value={company.postalCode} />
          </div>
        </div>

        <div className="bg-white border border-gray-200 rounded-xl">
          <h2 className="text-sm font-semibold text-gray-700 px-6 pt-5">Recent Cases</h2>
          {/* No company-scoped cases API yet — see CompanyCasesTab. */}
          <ContactsEmptyState
            icon="ri-briefcase-line"
            title="No Cases Yet"
            description="Cases associated with this company will be displayed here."
          />
        </div>
      </div>
    </div>
  );
}
