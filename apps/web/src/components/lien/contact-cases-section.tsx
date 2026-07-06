'use client';

import { useState, useEffect, useCallback, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { contactsService } from '@/lib/contacts';
import type { ContactCaseSummary } from '@/lib/contacts/contacts.types';
import { StatusBadge } from '@/components/lien/status-badge';
import { ActionMenu } from '@/components/lien/action-menu';
import { Pagination } from '@/components/ui/pagination';

// Contact types with a known case-lookup API. Any other type (e.g.
// LienHolder, CaseManager, InternalUser) has no equivalent endpoint, so the
// section is hidden rather than showing an empty/broken table.
const SUPPORTED_CONTACT_TYPES = [
  'LawFirm',
  'Lead',
  'MedicalFacility',
  'Provider',
  'FundingCompany',
];

// Provider/facility/funding cases are surfaced via their liens, so the table
// also shows lien id + billing/purchase amount. Law firm and lead cases are
// shown at the case level only.
const LIEN_DETAIL_CONTACT_TYPES = ['MedicalFacility', 'Provider', 'FundingCompany'];

const PAGE_SIZE = 10;

const currency = (value: number | null) =>
  value != null ? new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(value) : '—';

interface Props {
  contactId: string;
  contactType: string;
}

export function ContactCasesSection({ contactId, contactType }: Props) {
  const router = useRouter();
  const [cases, setCases] = useState<ContactCaseSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const supported = SUPPORTED_CONTACT_TYPES.includes(contactType);
  const showLienColumns = LIEN_DETAIL_CONTACT_TYPES.includes(contactType);

  const fetchCases = useCallback(async () => {
    try {
      setLoading(true);
      const result = await contactsService.getCasesByContact(contactId, contactType);
      setCases(result);
    } catch {
      setCases([]);
    } finally {
      setLoading(false);
    }
  }, [contactId, contactType]);

  useEffect(() => {
    if (supported) fetchCases();
  }, [fetchCases, supported]);

  useEffect(() => { setPage(1); }, [cases]);

  const totalPages = Math.max(Math.ceil(cases.length / PAGE_SIZE), 1);
  const pageItems = useMemo(
    () => cases.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE),
    [cases, page],
  );

  if (!supported) return null;

  return (
    <div className="bg-white border border-gray-200 rounded-xl">
      <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
        <div className="flex items-center gap-2">
          <i className="ri-folder-2-line text-gray-500" />
          <h3 className="text-sm font-semibold text-gray-800">Cases</h3>
          {!loading && (
            <span className="text-xs text-gray-400 bg-gray-100 rounded-full px-2 py-0.5">{cases.length}</span>
          )}
        </div>
      </div>

      <div className="p-5">
        {loading ? (
          <div className="text-center py-10 text-sm text-gray-400">Loading cases...</div>
        ) : cases.length === 0 ? (
          <div className="text-center py-10 text-sm text-gray-400 italic">No Case Found</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-gray-50/80 border-b border-gray-100">
                  <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Case ID</th>
                  {showLienColumns && (
                    <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Lien ID</th>
                  )}
                  <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Full Name</th>
                  {showLienColumns && (
                    <>
                      <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Billing Amount</th>
                      <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Purchase Amount</th>
                    </>
                  )}
                  <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Accident Type</th>
                  <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Date of Loss</th>
                  <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Date of Birth</th>
                  <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
                  <th className="text-right px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {pageItems.map((c) => (
                  <tr key={c.id} className="hover:bg-gray-50/50">
                    <td className="px-3 py-3 font-medium text-gray-900">{c.caseNumber}</td>
                    {showLienColumns && (
                      <td className="px-3 py-3 text-gray-600">{c.lienId ?? '—'}</td>
                    )}
                    <td className="px-3 py-3 text-gray-700">{c.personName}</td>
                    {showLienColumns && (
                      <>
                        <td className="px-3 py-3 text-gray-900">{currency(c.billingAmount)}</td>
                        <td className="px-3 py-3 text-gray-900">{currency(c.purchaseAmount)}</td>
                      </>
                    )}
                    <td className="px-3 py-3 text-gray-600">{c.accidentType ?? '—'}</td>
                    <td className="px-3 py-3 text-xs text-gray-500 tabular-nums">{c.dateOfLoss ?? '—'}</td>
                    <td className="px-3 py-3 text-xs text-gray-500 tabular-nums">{c.dateOfBirth ?? '—'}</td>
                    <td className="px-3 py-3">
                      <StatusBadge status={c.status} />
                    </td>
                    <td className="px-3 py-3 text-right">
                      <ActionMenu
                        items={[
                          {
                            label: 'View Case',
                            icon: 'ri-eye-line',
                            onClick: () => router.push(`/lien/cases/${c.id}`),
                          },
                        ]}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            {totalPages > 1 && (
              <div className="flex justify-end pt-4">
                <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
