import { requireOrg } from '@/lib/auth-guards';
import { buildNavGroups, orgTypeLabel } from '@/lib/nav';
import { AppShell } from '@/components/shell/app-shell';
import type { NavGroup } from '@/types';
import Link from 'next/link';

/**
 * Dashboard — default landing page after login.
 * Shows a welcome card and quick-access tiles for each accessible product.
 */
export default async function DashboardPage() {
  const session = await requireOrg();
  const groups  = buildNavGroups(session);
  const productGroups = groups.filter(g => g.id !== 'admin');

  return (
    <AppShell>
      <div className="max-w-4xl space-y-8">

        {/* Welcome header */}
        <div>
          <h1 className="text-xl font-bold text-[#0f1928]">
            Welcome back{session.orgName ? `, ${session.orgName}` : ''}
          </h1>
          <p className="text-sm text-gray-500 mt-1">
            {orgTypeLabel(session.orgType)} · {session.email}
          </p>
        </div>

        {/* Product tiles */}
        {productGroups.length > 0 ? (
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-gray-400 mb-3">
              Your Products
            </p>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
              {productGroups.map(group => (
                <ProductCard key={group.id} group={group} />
              ))}
            </div>
          </div>
        ) : (
          <div className="rounded-xl border border-dashed border-gray-300 p-10 text-center bg-white">
            <i className="ri-inbox-line text-3xl text-gray-300" />
            <p className="mt-3 text-sm font-medium text-gray-600">No products available</p>
            <p className="text-xs text-gray-400 mt-1">
              Contact your administrator to get access to LegalSynq products.
            </p>
          </div>
        )}

        {/* Admin shortcut */}
        {(session.isTenantAdmin || session.isPlatformAdmin) && (
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-gray-400 mb-3">
              Administration
            </p>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
              <AdminCard
                href="/admin/users"
                icon="ri-group-line"
                label="Users"
                description="Manage users and their roles"
              />
              <AdminCard
                href="/admin/organizations"
                icon="ri-building-line"
                label="Organizations"
                description="View and manage organizations"
              />
            </div>
          </div>
        )}

      </div>
    </AppShell>
  );
}

// ── Product card ──────────────────────────────────────────────────────────────

function ProductCard({ group }: { group: NavGroup }) {
  const primaryHref = group.items[0]?.href ?? '#';

  const colors: Record<string, { bg: string; icon: string; border: string }> = {
    careconnect: { bg: 'bg-blue-50',   icon: 'text-blue-500',   border: 'border-blue-100' },
    fund:        { bg: 'bg-green-50',  icon: 'text-green-500',  border: 'border-green-100' },
    lien:        { bg: 'bg-purple-50', icon: 'text-purple-500', border: 'border-purple-100' },
  };
  const c = colors[group.id] ?? { bg: 'bg-gray-50', icon: 'text-gray-400', border: 'border-gray-100' };

  return (
    <Link
      href={primaryHref}
      className="group block rounded-xl border border-gray-200 bg-white p-5 hover:shadow-md hover:border-gray-300 transition-all"
    >
      {/* Icon */}
      <div className={`inline-flex items-center justify-center w-10 h-10 rounded-lg ${c.bg} ${c.border} border mb-4`}>
        {group.icon && <i className={`${group.icon} text-lg ${c.icon}`} />}
      </div>

      {/* Label */}
      <p className="text-sm font-bold text-[#0f1928] group-hover:text-orange-600 transition-colors">
        {group.label}
      </p>

      {/* Quick links */}
      <ul className="mt-3 space-y-1">
        {group.items.slice(0, 3).map(item => (
          <li key={item.href} className="flex items-center gap-1.5 text-[11px] text-gray-500">
            <i className="ri-arrow-right-s-line text-gray-300 text-sm" />
            {item.label}
          </li>
        ))}
      </ul>
    </Link>
  );
}

// ── Admin card ────────────────────────────────────────────────────────────────

function AdminCard({ href, icon, label, description }: { href: string; icon: string; label: string; description: string }) {
  return (
    <Link
      href={href}
      className="group flex items-start gap-3 rounded-xl border border-gray-200 bg-white p-4 hover:shadow-md hover:border-gray-300 transition-all"
    >
      <div className="inline-flex items-center justify-center w-8 h-8 rounded-lg bg-gray-50 border border-gray-100 shrink-0">
        <i className={`${icon} text-sm text-gray-400`} />
      </div>
      <div>
        <p className="text-sm font-semibold text-[#0f1928] group-hover:text-orange-600 transition-colors">{label}</p>
        <p className="text-[11px] text-gray-400 mt-0.5">{description}</p>
      </div>
    </Link>
  );
}
