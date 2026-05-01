import { requireOrg }           from '@/lib/auth-guards';
import { careConnectServerApi }  from '@/lib/careconnect-server-api';
import { ServerApiError }        from '@/lib/server-api-client';
import { BrowseNetworksClient }  from '@/components/careconnect/browse-networks-client';
import type { NetworkSummary }   from '@/types/careconnect';

export const dynamic = 'force-dynamic';

/**
 * /careconnect/browse-networks — Read-only provider network directory for
 * law firm referrers (CC-REFERRER-BROWSE).
 *
 * Lists all active networks in the tenant. Selecting one reveals the
 * interactive provider map so referrers can identify target providers
 * before submitting a referral.
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

  return (
    <BrowseNetworksClient
      initialNetworks={networks}
      fetchError={fetchError}
    />
  );
}
