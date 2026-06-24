'use client';

import { useState, useTransition } from 'react';
import { tenantClientApi } from '@/lib/tenant-client-api';

// Common IANA timezones grouped by region for the dropdown.
const TIMEZONE_OPTIONS: { label: string; value: string }[] = [
  // United States
  { label: 'Pacific Time — Los Angeles (PT)',      value: 'America/Los_Angeles' },
  { label: 'Mountain Time — Denver (MT)',           value: 'America/Denver' },
  { label: 'Mountain Time — Phoenix (no DST)',      value: 'America/Phoenix' },
  { label: 'Central Time — Chicago (CT)',           value: 'America/Chicago' },
  { label: 'Eastern Time — New York (ET)',          value: 'America/New_York' },
  { label: 'Alaska Time — Anchorage (AK)',          value: 'America/Anchorage' },
  { label: 'Hawaii Time — Honolulu (HT)',           value: 'Pacific/Honolulu' },
  // Canada
  { label: 'Pacific Time — Vancouver (PT)',         value: 'America/Vancouver' },
  { label: 'Eastern Time — Toronto (ET)',           value: 'America/Toronto' },
  // International
  { label: 'UTC',                                  value: 'UTC' },
  { label: 'GMT — London',                         value: 'Europe/London' },
  { label: 'Central European Time — Paris (CET)',  value: 'Europe/Paris' },
  { label: 'Indian Standard Time — Mumbai (IST)',  value: 'Asia/Kolkata' },
  { label: 'China Standard Time — Shanghai (CST)', value: 'Asia/Shanghai' },
  { label: 'Philippine Time — Manila (PHT)',       value: 'Asia/Manila' },
  { label: 'Japan Standard Time — Tokyo (JST)',    value: 'Asia/Tokyo' },
  { label: 'Australian Eastern Time — Sydney (AET)', value: 'Australia/Sydney' },
];

export function TenantTimezoneCard({
  tenantId,
  initialTimezone,
}: {
  tenantId:        string;
  initialTimezone: string;
}) {
  const [selected, setSelected]   = useState(initialTimezone);
  const [saved,    setSaved]      = useState(initialTimezone);
  const [error,    setError]      = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  const isDirty = selected !== saved;

  const save = () => {
    setError(null);
    startTransition(async () => {
      try {
        await tenantClientApi.updateTimezoneSetting(tenantId, selected);
        setSaved(selected);
      } catch {
        setError('Failed to save timezone. Please try again.');
      }
    });
  };

  const savedLabel = TIMEZONE_OPTIONS.find(o => o.value === saved)?.label ?? saved;

  return (
    <div className="bg-white border border-gray-200 rounded-xl px-6 py-6">
      <div className="mb-5">
        <h2 className="text-sm font-semibold text-gray-900 flex items-center gap-2">
          <i className="ri-time-line text-gray-400" />
          Timezone
        </h2>
        <p className="text-xs text-gray-500 mt-1">
          All dates and timestamps across your tenant will be displayed in this timezone.
        </p>
      </div>

      <div className="mb-4">
        <label htmlFor="timezone-select" className="block text-xs font-medium text-gray-700 mb-1.5">
          Display timezone
        </label>
        <select
          id="timezone-select"
          value={selected}
          onChange={e => setSelected(e.target.value)}
          disabled={isPending}
          className="w-full text-sm border border-gray-300 rounded-lg px-3 py-2 bg-white focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {TIMEZONE_OPTIONS.map(opt => (
            <option key={opt.value} value={opt.value}>
              {opt.label}
            </option>
          ))}
        </select>
      </div>

      {error && (
        <div className="mb-4 rounded-lg bg-red-50 border border-red-200 px-4 py-3">
          <p className="text-xs text-red-700">{error}</p>
        </div>
      )}

      <div className="flex items-center justify-between">
        <p className="text-xs text-gray-400">
          {isDirty
            ? 'Unsaved changes — click Save to apply.'
            : `Active: ${savedLabel}`}
        </p>
        <button
          type="button"
          disabled={!isDirty || isPending}
          onClick={save}
          className="inline-flex items-center gap-1.5 px-4 py-2 text-xs font-semibold rounded-lg bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          {isPending ? (
            <>
              <i className="ri-loader-4-line animate-spin" />
              Saving…
            </>
          ) : (
            <>
              <i className="ri-save-line" />
              Save
            </>
          )}
        </button>
      </div>
    </div>
  );
}
