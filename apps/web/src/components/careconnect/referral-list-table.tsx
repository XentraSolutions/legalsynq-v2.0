import Link from 'next/link';
import type { ReferralSummary } from '@/types/careconnect';
import { StatusBadge, UrgencyBadge } from './status-badge';
import { ReferralQuickActions } from './referral-quick-actions';

interface ReferralListTableProps {
  referrals:  ReferralSummary[];
  totalCount: number;
  page:       number;
  pageSize:   number;
  isReferrer: boolean;
  isReceiver: boolean;
  currentQs?: string;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short',
    day:   'numeric',
    year:  'numeric',
  });
}

/** Display label for status — "New" maps to "Pending" in the UI for clarity */
function statusLabel(status: string): string {
  return status === 'New' ? 'Pending' : status;
}

function rowHighlight(status: string): string {
  if (status === 'New') return 'bg-blue-50/40 hover:bg-blue-50 border-l-4 border-l-blue-400';
  if (status === 'Accepted') return 'hover:bg-gray-50 border-l-4 border-l-teal-400';
  return 'hover:bg-gray-50 border-l-4 border-l-transparent';
}

export function ReferralListTable({
  referrals,
  totalCount,
  page,
  pageSize,
  isReferrer,
  isReceiver,
  currentQs = '',
}: ReferralListTableProps) {
  if (referrals.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-12 text-center">
        <p className="text-sm font-medium text-gray-500">No referrals match your filters.</p>
        <p className="text-xs text-gray-400 mt-1">Try clearing your search or selecting a different status.</p>
      </div>
    );
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-100">
          <thead>
            <tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Client</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Provider</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide hidden sm:table-cell">Service</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide hidden md:table-cell">Urgency</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide hidden lg:table-cell">Created</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {referrals.map(r => (
              <tr key={r.id} className={`transition-colors ${rowHighlight(r.status)}`}>
                {/* Client */}
                <td className="px-4 py-3">
                  <p className="text-sm font-medium text-gray-900 truncate max-w-[160px]">
                    {r.clientFirstName} {r.clientLastName}
                  </p>
                  {r.caseNumber && (
                    <p className="text-xs text-gray-400 mt-0.5">#{r.caseNumber}</p>
                  )}
                </td>

                {/* Provider */}
                <td className="px-4 py-3">
                  <p className="text-sm text-gray-700 truncate max-w-[160px]">{r.providerName}</p>
                </td>

                {/* Service */}
                <td className="px-4 py-3 text-sm text-gray-600 hidden sm:table-cell">
                  <span className="truncate block max-w-[140px]">{r.requestedService}</span>
                </td>

                {/* Urgency */}
                <td className="px-4 py-3 hidden md:table-cell">
                  <UrgencyBadge urgency={r.urgency} />
                </td>

                {/* Status */}
                <td className="px-4 py-3">
                  <StatusBadge status={r.status} />
                  {r.status === 'New' && (
                    <p className="text-[10px] text-blue-500 font-medium mt-0.5 leading-none">Pending</p>
                  )}
                </td>

                {/* Created */}
                <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap hidden lg:table-cell">
                  {formatDate(r.createdAtUtc)}
                </td>

                {/* Quick actions */}
                <td className="px-4 py-3">
                  <ReferralQuickActions
                    referral={r}
                    isReferrer={isReferrer}
                    isReceiver={isReceiver}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Footer / pagination */}
      {totalCount > 0 && (
        <div className="px-4 py-3 border-t border-gray-100 flex items-center justify-between">
          <p className="text-xs text-gray-400">
            Showing {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, totalCount)} of {totalCount}
          </p>
          <div className="flex items-center gap-2">
            {page > 1 && (
              <Link
                href={currentQs ? `?${currentQs}&page=${page - 1}` : `?page=${page - 1}`}
                className="text-xs text-primary hover:underline"
              >
                ← Previous
              </Link>
            )}
            {page * pageSize < totalCount && (
              <Link
                href={currentQs ? `?${currentQs}&page=${page + 1}` : `?page=${page + 1}`}
                className="text-xs text-primary hover:underline"
              >
                Next →
              </Link>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
