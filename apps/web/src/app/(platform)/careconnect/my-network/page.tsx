import { requireOrg }              from '@/lib/auth-guards';
import { careConnectServerApi }    from '@/lib/careconnect-server-api';
import { ServerApiError }          from '@/lib/server-api-client';
import { MyNetworkClient }         from '@/components/careconnect/my-network-client';
import { PublicNetworkAccessCodePanel } from '@/components/careconnect/public-network-access-code-panel';
import { tenantServerApi }         from '@/lib/tenant-api';
import type { CareConnectAccessCodeMetadata } from '@/lib/tenant-api';
import type { NetworkDetail, SpecialtyOption } from '@/types/careconnect';

export const dynamic = 'force-dynamic';

/**
 * /careconnect/my-network — Preferred Provider Network management.
 *
 * Authenticated view. Allows the tenant to create, populate, and manage
 * their preferred provider network. The public-facing read-only view lives
 * at /careconnect/network (no auth required).
 */
export default async function MyNetworkPage() {
  const session = await requireOrg();
  const canManageAccessCode = session.isPlatformAdmin || session.isTenantAdmin;

  let network: NetworkDetail | null = null;
  let fetchError: string | null = null;
  let accessCodeStatus: CareConnectAccessCodeMetadata | null = null;
  let specialtyOptions: SpecialtyOption[] = [];

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

  try {
    specialtyOptions = await careConnectServerApi.specialties.list();
  } catch {
    specialtyOptions = [];
  }

  if (canManageAccessCode) {
    try {
      accessCodeStatus = await tenantServerApi.getCareConnectAccessCode(session.tenantId);
    } catch {
      // Non-fatal — keep the page usable even if the access-code panel cannot load.
    }
  }

  return (
    <div className="space-y-5">
      {canManageAccessCode && accessCodeStatus && (
        <PublicNetworkAccessCodePanel initialStatus={accessCodeStatus} />
      )}
      <MyNetworkClient initialNetwork={network} fetchError={fetchError} specialtyOptions={specialtyOptions} />
    </div>
  );
}
