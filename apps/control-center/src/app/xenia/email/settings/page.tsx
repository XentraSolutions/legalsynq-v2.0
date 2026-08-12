import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import { getEmailSettings, type EmailSettings } from '@/lib/xenia-email-api';

export const dynamic = 'force-dynamic';

export default async function EmailSettingsPage() {
  await requirePlatformAdmin();

  const jar = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';

  let settings: EmailSettings | null = null;
  let error = false;

  try {
    settings = await getEmailSettings(token);
  } catch {
    error = true;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-xl font-semibold text-gray-900">Email Settings</h2>
          <p className="text-sm text-gray-500 mt-1">
            Tenant-scoped Email module configuration. Controls connection policy, SSRF protection, and validation behaviour.
          </p>
        </div>
        <a
          href="/xenia/email"
          className="inline-flex items-center gap-1 rounded-md border border-gray-200 bg-white px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50"
        >
          ← Email Dashboard
        </a>
      </div>

      {error ? (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Unable to reach the Xenia service. Ensure it is running on port 5035.
        </div>
      ) : settings ? (
        <div className="space-y-4">
          {/* Connection Policy */}
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <h3 className="text-sm font-semibold text-gray-700">Connection Policy</h3>
            </div>
            <dl className="divide-y divide-gray-100">
              <SettingsRow
                label="Connection Timeout"
                value={`${settings.connectionTimeoutSeconds}s`}
                detail="Maximum time allowed for a connectivity validation probe."
              />
              <SettingsRow
                label="Allowed Ports"
                value={settings.allowedPorts || 'Default (993, 995, 443)'}
                detail="CSV list of TCP ports permitted for IMAP/POP3 sources."
              />
              <SettingsRow
                label="Require TLS"
                value={settings.requireTls ? 'Yes (enforced)' : 'No (not enforced)'}
                detail="When enabled, all sources must use TLS."
                badge={settings.requireTls ? 'good' : 'warn'}
              />
              <SettingsRow
                label="Allow Custom Hosts"
                value={settings.allowCustomHosts ? 'Allowed' : 'Blocked (provider list only)'}
                detail="Whether tenants may configure arbitrary incoming mail server hosts."
                badge={settings.allowCustomHosts ? 'warn' : 'good'}
              />
              <SettingsRow
                label="Allowed Provider Types"
                value={settings.allowedProviderTypes || 'All providers'}
                detail="CSV list of permitted email provider types. Empty = all allowed."
              />
            </dl>
          </div>

          {/* SSRF Protection */}
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <h3 className="text-sm font-semibold text-gray-700">SSRF Protection</h3>
            </div>
            <dl className="divide-y divide-gray-100">
              <SettingsRow
                label="SSRF Policy Mode"
                value={settings.ssrfPolicyMode}
                detail="Strict: DNS-resolved IPs checked against all blocked ranges. Permissive: hostname-only check (not available in production)."
                badge={settings.ssrfPolicyMode === 'Strict' ? 'good' : 'warn'}
              />
            </dl>
            <div className="px-4 py-3 bg-blue-50 border-t border-blue-100">
              <p className="text-xs text-blue-700">
                <strong>SSRF protection is always active.</strong> Connectors resolve DNS and validate all returned IP
                addresses against 16 blocked IPv4 ranges and full IPv6 private/reserved ranges.
                The resolved IP is pinned for the connection to prevent DNS rebinding.
              </p>
            </div>
          </div>

          {/* Validation */}
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <h3 className="text-sm font-semibold text-gray-700">Validation</h3>
            </div>
            <dl className="divide-y divide-gray-100">
              <SettingsRow
                label="Retry Limit"
                value={`${settings.validationRetryLimit} retr${settings.validationRetryLimit !== 1 ? 'ies' : 'y'}`}
                detail="Maximum number of validation attempts before recording a failure."
              />
              <SettingsRow
                label="History Retention"
                value={`${settings.validationHistoryRetentionDays} days`}
                detail="How long validation history records are retained per source."
              />
              <SettingsRow
                label="Default Source State"
                value={settings.defaultSourceEnabled ? 'Enabled on create' : 'Disabled on create'}
                detail="Whether newly-created sources are enabled by default."
              />
            </dl>
          </div>

          {/* Metadata */}
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <h3 className="text-sm font-semibold text-gray-700">Record Info</h3>
            </div>
            <dl className="divide-y divide-gray-100">
              <SettingsRow label="Settings ID" value={settings.id} detail="Unique settings record identifier." />
              <SettingsRow label="Version" value={String(settings.version)} detail="Optimistic concurrency version." />
              <SettingsRow
                label="Last Updated"
                value={new Date(settings.updatedAtUtc).toLocaleString()}
                detail="When these settings were last modified."
              />
            </dl>
          </div>

          <div className="rounded-lg border border-amber-200 bg-amber-50 p-3">
            <p className="text-xs text-amber-800 font-medium">Read-only view</p>
            <p className="text-xs text-amber-700 mt-0.5">
              Settings are managed via the Xenia API (<code className="font-mono">PUT /email/settings</code>).
              Updates require the current version number to prevent concurrent modification conflicts.
            </p>
          </div>
        </div>
      ) : (
        <div className="rounded-lg border border-gray-200 bg-white p-8 text-center">
          <p className="text-sm text-gray-500">Loading settings…</p>
        </div>
      )}
    </div>
  );
}

function SettingsRow({
  label,
  value,
  detail,
  badge,
}: {
  label: string;
  value: string;
  detail?: string;
  badge?: 'good' | 'warn';
}) {
  return (
    <div className="flex items-start justify-between px-4 py-3 gap-4">
      <div className="min-w-0">
        <dt className="text-xs font-medium text-gray-500 uppercase tracking-wide">{label}</dt>
        {detail && <dd className="text-xs text-gray-400 mt-0.5">{detail}</dd>}
      </div>
      <div className="flex-shrink-0 flex items-center gap-1.5">
        {badge && (
          <span
            className={`h-1.5 w-1.5 rounded-full ${
              badge === 'good' ? 'bg-green-500' : 'bg-amber-400'
            }`}
          />
        )}
        <dd className="text-sm font-medium text-gray-900 text-right">{value}</dd>
      </div>
    </div>
  );
}
