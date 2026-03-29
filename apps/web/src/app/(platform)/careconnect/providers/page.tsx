import { redirect } from 'next/navigation';
import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import { ProviderSearchFilters } from '@/components/careconnect/provider-search-filters';
import { ProviderMapShell } from '@/components/careconnect/provider-map-shell';

interface ProvidersPageProps {
  searchParams: {
    name?:               string;
    city?:               string;
    state?:              string;
    categoryCode?:       string;
    acceptingReferrals?: string;
    page?:               string;
    view?:               string;
    lat?:                string;
    lng?:                string;
    radius?:             string;
    nLat?:               string;
    sLat?:               string;
    eLng?:               string;
    wLng?:               string;
  };
}

/**
 * /careconnect/providers — Provider search with list/map toggle.
 *
 * Access: CARECONNECT_REFERRER only.
 *
 * Rendering: Server Component — fetches initial list data and passes it
 * to ProviderMapShell (Client Component) as a prop.
 *   - List mode uses the initial data (no extra round-trip).
 *   - Map mode fetches markers client-side via BFF proxy.
 *
 * URL params:
 *   name, city, state, categoryCode, acceptingReferrals  — text filters
 *   page                                                  — list pagination
 *   view                                                  — "list" | "map"
 *   lat, lng, radius                                      — geolocation (radius search)
 *   nLat, sLat, eLng, wLng                               — viewport bounds (map pan)
 */
export default async function ProvidersPage({ searchParams }: ProvidersPageProps) {
  const session = await requireOrg();

  if (!session.productRoles.includes(ProductRole.CareConnectReferrer)) {
    redirect('/dashboard');
  }

  const page = Math.max(1, parseInt(searchParams.page ?? '1') || 1);

  const lat    = searchParams.lat    ? parseFloat(searchParams.lat)    : undefined;
  const lng    = searchParams.lng    ? parseFloat(searchParams.lng)    : undefined;
  const radius = searchParams.radius ? parseFloat(searchParams.radius) : undefined;
  const nLat   = searchParams.nLat   ? parseFloat(searchParams.nLat)   : undefined;
  const sLat   = searchParams.sLat   ? parseFloat(searchParams.sLat)   : undefined;
  const eLng   = searchParams.eLng   ? parseFloat(searchParams.eLng)   : undefined;
  const wLng   = searchParams.wLng   ? parseFloat(searchParams.wLng)   : undefined;

  const geoParams =
    lat && lng && radius
      ? { latitude: lat, longitude: lng, radiusMiles: radius }
      : nLat && sLat && eLng && wLng
      ? { northLat: nLat, southLat: sLat, eastLng: eLng, westLng: wLng }
      : {};

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
      ...geoParams,
    });
  } catch (err) {
    fetchError =
      err instanceof ServerApiError
        ? err.isNotFound
          ? 'No providers found.'
          : err.message
        : 'Failed to load providers.';
  }

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-gray-900">Find Providers</h1>
        {result && (
          <span className="text-sm text-gray-400">
            {result.totalCount.toLocaleString()}{' '}
            {result.totalCount === 1 ? 'result' : 'results'}
          </span>
        )}
      </div>

      {/* Filters (client component, URL-synced) */}
      <ProviderSearchFilters />

      {/* Shell — handles list/map toggle + rendering */}
      <ProviderMapShell
        initialProviders={result}
        initialPage={page}
        isReferrer
        fetchError={fetchError}
      />
    </div>
  );
}
