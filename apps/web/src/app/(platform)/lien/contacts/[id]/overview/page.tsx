"use client";

import Link from "next/link";
import { useContactDetailContext } from "../contact-detail-context";
import { StatusBadge } from "@/components/lien/status-badge";
import { useContactCases } from "@/hooks/use-contact-cases";
import { CASE_REASSIGN_CONFIG } from "@/lib/contacts";
import type { ContactCaseSummary } from "@/lib/contacts/contacts.types";

const RECENT_CASES_LIMIT = 5;
// Bounded sample used only to total billing across a contact's cases —
// there's no server-side aggregate endpoint, so this is computed
// client-side over a capped page.
const BILLING_STATS_SAMPLE_SIZE = 200;

const currency = (value: number) =>
  new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" }).format(value);

export default function ContactOverviewPage() {
  const { contact } = useContactDetailContext();
  const casesSupported = Object.keys(CASE_REASSIGN_CONFIG).includes(contact.contactType);

  const { data: recentData, isLoading: recentLoading } = useContactCases(
    contact.id,
    contact.contactType,
    { page: 1, limit: RECENT_CASES_LIMIT },
    casesSupported,
  );
  const { data: billingData } = useContactCases(
    contact.id,
    contact.contactType,
    { page: 1, limit: BILLING_STATS_SAMPLE_SIZE },
    casesSupported,
  );

  // Lien-level contact types (facility/provider/funding) return one row
  // per lien, so a case with multiple liens would otherwise take up
  // several of the 5 "recent" slots and list itself repeatedly. Each row
  // already carries that case's full totalBilling (summed server-side
  // across all its liens), so grouping just keeps the first row per case.
  const groupByCase = (items: ContactCaseSummary[]) => {
    const byCase = new Map<string, ContactCaseSummary>();
    items.forEach((c) => {
      if (!byCase.has(c.id)) byCase.set(c.id, c);
    });
    return [...byCase.values()];
  };

  const recentCases = groupByCase(recentData?.items ?? []);
  const totalCases = recentData?.totalCount ?? 0;
  const billingCases = groupByCase(billingData?.items ?? []);

  const totalBillingForAllCases = billingCases.reduce((sum, c) => sum + (c.totalBilling ?? 0), 0);

  return (
    <div className="space-y-5">
      {contact.notes && (
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-gray-800 mb-2">Notes</h3>
          <p className="text-sm text-gray-600">{contact.notes}</p>
        </div>
      )}

      {casesSupported && (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
          <div className="lg:col-span-2 bg-white border border-gray-200 rounded-xl">
            <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
              <h3 className="text-sm font-semibold text-gray-800">Recent Cases</h3>
              {totalCases > RECENT_CASES_LIMIT && (
                <Link
                  href={`/lien/contacts/${contact.id}/cases`}
                  className="text-xs font-medium text-primary hover:underline"
                >
                  View All ({totalCases})
                </Link>
              )}
            </div>
            <div className="p-5">
              {recentLoading ? (
                <div className="text-center py-8 text-sm text-gray-400">Loading cases...</div>
              ) : recentCases.length === 0 ? (
                <div className="bg-gray-50/80 rounded-lg text-center py-8 text-sm text-gray-400">
                  No record.
                </div>
              ) : (
                <ul className="space-y-2.5">
                  {recentCases.map((c) => {
                    const amount = c.totalBilling ?? c.billingAmount ?? c.purchaseAmount;
                    return (
                      <li key={c.id}>
                        <Link
                          href={`/lien/cases/${c.id}`}
                          className="flex items-center justify-between gap-3 bg-gray-50/80 hover:bg-gray-100 rounded-lg px-4 py-3 transition-colors"
                        >
                          <div className="min-w-0">
                            <div className="flex items-center gap-2">
                              <p className="text-sm font-medium text-gray-900 truncate">
                                {c.personName}
                              </p>
                              <StatusBadge status={c.status} />
                            </div>
                            <p className="text-xs text-gray-400 mt-1">Case ID: {c.caseNumber}</p>
                          </div>
                          {amount != null && (
                            <p className="text-base font-semibold text-primary shrink-0">
                              {currency(amount)}
                            </p>
                          )}
                        </Link>
                      </li>
                    );
                  })}
                </ul>
              )}
            </div>
          </div>

          <div className="bg-white border border-gray-200 rounded-xl p-5">
            <h3 className="text-sm font-semibold text-gray-800 mb-4">Statistics</h3>
            <div className="space-y-4">
              <div className="border border-gray-100 rounded-lg p-4">
                <p className="text-2xl font-semibold text-blue-600">{totalCases}</p>
                <p className="text-xs text-gray-500 mt-1">Total Cases</p>
              </div>
              <div className="border border-gray-100 rounded-lg p-4">
                <p className="text-2xl font-semibold text-amber-500">{contact.activeCases}</p>
                <p className="text-xs text-gray-500 mt-1">Active Cases</p>
              </div>
              <div className="border border-gray-100 rounded-lg p-4">
                <p className="text-2xl font-semibold text-cyan-600">
                  {currency(totalBillingForAllCases)}
                </p>
                <p className="text-xs text-gray-500 mt-1">Total Billing</p>
              </div>
            </div>
          </div>
        </div>
      )}

      {!contact.notes && !casesSupported && (
        <div className="bg-white border border-gray-200 rounded-xl p-10 text-center text-sm text-gray-400">
          No additional overview details for this contact.
        </div>
      )}
    </div>
  );
}
