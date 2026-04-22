import { requireOrg }              from '@/lib/auth-guards';
import { careConnectServerApi }    from '@/lib/careconnect-server-api';
import { ServerApiError }          from '@/lib/server-api-client';
import { MyNetworkClient }         from '@/components/careconnect/my-network-client';
import type { NetworkDetail }      from '@/types/careconnect';

export const dynamic = 'force-dynamic';

/**
 * /careconnect/network — My Preferred Provider Network
 *
 * Loads the tenant's first (default) network and renders the full
 * provider management table. If no network exists yet, the client
 * component guides the user to create one.
 *
 * Accessible to any CareConnect user — no NetworkManager role required.
 */
export default async function MyNetworkPage() {
  await requireOrg();

  let network: NetworkDetail | null = null;
  let fetchError: string | null = null;

  try {
    const networks = await careConnectServerApi.networks.list();
    if (networks.length > 0) {
      network = await careConnectServerApi.networks.getById(networks[0].id);
    }
  } catch (err) {
    if (err instanceof ServerApiError && err.status !== 404) {
      fetchError = err.message;
    } else if (!(err instanceof ServerApiError)) {
      fetchError = 'Unable to load your network. Please try again.';
    }
  }

  return (
    <div className="space-y-1">
      <MyNetworkClient initialNetwork={network} fetchError={fetchError} />
    </div>
  );
}
