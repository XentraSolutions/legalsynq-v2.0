import Link                    from 'next/link';
import { requireOrg }           from '@/lib/auth-guards';
import { careConnectServerApi }  from '@/lib/careconnect-server-api';
import { ServerApiError }        from '@/lib/server-api-client';
import type { NetworkSummary }   from '@/types/careconnect';

export const dynamic = 'force-dynamic';

/**
 * /careconnect/browse-networks — Available provider networks directory.
 * CC-REFERRER-BROWSE: Law firm referrers can browse all active networks
 * and click through to submit referrals to providers within each network.
 */
export default async function BrowseNetworksPage() {
  await requireOrg();

  let networks: NetworkSummary[] = [];
  let fetchError: string | null  = null;

  try {
    networks = await careConnectServerApi.browseNetworks.list();
  } catch (err) {
    if (err instanceof ServerApiError) {
      fetchError = err.message;
    } else {
      fetchError = 'Unable to load networks. Please try again.';
    }
  }

  if (fetchError) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-6 text-sm text-red-700">
        {fetchError}
      </div>
    );
  }

  if (networks.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-24 text-center">
        <i className="ri-share-circle-line text-5xl text-gray-200 mb-4" />
        <p className="text-sm font-medium text-gray-500">No provider networks are available yet.</p>
        <p className="text-xs text-gray-400 mt-1">Check back later or contact your coordinator.</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold text-gray-900">Available Networks</h1>
        <p className="text-sm text-gray-500 mt-0.5">
          Select a network to browse providers and submit a referral.
        </p>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {networks.map(network => (
          <NetworkCard key={network.id} network={network} />
        ))}
      </div>
    </div>
  );
}

function NetworkCard({ network }: { network: NetworkSummary }) {
  const initials = network.name
    .split(' ')
    .slice(0, 2)
    .map(w => w[0] ?? '')
    .join('')
    .toUpperCase();

  return (
    <Link
      href={`/careconnect/browse-networks/${network.id}`}
      className="group flex flex-col rounded-xl border border-gray-200 bg-white p-5 shadow-sm transition-all hover:border-blue-300 hover:shadow-md"
    >
      {/* Logo placeholder */}
      <div className="mb-4 flex h-14 w-14 items-center justify-center rounded-xl bg-blue-50 text-blue-600 text-lg font-bold group-hover:bg-blue-100 transition-colors">
        {initials || <i className="ri-share-circle-line text-2xl" />}
      </div>

      {/* Name */}
      <p className="text-sm font-semibold text-gray-900 group-hover:text-blue-700 transition-colors line-clamp-2">
        {network.name}
      </p>

      {/* Description */}
      {network.description && (
        <p className="mt-1 text-xs text-gray-500 line-clamp-2">{network.description}</p>
      )}

      {/* Provider count */}
      <div className="mt-3 flex items-center gap-1.5 text-xs text-gray-400">
        <i className="ri-hospital-line" />
        <span>{network.providerCount} provider{network.providerCount !== 1 ? 's' : ''}</span>
      </div>

      {/* CTA */}
      <div className="mt-4 flex items-center gap-1 text-xs font-medium text-blue-600 group-hover:text-blue-700">
        View providers & refer
        <i className="ri-arrow-right-line" />
      </div>
    </Link>
  );
}
