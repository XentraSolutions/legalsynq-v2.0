// Cached Intl.DateTimeFormat instances keyed by timezone to avoid repeated
// object allocation in list views with many rows (Pete's optimization).
const _dateFormatters = new Map<string, Intl.DateTimeFormat>();
const _timeFormatters = new Map<string, Intl.DateTimeFormat>();

function getDateFormatter(timezone: string): Intl.DateTimeFormat {
  let fmt = _dateFormatters.get(timezone);
  if (!fmt) {
    fmt = new Intl.DateTimeFormat('en-US', {
      month:    'short',
      day:      'numeric',
      year:     'numeric',
      timeZone: timezone,
    });
    _dateFormatters.set(timezone, fmt);
  }
  return fmt;
}

function getTimeFormatter(timezone: string): Intl.DateTimeFormat {
  let fmt = _timeFormatters.get(timezone);
  if (!fmt) {
    fmt = new Intl.DateTimeFormat('en-US', {
      hour:     'numeric',
      minute:   '2-digit',
      hour12:   true,
      timeZone: timezone,
    });
    _timeFormatters.set(timezone, fmt);
  }
  return fmt;
}

export function formatTimestamp(
  iso: string,
  timezone = 'UTC',
): { date: string; time: string } {
  const d = new Date(iso);
  return {
    date: getDateFormatter(timezone).format(d),
    time: getTimeFormatter(timezone).format(d),
  };
}

export function formatShortTimestamp(iso: string, timezone = 'UTC'): string {
  const { date, time } = formatTimestamp(iso, timezone);
  return `${date}, ${time}`;
}

export function formatDateOnly(
  value: string,
  options: Intl.DateTimeFormatOptions = { month: 'short', day: 'numeric', year: 'numeric' },
): string {
  const trimmed = value.trim();
  if (!trimmed) return '';

  const isoDatePrefixMatch = /^(\d{4})-(\d{2})-(\d{2})(?:T.*)?$/.exec(trimmed);
  if (isoDatePrefixMatch) {
    const [, year, month, day] = isoDatePrefixMatch;
    const utcDate = new Date(Date.UTC(Number(year), Number(month) - 1, Number(day)));
    return utcDate.toLocaleDateString('en-US', { ...options, timeZone: 'UTC' });
  }

  const parsed = new Date(trimmed);
  if (Number.isNaN(parsed.getTime())) return trimmed;
  return parsed.toLocaleDateString('en-US', { ...options, timeZone: 'UTC' });
}

// ─────────────────────────────────────────────────────────────────────────
// Legacy (lien/case/servicing) date formatting.
//
// The functions above are shared across the whole app (careconnect,
// notifications, workflow, audit, etc.) — changing their defaults or
// parsing behavior would silently change output for product areas owned
// by other teams. The lien/case/servicing domain wants its own, stricter
// rules (legacy MM/DD/YYYY format, no day-shifting on pure dates, and a
// documented UTC assumption for naive datetimes), so those live here as
// separate, explicitly-named functions instead of changing the shared
// ones. Only lien/case/servicing code should use these.
// ─────────────────────────────────────────────────────────────────────────

// The lien API is expected to serialize timestamps with an explicit UTC
// 'Z' suffix — lien backend entities consistently use DateTime.UtcNow
// (see apps/services/liens/Liens.Domain/Entities/*.cs, fields named
// *AtUtc), and System.Text.Json's default serialization appends 'Z' for
// DateTimeKind.Utc. If a timestamp ever arrives WITHOUT an explicit
// offset (e.g. "2024-01-15T10:30:00"), we assume it's UTC rather than
// letting the browser fall back to interpreting it as local time. This
// is a defensive assumption, not an observed behavior — if the lien
// backend's serialization convention ever changes (e.g. a field switches
// to DateTimeKind.Unspecified or starts sending naive local time), this
// needs to be revisited.
function parseLegacyApiDateTime(value: string): Date {
  const hasTimeComponent = value.includes('T');
  const hasExplicitOffset = /Z$|[+-]\d{2}:?\d{2}$/.test(value);
  if (hasTimeComponent && !hasExplicitOffset) {
    return new Date(`${value}Z`);
  }
  return new Date(value);
}

// Pure calendar-date values (e.g. "2024-01-15", no time component) don't
// carry a timezone-meaningful instant — they represent one specific day,
// full stop. Extract the Y-M-D parts directly and format without running
// them through any timezone conversion, so the day never appears to move
// forward/back depending on the viewer's or tenant's timezone offset.
const LEGACY_DATE_ONLY_PATTERN = /^(\d{4})-(\d{2})-(\d{2})$/;

// Legacy-system default: MM/DD/YYYY.
const LEGACY_DEFAULT_DATE_OPTIONS: Intl.DateTimeFormatOptions = {
  month: '2-digit',
  day: '2-digit',
  year: 'numeric',
};

/**
 * Date-only formatter for the lien/case/servicing domain. Resolves the
 * calendar date in the given timezone for real timestamps, but never
 * timezone-shifts a pure calendar-date value (no time component).
 * Defaults to legacy-system MM/DD/YYYY.
 */
export function formatLegacyDateOnly(
  iso: string,
  timezone: string = 'UTC',
  options: Intl.DateTimeFormatOptions = LEGACY_DEFAULT_DATE_OPTIONS,
): string {
  const trimmed = iso.trim();
  if (!trimmed) return '';

  const dateOnlyMatch = LEGACY_DATE_ONLY_PATTERN.exec(trimmed);
  if (dateOnlyMatch) {
    const [, year, month, day] = dateOnlyMatch;
    const utcDate = new Date(Date.UTC(Number(year), Number(month) - 1, Number(day)));
    return utcDate.toLocaleDateString('en-US', { ...options, timeZone: 'UTC' });
  }

  const parsed = parseLegacyApiDateTime(trimmed);
  if (Number.isNaN(parsed.getTime())) throw new Error(`Invalid date: ${trimmed}`);
  return parsed.toLocaleDateString('en-US', { ...options, timeZone: timezone });
}

/**
 * Datetime formatter for the lien/case/servicing domain. Same timezone
 * and UTC-assumption handling as formatLegacyDateOnly, plus a time
 * component. Defaults to legacy-system MM/DD/YYYY for the date part.
 * Throws on an unparseable value, matching formatLegacyDateOnly, so
 * callers (e.g. DateDisplay) can catch and render a fallback.
 */
export function formatLegacyTimestamp(
  iso: string,
  timezone: string = 'UTC',
): { date: string; time: string } {
  const d = parseLegacyApiDateTime(iso);
  if (Number.isNaN(d.getTime())) throw new Error(`Invalid date: ${iso}`);
  return {
    date: d.toLocaleDateString('en-US', { ...LEGACY_DEFAULT_DATE_OPTIONS, timeZone: timezone }),
    time: d.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit', hour12: true, timeZone: timezone }),
  };
}

export function formatLegacyShortTimestamp(iso: string, timezone: string = 'UTC'): string {
  const { date, time } = formatLegacyTimestamp(iso, timezone);
  return time ? `${date}, ${time}` : date;
}
