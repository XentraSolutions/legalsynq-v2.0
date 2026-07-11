import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import { getEmailOperationalSettings, type EmailOperationalSettings } from '@/lib/xenia-email-api';

export const dynamic = 'force-dynamic';

export default async function EmailRetentionSettingsPage() {
  await requirePlatformAdmin();

  const jar   = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';

  let settings: EmailOperationalSettings | null = null;
  let serviceError = false;

  try {
    settings = await getEmailOperationalSettings(token);
  } catch {
    serviceError = true;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h2 className="text-xl font-semibold text-gray-900">Retention &amp; Operational Settings</h2>
          <p className="text-sm text-gray-500 mt-1">Configure data lifecycle, alert thresholds, and operational behaviour.</p>
        </div>
        <a href="/xenia/email/retention" className="text-xs text-indigo-600 hover:text-indigo-700">
          ← Retention
        </a>
      </div>

      {serviceError ? (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Unable to reach the Xenia service.
        </div>
      ) : settings ? (
        <div className="space-y-6">
          {/* Retention policy */}
          <SettingsSection title="Data Retention Policy">
            <SettingsRow
              label="Retention Enabled"
              description="Enable destructive retention execution. When disabled, only dry-run analysis is available."
              value={settings.retentionEnabled ? 'Enabled' : 'Disabled (Dry-run only)'}
              accent={settings.retentionEnabled ? undefined : 'amber'}
            />
            <SettingsRow
              label="Legal Hold"
              description="When active, all deletion operations are blocked regardless of retention settings."
              value={settings.legalHoldEnabled ? 'Active' : 'Inactive'}
              accent={settings.legalHoldEnabled ? 'red' : undefined}
            />
            <SettingsRow
              label="Message Metadata Retention"
              value={`${settings.messageMetadataRetentionDays} days`}
            />
            <SettingsRow
              label="Message Body Retention"
              value={`${settings.messageBodyRetentionDays} days`}
              description="Message bodies (content) are purged before metadata."
            />
            <SettingsRow
              label="Ingestion Run Retention"
              value={`${settings.ingestionRunRetentionDays} days`}
            />
            <SettingsRow
              label="Alert Retention"
              value={`${settings.alertRetentionDays} days`}
            />
            <SettingsRow
              label="Attachment Reference Retention"
              value={`${settings.attachmentReferenceRetentionDays ?? '365'} days`}
            />
            <SettingsRow
              label="Purge Batch Size"
              value={String(settings.purgeBatchSize)}
              description="Records processed per transaction during a retention run."
            />
            <SettingsRow
              label="Default Dry Run"
              value={settings.retentionDryRunDefault ? 'Yes' : 'No'}
            />
          </SettingsSection>

          {/* Operational thresholds */}
          <SettingsSection title="Operational Thresholds">
            <SettingsRow
              label="Source Failure Alert Threshold"
              value={`${settings.sourceFailureAlertThreshold} consecutive failures`}
            />
            <SettingsRow
              label="Stale Sync Threshold"
              value={`${settings.staleSyncThresholdMinutes} minutes`}
              description="Time since last successful sync before a stale alert is raised."
            />
            <SettingsRow
              label="Lock Warning Threshold"
              value={`${settings.lockWarningThresholdMinutes} minutes`}
            />
            <SettingsRow
              label="Maximum Retry Count"
              value={String(settings.maximumRetryCount)}
            />
            <SettingsRow
              label="Cancellation Timeout"
              value={`${settings.cancellationTimeoutSeconds}s`}
            />
          </SettingsSection>

          {/* Operational preferences */}
          <SettingsSection title="Operational Preferences">
            <SettingsRow
              label="Metrics Enabled"
              value={settings.metricsEnabled ? 'Yes' : 'No'}
            />
            <SettingsRow
              label="Notification Alerts"
              value={settings.notificationAlertsEnabled ? 'Enabled' : 'Disabled'}
            />
            <SettingsRow
              label="Default Dashboard Range"
              value={`${settings.defaultDashboardRangeDays} days`}
            />
          </SettingsSection>

          <div className="text-xs text-gray-400">
            Last updated:{' '}
            {settings.updatedBy ? `by ${settings.updatedBy} — ` : ''}
            {new Date(settings.updatedAt).toLocaleString()}
          </div>
        </div>
      ) : null}
    </div>
  );
}

function SettingsSection({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
      <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
        <h3 className="text-sm font-semibold text-gray-900">{title}</h3>
      </div>
      <dl className="divide-y divide-gray-100">{children}</dl>
    </div>
  );
}

function SettingsRow({
  label,
  value,
  description,
  accent,
}: {
  label: string;
  value: string;
  description?: string;
  accent?: 'red' | 'amber';
}) {
  const valueClass =
    accent === 'red'   ? 'text-red-600 font-semibold' :
    accent === 'amber' ? 'text-amber-600 font-semibold' :
    'text-gray-900';

  return (
    <div className="grid grid-cols-3 gap-4 px-4 py-3">
      <dt className="text-sm font-medium text-gray-600">
        {label}
        {description && (
          <p className="text-xs font-normal text-gray-400 mt-0.5">{description}</p>
        )}
      </dt>
      <dd className={`text-sm col-span-2 ${valueClass}`}>{value}</dd>
    </div>
  );
}
