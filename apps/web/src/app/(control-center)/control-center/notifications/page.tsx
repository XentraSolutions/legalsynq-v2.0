import Link                       from 'next/link';
import { cookies }                 from 'next/headers';
import { requireCCPlatformAdmin }  from '@/lib/auth-guards';
import { CCRoutes }                from '@/lib/control-center-routes';
import { controlCenterServerApi }  from '@/lib/control-center-api';
import {
  setNotifTenantContextAction,
  clearNotifTenantContextAction,
  type TenantContext,
} from './actions';

interface SectionCard {
  href:        string;
  title:       string;
  description: string;
  icon:        string;
}

const SECTIONS: SectionCard[] = [
  {
    href:        CCRoutes.notifProviders,
    title:       'Providers',
    description: 'Configure delivery providers (SendGrid, SMTP, Twilio) and channel routing settings.',
    icon:        'ri-plug-line',
  },
  {
    href:        CCRoutes.notifTemplates,
    title:       'Templates',
    description: 'Create and manage message templates and draft versions for each channel.',
    icon:        'ri-file-text-line',
  },
  {
    href:        CCRoutes.notifBilling,
    title:       'Billing',
    description: 'Manage billing plans, usage-based rates, and rate-limit policies.',
    icon:        'ri-bar-chart-line',
  },
  {
    href:        CCRoutes.notifContactPolicies,
    title:       'Contact Policies',
    description: 'Set blocking rules for suppressed, bounced, unsubscribed, and invalid contacts.',
    icon:        'ri-shield-check-line',
  },
  {
    href:        CCRoutes.notifLog,
    title:       'Delivery Log',
    description: 'Browse recent notification dispatch events, statuses, and failure reasons.',
    icon:        'ri-list-check-line',
  },
];

function readTenantContext(): TenantContext | null {
  const raw = cookies().get('cc_tenant_context')?.value;
  if (!raw) return null;
  try {
    const ctx = JSON.parse(raw) as Partial<TenantContext>;
    if (ctx.tenantId && ctx.tenantName && ctx.tenantCode) {
      return ctx as TenantContext;
    }
    return null;
  } catch {
    return null;
  }
}

export default async function NotificationsOverviewPage() {
  await requireCCPlatformAdmin();

  const tenantCtx = readTenantContext();

  const tenantsResult = !tenantCtx
    ? await controlCenterServerApi.tenants.list({ page: 1, pageSize: 50 }).catch(() => null)
    : null;

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Notifications</h1>
          <p className="mt-1 text-sm text-gray-500">
            Platform-wide notification administration — providers, templates, billing, and contact policies.
          </p>
        </div>
        {tenantCtx && (
          <form action={clearNotifTenantContextAction}>
            <button
              type="submit"
              className="text-xs text-gray-500 border border-gray-200 bg-white hover:bg-gray-50 px-3 py-1.5 rounded-md transition-colors whitespace-nowrap"
            >
              Change Tenant
            </button>
          </form>
        )}
      </div>

      {/* ── Active tenant banner ─────────────────────────────────────────── */}
      {tenantCtx && (
        <div className="flex items-center gap-2 bg-amber-50 border border-amber-200 rounded-lg px-4 py-2.5 text-sm text-amber-800">
          <span className="h-2 w-2 rounded-full bg-amber-500 shrink-0" />
          <span>
            Viewing notifications for{' '}
            <span className="font-semibold">{tenantCtx.tenantName}</span>
            <span className="text-amber-600 ml-1">({tenantCtx.tenantCode})</span>
          </span>
        </div>
      )}

      {/* ── No tenant selected — show picker ────────────────────────────── */}
      {!tenantCtx && (
        <div className="rounded-lg border border-amber-200 bg-amber-50 p-6">
          <p className="text-sm font-semibold text-amber-800 mb-1">Select a tenant to continue</p>
          <p className="text-sm text-amber-700 mb-4">
            The Notifications section is scoped to a tenant. Choose one below to activate it.
          </p>

          {tenantsResult && tenantsResult.items.length > 0 ? (
            <div className="bg-white border border-amber-100 rounded-lg overflow-hidden divide-y divide-gray-100">
              {tenantsResult.items.map(tenant => {
                const activateAction = setNotifTenantContextAction.bind(null, {
                  tenantId:   tenant.id,
                  tenantName: tenant.displayName,
                  tenantCode: tenant.code,
                });
                return (
                  <div key={tenant.id} className="flex items-center justify-between px-4 py-2.5 hover:bg-gray-50">
                    <div>
                      <p className="text-sm font-medium text-gray-900">{tenant.displayName}</p>
                      <p className="text-xs text-gray-400">{tenant.code} · {tenant.type}</p>
                    </div>
                    <form action={activateAction}>
                      <button
                        type="submit"
                        className="text-xs text-amber-700 font-medium border border-amber-300 bg-amber-50 hover:bg-amber-100 px-3 py-1 rounded-md transition-colors whitespace-nowrap"
                      >
                        Activate
                      </button>
                    </form>
                  </div>
                );
              })}
            </div>
          ) : (
            <p className="text-sm text-amber-600 italic">No tenants available.</p>
          )}
        </div>
      )}

      {/* ── Section cards (only when tenant is active) ──────────────────── */}
      {tenantCtx && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {SECTIONS.map(s => (
            <Link
              key={s.href}
              href={s.href}
              className="group block bg-white border border-gray-200 rounded-lg p-5 hover:border-indigo-300 hover:shadow-sm transition-all"
            >
              <div className="flex items-start gap-3">
                <div className="mt-0.5 flex-shrink-0 w-8 h-8 rounded-md bg-indigo-50 border border-indigo-100 flex items-center justify-center">
                  <i className={`${s.icon} text-indigo-600 text-base`} />
                </div>
                <div className="flex-1 min-w-0">
                  <h2 className="text-sm font-semibold text-gray-900 group-hover:text-indigo-700 transition-colors">
                    {s.title}
                  </h2>
                  <p className="mt-1 text-xs text-gray-500 leading-relaxed">{s.description}</p>
                  <p className="mt-2 text-xs text-indigo-600 font-medium opacity-0 group-hover:opacity-100 transition-opacity">
                    Open →
                  </p>
                </div>
              </div>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
