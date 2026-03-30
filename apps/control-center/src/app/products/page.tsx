import { requirePlatformAdmin } from '@/lib/auth-guards';
import { CCShell }              from '@/components/shell/cc-shell';

/**
 * /products — Product Entitlements.
 *
 * Status: MOCKUP
 *
 * No product catalog admin backend endpoint exists.
 * Per-tenant entitlements are managed via the tenant detail page.
 */
export default async function ProductsPage() {
  const session = await requirePlatformAdmin();

  return (
    <CCShell userEmail={session.email}>
      <div className="max-w-3xl space-y-4">

        <div className="flex items-start justify-between">
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-xl font-semibold text-gray-900">Products</h1>
              <span className="inline-flex items-center text-[11px] font-semibold px-2.5 py-1 rounded-full bg-gray-100 text-gray-500">
                MOCKUP
              </span>
            </div>
            <p className="text-sm text-gray-500 mt-0.5">
              Product catalog administration — not yet implemented as a dedicated admin endpoint.
            </p>
          </div>
        </div>

        <div className="bg-amber-50 border border-amber-200 rounded-lg px-4 py-3 text-xs text-amber-800">
          <strong>Not yet wired:</strong> A standalone product catalog admin endpoint has not been
          built. Per-tenant product entitlements can be managed via the{' '}
          <a href="/tenants" className="underline font-medium">Tenants</a> detail view.
        </div>

        <div className="rounded-xl border border-gray-200 bg-white p-8 text-center text-sm text-gray-400">
          Product catalog administration coming soon.
        </div>

      </div>
    </CCShell>
  );
}
