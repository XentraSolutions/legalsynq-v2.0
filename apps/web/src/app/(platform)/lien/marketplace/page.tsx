import { requireProductRole } from '@/lib/auth-guards';
import { ProductRole } from '@/types';

/**
 * SynqLien — Lien Marketplace.
 * Only SYNQLIEN_BUYER may browse and purchase liens.
 */
export default async function MarketplacePage() {
  await requireProductRole(ProductRole.SynqLienBuyer);

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold text-gray-900">Lien Marketplace</h1>

      {/* TODO: Replace with LienMarketplaceList component fetching from apiClient */}
      <div className="bg-white border border-gray-200 rounded-lg p-8 text-center text-sm text-gray-400">
        Marketplace lien browser — connect to{' '}
        <code className="font-mono bg-gray-100 px-1 rounded">
          GET /api/fund/api/liens
        </code>
      </div>
    </div>
  );
}
