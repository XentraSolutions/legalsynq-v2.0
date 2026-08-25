import { requirePlatformAdmin }      from '@/lib/auth-guards';
import { controlCenterServerApi }    from '@/lib/control-center-api';
import { CCShell }                   from '@/components/shell/cc-shell';
import { PlatformBaseUrlPanel }      from '@/components/products/platform-base-url-panel';
import { PRODUCT_CATALOG }           from '@/lib/product-catalog';
import type { PlatformSetting }      from '@/types/control-center';

export const dynamic = 'force-dynamic';

/**
 * /products — Product Catalog.
 *
 * Contains:
 *   - Platform Base URL section (LS-ID-TNT-016-01): configure the domain /
 *     fallback URL used for tenant-subdomain portal links and email links.
 *
 * Per-tenant entitlements are managed via the Tenant detail page.
 */
export default async function ProductsPage() {
  const session = await requirePlatformAdmin();

  let settings: PlatformSetting[] = [];
  try {
    settings = await controlCenterServerApi.settings.list();
  } catch {
    // Non-fatal — UI falls back to empty strings below.
  }

  const findStr = (key: string) =>
    String(settings.find(s => s.key === key)?.value ?? '');

  const portalBaseDomain = findStr('platform.portalBaseDomain');
  const portalBaseUrl    = findStr('platform.portalBaseUrl');

  return (
    <CCShell userEmail={session.email}>
      <div className="max-w-3xl space-y-6">

        {/* Page header */}
        <div className="flex items-start justify-between">
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-xl font-semibold text-gray-900">Products</h1>
              <span className="inline-flex items-center text-[11px] font-semibold px-2.5 py-1 rounded-full bg-indigo-50 text-indigo-700">
                {PRODUCT_CATALOG.length} PRODUCTS
              </span>
            </div>
            <p className="text-sm text-gray-500 mt-0.5">
              Platform-wide product configuration and catalog settings.
            </p>
          </div>
        </div>

        {/* ── Platform Base URL ─────────────────────────────────────────── */}
        <PlatformBaseUrlPanel
          portalBaseDomain={portalBaseDomain}
          portalBaseUrl={portalBaseUrl}
        />

        {/* ── Per-tenant entitlements notice ───────────────────────────── */}
        <div className="bg-amber-50 border border-amber-200 rounded-lg px-4 py-3 text-xs text-amber-800">
          <strong>Per-tenant product entitlements</strong> are managed via the{' '}
          <a href="/tenants" className="underline font-medium">Tenants</a> detail view.
          Select a tenant to enable or disable its products.
        </div>

        {/* ── Product catalog ────────────────────────────────────────────── */}
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          {PRODUCT_CATALOG.map(product => (
            <div key={product.code} className="rounded-xl border border-gray-200 bg-white p-5">
              <div className="flex items-start gap-3">
                {product.iconSrc ? (
                  <img
                    src={product.iconSrc}
                    alt=""
                    aria-hidden
                    className="h-9 w-9 shrink-0 object-contain"
                  />
                ) : (
                  <span
                    aria-hidden
                    className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-indigo-50 text-xs font-bold text-indigo-700"
                  >
                    SS
                  </span>
                )}
                <div className="min-w-0">
                  <h2 className="text-sm font-semibold text-gray-900">{product.name}</h2>
                  <p className="mt-0.5 font-mono text-[11px] text-gray-400">{product.platformCode}</p>
                </div>
              </div>
              <p className="mt-3 text-xs leading-relaxed text-gray-500">{product.description}</p>
            </div>
          ))}
        </div>

      </div>
    </CCShell>
  );
}
