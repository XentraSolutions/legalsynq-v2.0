"use client";

import { Briefcase } from "lucide-react";
import { useCompanyDetailContext } from "./context";
import { ContactsEmptyState } from "./contacts-empty-state";
import { CaseStatusChip } from "@/components/lien/case-status-chip";
import { Pagination } from "@/components/ui/pagination";
import type { CompanyRecentCase } from "@/lib/selling/companies.types";

const PRIMARY_BUTTON_CLASSNAME = "border-[#EE7132] bg-[#EE7132] text-white";

function Field({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs font-medium text-gray-500 tracking-wide">{label}</dt>
      <dd className="mt-1 text-sm text-gray-900">{value ?? "—"}</dd>
    </div>
  );
}

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
    minimumFractionDigits: 2,
  }).format(amount);
}

function RecentCaseRow({ item }: { item: CompanyRecentCase }) {
  return (
    <div className="flex items-center justify-between px-6 py-4">
      <div>
        <div className="flex items-center gap-2">
          <span className="text-sm font-medium text-gray-900">{item.clientName}</span>
          <CaseStatusChip status={item.status} label={item.statusLabel} />
        </div>
        <p className="text-xs text-gray-400 mt-1">Case ID: {item.caseNumber}</p>
      </div>
      <span className="text-sm font-semibold text-gray-900">
        {formatCurrency(item.billingAmount)}
      </span>
    </div>
  );
}

export function CompanyOverviewTab() {
  const { company, detailsQuery, recentCasesPage: page, setRecentCasesPage: setPage } =
    useCompanyDetailContext();

  const recentCases = detailsQuery.data?.recentCases;
  const items = recentCases?.items ?? [];
  const totalPages = recentCases
    ? Math.max(1, Math.ceil(recentCases.totalCount / recentCases.pageSize))
    : 1;

  return (
    <div className="space-y-5">
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

        <div className="bg-white border border-gray-200 rounded-xl flex flex-col">
          <h2 className="text-sm font-semibold text-gray-700 px-6 pt-5 pb-4">Recent Cases</h2>
          {detailsQuery.isLoading ? (
            <div className="px-6 pb-5 space-y-4 animate-pulse">
              {Array.from({ length: 3 }).map((_, i) => (
                <div key={i} className="h-12 bg-gray-100 rounded-lg" />
              ))}
            </div>
          ) : items.length === 0 ? (
            <ContactsEmptyState
              icon={Briefcase}
              title="No Cases Yet"
              description="Cases associated with this company will be displayed here."
            />
          ) : (
            <>
              <div className="flex-1 divide-y divide-gray-100">
                {items.map((item) => (
                  <RecentCaseRow key={item.id} item={item} />
                ))}
              </div>
              {totalPages > 1 && (
                <div className="flex justify-end px-6 py-4 mt-auto">
                  <Pagination
                    page={page}
                    totalPages={totalPages}
                    onPageChange={setPage}
                    primaryButtonClassName={PRIMARY_BUTTON_CLASSNAME}
                  />
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}
