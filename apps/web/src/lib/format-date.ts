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
