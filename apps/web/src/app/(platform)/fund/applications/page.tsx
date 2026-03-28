import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';

/**
 * SynqFund — Applications list.
 * SYNQFUND_REFERRER: sees applications they submitted (law firm).
 * SYNQFUND_FUNDER:   sees applications addressed to their org.
 */
export default async function ApplicationsPage() {
  const session = await requireOrg();

  const isReferrer = session.productRoles.includes(ProductRole.SynqFundReferrer);
  const isFunder   = session.productRoles.includes(ProductRole.SynqFundFunder);

  if (!isReferrer && !isFunder) {
    return (
      <div className="text-sm text-gray-500">
        You do not have access to SynqFund applications.
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-gray-900">Applications</h1>
        {isReferrer && (
          <a
            href="/fund/applications/new"
            className="bg-primary text-white text-sm font-medium px-4 py-2 rounded-md hover:opacity-90 transition-opacity"
          >
            New Application
          </a>
        )}
      </div>

      {/* TODO: Replace with ApplicationList component fetching from apiClient */}
      <div className="bg-white border border-gray-200 rounded-lg p-8 text-center text-sm text-gray-400">
        Application list — connect to{' '}
        <code className="font-mono bg-gray-100 px-1 rounded">
          GET /api/fund/api/applications
        </code>
      </div>
    </div>
  );
}
