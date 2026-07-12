import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import {
  getEmailOperationalSettings,
  updateEmailOperationalSettings,
  type EmailOperationalSettings,
} from '@/lib/xenia-email-api';
import { revalidatePath } from 'next/cache';

export const dynamic = 'force-dynamic';

export default async function EmailOperationalSettingsPage() {
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

  async function handleSave(formData: FormData) {
    'use server';
    const jar2 = await cookies();
    const tok  = jar2.get(SESSION_COOKIE_NAME)?.value ?? '';
    const patch: Partial<EmailOperationalSettings> = {
      maximumRetryCount:          Number(formData.get('maximumRetryCount') ?? 3),
      staleSyncThresholdMinutes:  Number(formData.get('staleSyncThresholdMinutes') ?? 60),
      lockWarningThresholdMinutes:Number(formData.get('lockWarningThresholdMinutes') ?? 5),
      sourceFailureAlertThreshold:Number(formData.get('sourceFailureAlertThreshold') ?? 3),
      defaultDashboardRangeDays:  Number(formData.get('defaultDashboardRangeDays') ?? 7),
    };
    try { await updateEmailOperationalSettings(tok, patch); } catch { /* handled gracefully */ }
    revalidatePath('/xenia/email/settings/operations');
  }

  if (serviceError) {
    return (
      <div className="p-8">
        <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-red-700">
          Unable to load operational settings. The Xenia service may be unavailable.
        </div>
      </div>
    );
  }

  const s = settings;

  return (
    <div className="p-8 max-w-2xl space-y-8">
      <div>
        <h1 className="text-2xl font-bold text-gray-900">Operational Settings</h1>
        <p className="mt-1 text-sm text-gray-500">
          Configure alert thresholds and operational behaviour for the Xenia Email module.
        </p>
      </div>

      <form action={handleSave} className="space-y-6">
        <Section title="Alert Thresholds">
          <NumberRow
            name="sourceFailureAlertThreshold"
            label="Source Failure Alert Threshold"
            hint="Consecutive failures before an alert fires"
            defaultValue={s?.sourceFailureAlertThreshold ?? 3}
          />
          <NumberRow
            name="staleSyncThresholdMinutes"
            label="Stale Sync Threshold (minutes)"
            hint="Minutes without a successful sync before source is considered stale"
            defaultValue={s?.staleSyncThresholdMinutes ?? 60}
          />
          <NumberRow
            name="lockWarningThresholdMinutes"
            label="Lock Warning Threshold (minutes)"
            hint="Minutes a sync lock can be held before a warning alert fires"
            defaultValue={s?.lockWarningThresholdMinutes ?? 5}
          />
        </Section>

        <Section title="Sync Behaviour">
          <NumberRow
            name="maximumRetryCount"
            label="Maximum Retry Count"
            hint="Max automatic retries per ingestion run"
            defaultValue={s?.maximumRetryCount ?? 3}
          />
          <NumberRow
            name="defaultDashboardRangeDays"
            label="Default Dashboard Range (days)"
            hint="Default time range shown on the operations dashboard"
            defaultValue={s?.defaultDashboardRangeDays ?? 7}
          />
        </Section>

        <div className="flex gap-3 pt-2">
          <button
            type="submit"
            className="px-5 py-2 text-sm font-medium bg-indigo-600 text-white rounded-md hover:bg-indigo-700"
          >
            Save Settings
          </button>
        </div>
      </form>

      {s && (
        <div className="bg-gray-50 border border-gray-200 rounded-lg p-4 text-xs text-gray-500">
          Last updated: {
            s.updatedAt ? new Date(s.updatedAt).toLocaleString() : '—'
          }
        </div>
      )}
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div>
      <h2 className="text-sm font-semibold text-gray-700 uppercase tracking-wide mb-3">{title}</h2>
      <div className="bg-white border border-gray-200 rounded-lg divide-y divide-gray-100">
        {children}
      </div>
    </div>
  );
}

function NumberRow({
  name, label, hint, defaultValue,
}: {
  name: string; label: string; hint: string; defaultValue: number;
}) {
  return (
    <div className="px-4 py-3 flex items-start justify-between gap-4">
      <div>
        <label htmlFor={name} className="text-sm font-medium text-gray-700">{label}</label>
        <p className="text-xs text-gray-500 mt-0.5">{hint}</p>
      </div>
      <input
        id={name}
        name={name}
        type="number"
        defaultValue={defaultValue}
        min={0}
        className="w-24 text-sm border border-gray-300 rounded-md px-2 py-1.5 text-right focus:outline-none focus:ring-2 focus:ring-indigo-500"
      />
    </div>
  );
}
