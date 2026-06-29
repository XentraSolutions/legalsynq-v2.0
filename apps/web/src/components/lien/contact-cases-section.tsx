'use client';

import { useState, useEffect, useCallback } from 'react';
import { contactsService } from '@/lib/contacts';
import type { ContactCaseSummary } from '@/lib/contacts/contacts.types';
import { StatusBadge } from '@/components/lien/status-badge';

interface Props {
  contactId: string;
}

export function ContactCasesSection({ contactId }: Props) {
  const [cases, setCases] = useState<ContactCaseSummary[]>([]);
  const [loading, setLoading] = useState(true);

  const fetchCases = useCallback(async () => {
    try {
      setLoading(true);
      const result = await contactsService.getCasesByContact(contactId);
      setCases(result);
    } catch {
      setCases([]);
    } finally {
      setLoading(false);
    }
  }, [contactId]);

  useEffect(() => { fetchCases(); }, [fetchCases]);

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
          <div className="text-center py-10 text-sm text-gray-400">No cases associated with this contact.</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-100">
                  <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Case ID</th>
                  <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Person Name</th>
                  <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
                  <th className="text-right px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Billing Amount</th>
                </tr>
              </thead>
              <tbody>
                {cases.map((c) => (
                  <tr key={c.id} className="border-b border-gray-50 hover:bg-gray-50/50">
                    <td className="px-3 py-3 font-medium text-gray-900">{c.caseNumber}</td>
                    <td className="px-3 py-3 text-gray-700">{c.personName}</td>
                    <td className="px-3 py-3">
                      <StatusBadge status={c.status} />
                    </td>
                    <td className="px-3 py-3 text-right text-gray-900 font-medium">
                      {c.billingAmount != null
                        ? new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(c.billingAmount)
                        : '—'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
