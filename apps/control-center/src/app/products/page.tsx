import { requirePlatformAdmin } from '@/lib/auth-guards';
import { CCShell } from '@/components/shell/cc-shell';

export default async function ProductsPage() {
  const session = await requirePlatformAdmin();

  return (
    <CCShell userEmail={session.email}>
      <div className="max-w-3xl">
        <h1 className="text-xl font-semibold text-gray-900 mb-1">Product Entitlements</h1>
        <p className="text-sm text-gray-500 mb-6">
          Manage which products are available to each tenant.
        </p>
        <div className="rounded-xl border border-gray-200 bg-white p-8 text-center text-sm text-gray-400">
          Product entitlement configuration — coming soon.
        </div>
      </div>
    </CCShell>
  );
}
