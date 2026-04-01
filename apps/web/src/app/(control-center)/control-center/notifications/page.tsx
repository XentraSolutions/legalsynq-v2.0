import { cookies }                  from 'next/headers';
import Link                          from 'next/link';
import { requireCCPlatformAdmin }    from '@/lib/auth-guards';
import { CCRoutes }                  from '@/lib/control-center-routes';
import { controlCenterServerApi }    from '@/lib/control-center-api';
import { ActivateTenantButton, ClearTenantContextButton } from '@/components/control-center/tenant-context-button';
import type { TenantContext } from './actions';

// ── Types ──────────────────────────────────────────────────────────────────────

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

// ── Helpers ────────────────────────────────────────────────────────────────────

function readTenantContext(): TenantContext | null {
  const raw = cookies().get('cc_tenant_context')?.value;
  if (!raw) return null;
  try {
    const ctx = JSON.parse(raw) as Partial<TenantContext>;
    if (ctx.tenantId && ctx.tenantName && ctx.tenantCode) return ctx as TenantContext;
    return null;
  } catch {
    return null;
  }
}

// ── Page ───────────────────────────────────────────────────────────────────────

export default async function NotificationsOverviewPage() {
  await requireCCPlatformAdmin();

  const tenantCtx     = readTenantContext();
  const tenantsResult = await controlCenterServerApi.tenants.list({ page: 1, pageSize: 50 }).catch(() => null);
  const tenants       = tenantsResult?.items ?? [];

  return (
    <div className="space-y-6">

      {/* ── Page header ───────────────────────────────────────────────────── */}
      <div>
        <h1 className="text-xl font-semibold text-gray-900">Notifications</h1>
        <p className="mt-1 text-sm text-gray-500">
          Platform-wide notification administration — providers, templates, billing, and contact policies.
        </p>
      </div>

      {/* ── General settings / section cards ─────────────────────────────── */}
      <section>
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-sm font-semibold text-gray-700">General Settings</h2>
          {tenantCtx && (
            <div className="flex items-center gap-2">
              <span className="flex items-center gap-1.5 text-xs text-amber-700 font-medium">
                <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
                {tenantCtx.tenantName} ({tenantCtx.tenantCode})
              </span>
              <ClearTenantContextButton label="Clear" />
            </div>
          )}
        </div>

        {!tenantCtx && (
          <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800 mb-3">
            Select an active tenant below to enable the settings sections.
          </div>
        )}

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {SECTIONS.map(s => (
            <Link
              key={s.href}
              href={tenantCtx ? s.href : '#'}
              aria-disabled={!tenantCtx}
              className={`group block bg-white border rounded-lg p-5 transition-all ${
                tenantCtx
                  ? 'border-gray-200 hover:border-indigo-300 hover:shadow-sm cursor-pointer'
                  : 'border-gray-100 opacity-40 cursor-not-allowed'
              }`}
            >
              <div className="flex items-start gap-3">
                <div className="mt-0.5 flex-shrink-0 w-8 h-8 rounded-md bg-indigo-50 border border-indigo-100 flex items-center justify-center">
                  <i className={`${s.icon} text-indigo-600 text-base`} />
                </div>
                <div className="flex-1 min-w-0">
                  <h3 className="text-sm font-semibold text-gray-900 group-hover:text-indigo-700 transition-colors">
                    {s.title}
                  </h3>
                  <p className="mt-1 text-xs text-gray-500 leading-relaxed">{s.description}</p>
                  {tenantCtx && (
                    <p className="mt-2 text-xs text-indigo-600 font-medium opacity-0 group-hover:opacity-100 transition-opacity">
                      Open →
                    </p>
                  )}
                </div>
              </div>
            </Link>
          ))}
        </div>
      </section>

      {/* ── Tenant list ───────────────────────────────────────────────────── */}
      <section>
        <h2 className="text-sm font-semibold text-gray-700 mb-3">Tenants</h2>

        <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
          {tenants.length === 0 ? (
            <p className="px-4 py-8 text-center text-sm text-gray-400">No tenants available.</p>
          ) : (
            <>
              <table className="min-w-full divide-y divide-gray-100 text-sm">
                <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                  <tr>
                    <th className="px-4 py-2.5 text-left font-medium">Name</th>
                    <th className="px-4 py-2.5 text-left font-medium">Type</th>
                    <th className="px-4 py-2.5 text-left font-medium">Status</th>
                    <th className="px-4 py-2.5 text-left font-medium">Context</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {tenants.map(t => {
                    const isActive = t.id === tenantCtx?.tenantId;
                    return (
                      <tr
                        key={t.id}
                        className={`transition-colors ${isActive ? 'bg-amber-50' : 'hover:bg-gray-50'}`}
                      >
                        <td className="px-4 py-2.5">
                          <div className="flex items-center gap-2">
                            <div>
                              <p className="font-medium text-gray-900">{t.displayName}</p>
                              <p className="text-xs text-gray-400 mt-0.5">{t.code}</p>
                            </div>
                            {isActive && (
                              <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-full bg-amber-100 border border-amber-300 text-[10px] font-semibold text-amber-700 shrink-0">
                                <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
                                Active
                              </span>
                            )}
                          </div>
                        </td>
                        <td className="px-4 py-2.5 text-xs text-gray-600">{t.type}</td>
                        <td className="px-4 py-2.5">
                          <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${
                            t.status === 'Active'
                              ? 'bg-green-50 text-green-700 border-green-200'
                              : t.status === 'Suspended'
                              ? 'bg-red-50 text-red-700 border-red-200'
                              : 'bg-gray-100 text-gray-500 border-gray-200'
                          }`}>
                            {t.status}
                          </span>
                        </td>
                        <td className="px-4 py-2.5">
                          {isActive ? (
                            <ClearTenantContextButton label="Deactivate" />
                          ) : (
                            <ActivateTenantButton
                              tenant={{ tenantId: t.id, tenantName: t.displayName, tenantCode: t.code }}
                            />
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </>
          )}
        </div>
      </section>

    </div>
  );
}
