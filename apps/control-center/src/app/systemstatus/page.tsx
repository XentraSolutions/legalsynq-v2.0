import Link from 'next/link';
import { Routes } from '@/lib/routes';

export default function SystemStatusPage() {
  return (
    <div className="min-h-screen bg-gray-50 flex flex-col items-center justify-center px-4 py-10">

      <div className="mb-8 text-center space-y-2">
        <div className="inline-flex items-center gap-2 px-3 py-1 rounded-md bg-indigo-50 border border-indigo-200 mb-2">
          <span className="text-xs font-semibold text-indigo-700 tracking-wide uppercase">
            Control Center
          </span>
        </div>
        <h1 className="text-2xl font-bold text-gray-900">LegalSynq Control Center</h1>
        <div className="inline-flex items-center gap-2 px-4 py-1.5 rounded-full bg-green-50 border border-green-200">
          <span className="h-2 w-2 rounded-full bg-green-500" />
          <span className="text-sm font-medium text-green-700">System Online</span>
        </div>
      </div>

      <div className="w-full max-w-2xl space-y-3">

        <SectionCard heading="Platform">
          <NavLink href={Routes.platformReadiness} label="Platform Readiness" description="Cross-domain snapshot of migration and data coverage" badge="LIVE" />
          <NavLink href={Routes.legacyCoverage}    label="Legacy Coverage"    description="Eligibility-rules and role-assignment migration tracking" badge="LIVE" />
        </SectionCard>

        <SectionCard heading="Identity">
          <NavLink href={Routes.tenants}      label="Tenants"      description="Manage tenant accounts and product entitlements" badge="LIVE" />
          <NavLink href={Routes.tenantUsers}  label="Users"        description="View and manage users across all tenants" badge="LIVE" />
          <NavLink href={Routes.roles}        label="Roles"        description="Platform RBAC roles and permission definitions" badge="LIVE" />
          <NavLink href={Routes.scopedRoles}  label="Scoped Roles" description="Phase G: ScopedRoleAssignments global list view" badge="MOCKUP" />
          <NavLink href={Routes.orgTypes}     label="Org Types"    description="Organization type catalog (seed reference data)" badge="LIVE" />
        </SectionCard>

        <SectionCard heading="Relationships">
          <NavLink href={Routes.relationshipTypes} label="Relationship Types" description="Edge types in the organization graph" badge="LIVE" />
          <NavLink href={Routes.orgRelationships}  label="Org Relationships"  description="Live org-to-org graph edges" badge="LIVE" />
        </SectionCard>

        <SectionCard heading="Product Rules">
          <NavLink href={Routes.productRules} label="Access Rules" description="Org-type and rel-type eligibility rules per product role" badge="LIVE" />
        </SectionCard>

        <SectionCard heading="CareConnect">
          <NavLink href={Routes.careConnectIntegrity} label="Integrity" description="Referral, appointment, provider, and facility link counters" badge="LIVE" />
        </SectionCard>

        <SectionCard heading="Operations">
          <NavLink href={Routes.support}    label="Support Tools" description="Internal support case management" badge="LIVE" />
          <NavLink href={Routes.auditLogs}  label="Audit Logs"    description="System-wide event audit trail" badge="LIVE" />
          <NavLink href={Routes.monitoring} label="Monitoring"    description="Service health and active alerts" badge="IN PROGRESS" />
        </SectionCard>

        <SectionCard heading="Mockup / Not yet wired">
          <NavLink href={Routes.domains}   label="Tenant Domains" description="Custom domain assignments per tenant" badge="MOCKUP" />
          <NavLink href={Routes.products}  label="Products"       description="Product catalog administration" badge="MOCKUP" />
          <NavLink href={Routes.settings}  label="Settings"       description="Platform configuration and flags" badge="LIVE" />
        </SectionCard>

      </div>

      <div className="mt-6 text-center">
        <Link
          href="/login"
          className="inline-block text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 transition-colors px-5 py-2.5 rounded-lg"
        >
          Sign in to Control Center
        </Link>
        <p className="mt-3 text-xs text-gray-400">Platform administration access only</p>
      </div>

    </div>
  );
}

function SectionCard({ heading, children }: { heading: string; children: React.ReactNode }) {
  return (
    <div className="bg-white border border-gray-200 rounded-xl shadow-sm overflow-hidden">
      <div className="px-5 py-2.5 border-b border-gray-100 bg-gray-50">
        <p className="text-[10px] font-semibold text-gray-400 uppercase tracking-widest">{heading}</p>
      </div>
      <div className="divide-y divide-gray-100">{children}</div>
    </div>
  );
}

type BadgeVariant = 'LIVE' | 'IN PROGRESS' | 'MOCKUP';

function NavLink({
  href,
  label,
  description,
  badge,
}: {
  href:        string;
  label:       string;
  description: string;
  badge?:      BadgeVariant;
}) {
  const badgeStyles: Record<BadgeVariant, string> = {
    'LIVE':        'bg-emerald-100 text-emerald-700',
    'IN PROGRESS': 'bg-amber-100   text-amber-700',
    'MOCKUP':      'bg-gray-100    text-gray-500',
  };

  return (
    <Link
      href={href}
      className="flex items-center justify-between px-5 py-3.5 hover:bg-gray-50 transition-colors group"
    >
      <div className="min-w-0">
        <div className="flex items-center gap-2">
          <p className="text-sm font-medium text-gray-800 group-hover:text-indigo-700 transition-colors">
            {label}
          </p>
          {badge && (
            <span className={`text-[9px] font-semibold px-1.5 py-0.5 rounded-full leading-none ${badgeStyles[badge]}`}>
              {badge}
            </span>
          )}
        </div>
        <p className="text-xs text-gray-400 mt-0.5 truncate">{description}</p>
      </div>
      <span className="shrink-0 text-gray-300 group-hover:text-indigo-400 transition-colors text-sm ml-3">&rarr;</span>
    </Link>
  );
}
