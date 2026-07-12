import { requirePlatformAdmin } from '@/lib/auth-guards';

export const dynamic = 'force-dynamic';

export default async function XeniaSettingsPage() {
  await requirePlatformAdmin();

  return (
    <div>
      <div className="mb-6">
        <h2 className="text-xl font-semibold text-gray-900">Settings</h2>
        <p className="text-sm text-gray-500 mt-1">
          Xenia platform configuration. Global, tenant, and module-level settings.
        </p>
      </div>

      <div className="space-y-6">
        {/* Global configuration placeholder */}
        <SettingsSection
          title="Global Configuration"
          description="Platform-wide Xenia settings that apply to all tenants and modules."
          placeholder="Global configuration management will be available in a future release."
        />

        {/* Tenant configuration placeholder */}
        <SettingsSection
          title="Tenant Configuration"
          description="Per-tenant Xenia settings and overrides."
          placeholder="Tenant configuration management will be available in a future release."
        />

        {/* Module configuration placeholder */}
        <SettingsSection
          title="Module Configuration"
          description="Per-module and tenant-module configuration overrides."
          placeholder="Module configuration management will be available in a future release."
        />

        {/* Architecture note */}
        <div className="rounded-lg border border-gray-200 bg-gray-50 p-5">
          <h3 className="text-sm font-semibold text-gray-700 mb-2">Configuration Architecture</h3>
          <p className="text-sm text-gray-600 mb-3">
            Xenia uses a layered configuration system with the following precedence (lowest → highest):
          </p>
          <ol className="list-decimal list-inside space-y-1 text-sm text-gray-600">
            <li>Application defaults</li>
            <li>Environment configuration</li>
            <li>Global persisted configuration</li>
            <li>Tenant configuration</li>
            <li>Module configuration</li>
            <li>Tenant-module override (highest precedence)</li>
          </ol>
          <p className="text-xs text-gray-400 mt-3">
            Secret values are never returned by the configuration API.
            Use secret references (secret manager keys) rather than plaintext secrets.
          </p>
        </div>
      </div>
    </div>
  );
}

function SettingsSection({
  title,
  description,
  placeholder,
}: {
  title: string;
  description: string;
  placeholder: string;
}) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-5">
      <h3 className="text-sm font-semibold text-gray-700">{title}</h3>
      <p className="text-xs text-gray-500 mt-0.5 mb-4">{description}</p>
      <div className="rounded border border-dashed border-gray-300 bg-gray-50 p-6 text-center">
        <p className="text-sm text-gray-400 italic">{placeholder}</p>
        <p className="text-xs text-gray-300 mt-1">
          Read-only configuration viewer — no active controls in this foundation release.
        </p>
      </div>
    </div>
  );
}
