"use client";

import { useCompanyDetailContext } from "./context";

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
    <div className="bg-white border border-gray-200 rounded-xl px-6 py-5">
      <h2 className="text-sm font-semibold text-gray-700 mb-4">Company Information</h2>
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-x-6 gap-y-5">
        <Field label="Name" value={company.name} />
        <Field label="Type" value={company.companyTypeName} />
        <Field label="Status" value={company.isActive ? "Active" : "Inactive"} />
        <Field label="Email" value={company.email} />
        <Field label="Phone" value={company.phone} />
        <Field label="Address" value={company.addressLine1} />
        <Field label="City" value={company.city} />
        <Field label="State" value={company.state} />
        <Field label="Zip Code" value={company.postalCode} />
      </div>
    </div>
  );
}
