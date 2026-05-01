import Link                         from 'next/link';
import { notFound }                  from 'next/navigation';
import { requireAuthenticated }      from '@/lib/auth-guards';
import {
  fetchPublicNetworkDetail,
  type PublicNetworkDetail,
} from '@/lib/public-network-api';
import { PublicNetworkView }         from '@/components/careconnect/public-network-view';

export const dynamic = 'force-dynamic';

interface Props {
  params: Promise<{ id: string }>;
}

/**
 * /careconnect/browse-networks/[id] — Full referral view for a specific network.
 * CC-REFERRER-BROWSE: Authenticated law firm users get the same provider map +
 * referral form as the public network page, resolved via their session tenant.
 */
export default async function BrowseNetworkDetailPage({ params }: Props) {
  const { id }  = await params;
  const session = await requireAuthenticated();

  let detail: PublicNetworkDetail | null = null;

  try {
    detail = await fetchPublicNetworkDetail(session.tenantId, id);
  } catch {
    // fall through to notFound
  }

  if (!detail) return notFound();

  return (
    <div className="flex flex-col h-[calc(100vh-5rem)] -mx-4 -mt-4 overflow-hidden">
      {/* Slim breadcrumb */}
      <div className="shrink-0 px-4 py-2 border-b border-gray-100 bg-white">
        <nav className="flex items-center gap-1.5 text-xs text-gray-500">
          <Link href="/careconnect/browse-networks" className="hover:text-blue-600">
            Available Networks
          </Link>
          <i className="ri-arrow-right-s-line" />
          <span className="text-gray-800 font-medium">{detail.networkName}</span>
        </nav>
      </div>

      {/* Full public referral view fills the remaining height */}
      <div className="flex-1 overflow-hidden">
        <PublicNetworkView
          detail={detail}
          tenantCode={session.tenantCode}
          tenantId={session.tenantId}
        />
      </div>
    </div>
  );
}
