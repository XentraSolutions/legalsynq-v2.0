import { redirect } from 'next/navigation';
import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { careConnectServerApi } from '@/lib/careconnect-api';
import { ServerApiError } from '@/lib/server-api-client';
import { ProviderCard } from '@/components/careconnect/provider-card';
import { ProviderSearchFilters } from '@/components/careconnect/provider-search-filters';

interface ProvidersPageProps {
  searchParams: {
    name?:               string;
    city?:               string;
    state?:              string;
    categoryCode?:       string;
    acceptingReferrals?: string;
    page?:               string;
  };
}

/**
 * /careconnect/providers — Provider search.
 *
 * Access: CARECONNECT_REFERRER only.
 *   - Provider users (CARECONNECT_RECEIVER) do not need to discover providers.
 *   - The nav builder already hides "Find Providers" from non-referrers.
 *   - This page hard-redirects as a defence-in-depth measure.
 *
 * Rendering: Server Component — data fetched directly via serverApi.
 * Filters: rendered as a Client Component (ProviderSearchFilters) that
 *   writes to URL params, triggering a server re-render with fresh data.
 */
export default async function ProvidersPage({ searchParams }: ProvidersPageProps) {
  const session = await requireOrg();

  if (!session.productRoles.includes(ProductRole.CareConnectReferrer)) {
    redirect('/dashboard');
  }

  const page = Math.max(1, parseInt(searchParams.page ?? '1') || 1);

  let result = null;
  let fetchError: string | null = null;

  try {
    result = await careConnectServerApi.providers.search({
      name:               searchParams.name               || undefined,
      city:               searchParams.city               || undefined,
      state:              searchParams.state              || undefined,
      categoryCode:       searchParams.categoryCode       || undefined,
      acceptingReferrals: searchParams.acceptingReferrals === 'true' ? true : undefined,
      isActive:           true,
      page,
      pageSize:           20,
    });
  } catch (err) {
    fetchError = err instanceof ServerApiError
      ? (err.isNotFound ? 'No providers found.' : err.message)
      : 'Failed to load providers.';
  }

  const hasFilters = !!(
    searchParams.name ||
    searchParams.city ||
    searchParams.state ||
    searchParams.categoryCode ||
    searchParams.acceptingReferrals
  );

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-gray-900">Find Providers</h1>
        {result && (
          <span className="text-sm text-gray-400">
            {result.totalCount.toLocaleString()} {result.totalCount === 1 ? 'result' : 'results'}
          </span>
        )}
      </div>

      {/* Filters (client component) */}
      <ProviderSearchFilters />

      {/* Error */}
      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          {fetchError}
        </div>
      )}

      {/* Results */}
      {result && (
        <>
          {result.items.length === 0 ? (
            <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
              <p className="text-sm text-gray-400">
                {hasFilters
                  ? 'No providers match your filters. Try adjusting your search.'
                  : 'No providers available.'}
              </p>
            </div>
          ) : (
            <div className="grid gap-3">
              {result.items.map(provider => (
                <ProviderCard key={provider.id} provider={provider} />
              ))}
            </div>
          )}

          {/* Pagination */}
          {result.totalCount > 20 && (
            <div className="flex items-center justify-between">
              <p className="text-xs text-gray-400">
                Page {page} of {Math.ceil(result.totalCount / 20)}
              </p>
              <div className="flex items-center gap-3">
                {page > 1 && (
                  <a
                    href={`?${new URLSearchParams({ ...searchParams, page: String(page - 1) })}`}
                    className="text-sm text-primary hover:underline"
                  >
                    ← Previous
                  </a>
                )}
                {page * 20 < result.totalCount && (
                  <a
                    href={`?${new URLSearchParams({ ...searchParams, page: String(page + 1) })}`}
                    className="text-sm text-primary hover:underline"
                  >
                    Next →
                  </a>
                )}
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
