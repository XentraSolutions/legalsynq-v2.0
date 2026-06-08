import { redirect }             from 'next/navigation';
import { requireProductRole }    from '@/lib/auth-guards';
import { careConnectServerApi }  from '@/lib/careconnect-server-api';
import { fetchPublicNetworks }   from '@/lib/public-network-api';
import { ServerApiError }        from '@/lib/server-api-client';
import type { NetworkSummary }   from '@/types/careconnect';
import { NetworkCard }            from '@/components/careconnect/network-card';
import { ProductRole, OrgType }  from '@/types';

export const dynamic = 'force-dynamic';

/**
 * /careconnect/browse-networks — Available provider networks directory.
 * CC-REFERRER-BROWSE: Accessible only to elevated law firm referrers
 * (CareConnectReferrer role). Network managers (lien companies) are
 * redirected to the dashboard — they manage networks, they don't browse them.
 */
export default async function BrowseNetworksPage() {
  // Requires CareConnectReferrer role; redirects to /dashboard if absent.
  const session = await requireProductRole(ProductRole.CareConnectReferrer);

  // Lien company users must never access browse-networks, regardless of which
  // product roles their account carries. NetworkManager is the typical case;
  // the OrgType check covers any edge case where a lien-owner user only holds
  // CareConnectReferrer (e.g. during a partial provisioning state).
  if (
    session.productRoles.includes(ProductRole.CareConnectNetworkManager) ||
    session.orgType === OrgType.LienOwner
  ) redirect('/careconnect/dashboard');

  let tenantNetworkGroups: Array<{
    tenantId: string;
    tenantCode: string;
    tenantName: string;
    networks: NetworkSummary[];
  }> = [];
  let fetchError: string | null = null;

  try {
    const assignments = await careConnectServerApi.access.getMyProductAccess('SYNQ_CARECONNECT');
    const careConnectTenants = assignments
      .filter(item => item.accessStatus === 'Granted')
      .map(item => ({
        tenantId: item.tenantId,
        tenantCode: item.tenantCode,
        tenantName: item.tenantName,
      }))
      .filter((item, index, arr) => arr.findIndex(x => x.tenantId === item.tenantId) === index);

    const groups = await Promise.all(
      careConnectTenants.map(async tenant => ({
        ...tenant,
        networks: await fetchPublicNetworks(tenant.tenantId),
      })),
    );

    tenantNetworkGroups = groups.filter(group => group.networks.length > 0);
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

  if (tenantNetworkGroups.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-24 text-center">
        <i className="ri-share-circle-line text-5xl text-gray-200 mb-4" />
        <p className="text-sm font-medium text-gray-500">No provider networks are available for your CareConnect assignments yet.</p>
        <p className="text-xs text-gray-400 mt-1">Check back later or contact your coordinator.</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold text-gray-900">Available Networks</h1>
        <p className="text-sm text-gray-500 mt-0.5">
          Select a network from any CareConnect tenant assigned to this account.
        </p>
      </div>

      <div className="space-y-8">
        {tenantNetworkGroups.map(group => {
          const tenantLogoUrl = `/api/branding/logo/public?tenantCode=${encodeURIComponent(group.tenantCode)}`;
          return (
            <section key={group.tenantId} className="space-y-3">
              <div>
                <h2 className="text-sm font-semibold text-gray-900">{group.tenantName}</h2>
                <p className="text-xs text-gray-500">{group.tenantCode}</p>
              </div>

              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
                {group.networks.map(network => (
                  <NetworkCard
                    key={`${group.tenantId}:${network.id}`}
                    network={network}
                    tenantLogoUrl={tenantLogoUrl}
                    href={`/careconnect/browse-networks/${network.id}?tenantId=${encodeURIComponent(group.tenantId)}`}
                  />
                ))}
              </div>
            </section>
          );
        })}
      </div>
    </div>
  );
}
