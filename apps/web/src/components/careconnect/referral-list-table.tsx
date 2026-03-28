import Link from 'next/link';
import type { ReferralSummary } from '@/types/careconnect';
import { StatusBadge, UrgencyBadge } from './status-badge';

interface ReferralListTableProps {
  referrals:  ReferralSummary[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short',
    day:   'numeric',
    year:  'numeric',
  });
}

export function ReferralListTable({ referrals, totalCount, page, pageSize }: ReferralListTableProps) {
  if (referrals.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
        <p className="text-sm text-gray-400">No referrals found.</p>
      </div>
    );
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      {/* Table */}
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-100">
          <thead>
            <tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Client</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Provider</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Service</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Urgency</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Created</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {referrals.map(r => (
              <tr key={r.id} className="hover:bg-gray-50 transition-colors">
                <td className="px-4 py-3">
                  <p className="text-sm font-medium text-gray-900">
                    {r.clientFirstName} {r.clientLastName}
                  </p>
                  {r.caseNumber && (
                    <p className="text-xs text-gray-400 mt-0.5">#{r.caseNumber}</p>
                  )}
                </td>
                <td className="px-4 py-3 text-sm text-gray-700">{r.providerName}</td>
                <td className="px-4 py-3 text-sm text-gray-700">{r.requestedService}</td>
                <td className="px-4 py-3">
                  <UrgencyBadge urgency={r.urgency} />
                </td>
                <td className="px-4 py-3">
                  <StatusBadge status={r.status} />
                </td>
                <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap">
                  {formatDate(r.createdAtUtc)}
                </td>
                <td className="px-4 py-3 text-right">
                  <Link
                    href={`/careconnect/referrals/${r.id}`}
                    className="text-xs text-primary font-medium hover:underline whitespace-nowrap"
                  >
                    View →
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Footer */}
      <div className="px-4 py-3 border-t border-gray-100 flex items-center justify-between">
        <p className="text-xs text-gray-400">
          Showing {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, totalCount)} of {totalCount}
        </p>
        <div className="flex items-center gap-2">
          {page > 1 && (
            <Link
              href={`?page=${page - 1}`}
              className="text-xs text-primary hover:underline"
            >
              ← Previous
            </Link>
          )}
          {page * pageSize < totalCount && (
            <Link
              href={`?page=${page + 1}`}
              className="text-xs text-primary hover:underline"
            >
              Next →
            </Link>
          )}
        </div>
      </div>
    </div>
  );
}
