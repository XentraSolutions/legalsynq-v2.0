import Link from 'next/link';
import { Routes } from '@/lib/routes';

/**
 * Root landing page — no auth guard.
 *
 * Serves as a public health-check page confirming the CC app has started
 * and as a quick-nav into the main sections.
 *
 * All linked pages still require PlatformAdmin session via requirePlatformAdmin().
 */
export default function RootPage() {
  return (
    <div className="min-h-screen bg-gray-50 flex flex-col items-center justify-center px-4">

      {/* App identity */}
      <div className="mb-8 text-center space-y-2">
        <div className="inline-flex items-center gap-2 px-3 py-1 rounded-md bg-indigo-50 border border-indigo-200 mb-2">
          <span className="text-xs font-semibold text-indigo-700 tracking-wide uppercase">
            Control Center
          </span>
        </div>
        <h1 className="text-2xl font-bold text-gray-900">LegalSynq Control Center</h1>

        {/* Dev confirmation banner */}
        <div className="inline-flex items-center gap-2 px-4 py-1.5 rounded-full bg-green-50 border border-green-200">
          <span className="h-2 w-2 rounded-full bg-green-500" />
          <span className="text-sm font-medium text-green-700">Control Center Running</span>
        </div>
      </div>

      {/* Quick navigation card */}
      <div className="w-full max-w-sm bg-white border border-gray-200 rounded-xl shadow-sm overflow-hidden">
        <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
          <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide">
            Jump to section
          </p>
        </div>
        <nav className="divide-y divide-gray-100">
          <NavLink href={Routes.tenants}     label="All Tenants"         description="Manage tenant accounts and entitlements" />
          <NavLink href={Routes.tenantUsers} label="Tenant Users"        description="View and manage users across all tenants" />
          <NavLink href={Routes.roles}       label="Roles & Permissions" description="Platform RBAC roles and permission definitions" />
        </nav>
        <div className="px-5 py-3 border-t border-gray-100">
          <Link
            href="/login"
            className="block w-full text-center text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 transition-colors px-4 py-2 rounded-lg"
          >
            Sign in to Control Center
          </Link>
        </div>
      </div>

      <p className="mt-6 text-xs text-gray-400">
        Port 5004 · Platform administration access only
      </p>
    </div>
  );
}

function NavLink({
  href,
  label,
  description,
}: {
  href:        string;
  label:       string;
  description: string;
}) {
  return (
    <Link
      href={href}
      className="flex items-center justify-between px-5 py-3.5 hover:bg-gray-50 transition-colors group"
    >
      <div>
        <p className="text-sm font-medium text-gray-800 group-hover:text-indigo-700 transition-colors">
          {label}
        </p>
        <p className="text-xs text-gray-400 mt-0.5">{description}</p>
      </div>
      <span className="text-gray-300 group-hover:text-indigo-400 transition-colors text-sm">→</span>
    </Link>
  );
}
