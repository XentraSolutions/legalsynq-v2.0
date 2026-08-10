'use client';

import React from 'react';
import { useTimezone } from '@/lib/use-timezone';
import { formatLegacyShortTimestamp, formatLegacyDateOnly } from '@/lib/format-date';

type DateFormat = 'datetime' | 'date';

interface DateDisplayProps {
  value?: string | Date | null;
  format?: DateFormat;
  timezone?: string;
  fallback?: string;
}

/**
 * DateDisplay — centralized component for rendering dates with consistent formatting.
 *
 * - Handles null/undefined gracefully
 * - Uses tenant timezone (platform routes) or accepts explicit timezone override
 * - Supports 'datetime' (date + time) and 'date' (date only) formats
 *
 * Platform routes (inside `(platform)` layout):
 *   <DateDisplay value={iso} format="date" />
 *   // Automatically uses tenant timezone from context
 *
 * Non-platform routes (need timezone from hook):
 *   const tz = useBrowserTimezone();
 *   <DateDisplay value={iso} format="date" timezone={tz} />
 */
export function DateDisplay({
  value,
  format = 'datetime',
  timezone: explicitTimezone,
  fallback = '—',
}: DateDisplayProps) {
  // Use provided timezone, or fall back to tenant timezone from context
  const tenantTimezone = useTimezone();
  const effectiveTimezone = explicitTimezone ?? tenantTimezone;

  // Normalize value to ISO string
  let isoString: string | null = null;
  if (value) {
    if (typeof value === 'string') {
      isoString = value;
    } else if (value instanceof Date) {
      isoString = value.toISOString();
    }
  }

  if (!isoString) {
    return <>{fallback}</>;
  }

  try {
    if (format === 'datetime') {
      return <>{formatLegacyShortTimestamp(isoString, effectiveTimezone)}</>;
    } else if (format === 'date') {
      return <>{formatLegacyDateOnly(isoString, effectiveTimezone)}</>;
    }
    return <>{fallback}</>;
  } catch {
    // If formatting fails, show the fallback
    return <>{fallback}</>;
  }
}
