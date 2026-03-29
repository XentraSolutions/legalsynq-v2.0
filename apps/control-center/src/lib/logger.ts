/**
 * logger.ts — Control Center structured server-side logger.
 *
 * All logging in the Control Center flows through this module. It provides
 * three public functions — logInfo, logWarn, logError — that emit structured
 * log entries with a consistent shape in every environment.
 *
 * ── Environments ─────────────────────────────────────────────────────────────
 *
 *   development  →  human-readable prefix lines to stderr/stdout.
 *                   Formatted for easy reading in the Next.js dev console.
 *
 *   production   →  newline-delimited JSON (NDJSON) to stdout.
 *                   One JSON object per log entry, compatible with any
 *                   structured log aggregator (CloudWatch, Datadog, GCP
 *                   Logging, etc.).
 *
 * ── Log entry shape ──────────────────────────────────────────────────────────
 *
 *   {
 *     level:      "INFO" | "WARN" | "ERROR"
 *     message:    string          — short event label, e.g. "api.request.start"
 *     timestamp:  ISO-8601        — UTC wall-clock time
 *     service:    "control-center"
 *     // optional fields from LogMeta:
 *     requestId?         string   — X-Request-Id propagated to API gateway
 *     endpoint?          string   — URL path (never includes query secrets)
 *     method?            string   — HTTP verb
 *     durationMs?        number   — round-trip latency
 *     status?            number   — HTTP response status
 *     tenantId?          string   — active tenant context (if set)
 *     tenantCode?        string   — active tenant short code (if set)
 *     impersonatedUserId? string  — impersonated user (if active)
 *     impersonatedUserEmail? string
 *     // error fields (logError only):
 *     errorName?   string
 *     errorMessage? string
 *     errorStack?  string         — only in development
 *   }
 *
 * TODO: integrate with Datadog / OpenTelemetry
 * TODO: send logs to centralized logging service
 * TODO: add log sampling for high-volume INFO events
 * TODO: add correlation tracing (trace-id across microservices)
 */

// ── Env ───────────────────────────────────────────────────────────────────────

const IS_DEV  = process.env.NODE_ENV !== 'production';
const SERVICE = 'control-center' as const;

// ── Types ─────────────────────────────────────────────────────────────────────

/** Level union */
export type LogLevel = 'INFO' | 'WARN' | 'ERROR';

/**
 * LogMeta — well-known structured fields accepted by all log functions.
 *
 * Every field is optional so callers can include only what they know.
 * Unknown extra fields are accepted via the index signature and are
 * forwarded verbatim to the log output.
 *
 * Sensitive values (passwords, tokens, raw cookie values) must NEVER
 * appear here. Include only opaque identifiers such as requestId, tenantId,
 * userId — never credential payloads.
 */
export interface LogMeta {
  requestId?:             string;
  endpoint?:              string;
  method?:                string;
  durationMs?:            number;
  status?:                number;
  tenantId?:              string;
  tenantCode?:            string;
  impersonatedUserId?:    string;
  impersonatedUserEmail?: string;
  [key: string]:          unknown;
}

/** Full log entry shape (internal) */
interface LogEntry extends LogMeta {
  level:        LogLevel;
  message:      string;
  timestamp:    string;
  service:      typeof SERVICE;
  errorName?:    string;
  errorMessage?: string;
  errorStack?:   string;
}

// ── Serialise error ───────────────────────────────────────────────────────────

/**
 * serialiseError — extracts loggable fields from an unknown thrown value.
 *
 * Avoids leaking raw stack traces in production while still capturing
 * the essential name + message for alerting and search.
 */
function serialiseError(err: unknown): {
  errorName:     string;
  errorMessage:  string;
  errorStack?:   string;
} {
  if (err instanceof Error) {
    return {
      errorName:    err.name    || 'Error',
      errorMessage: err.message || String(err),
      // Stack traces are valuable in dev; omit in prod to reduce log volume
      // and prevent accidental source-path leakage.
      errorStack:   IS_DEV ? (err.stack ?? undefined) : undefined,
    };
  }
  return {
    errorName:    'UnknownError',
    errorMessage: String(err ?? 'unknown error'),
  };
}

// ── Output ────────────────────────────────────────────────────────────────────

/** ANSI colour codes — only used in dev */
const CLR = {
  reset:  '\x1b[0m',
  grey:   '\x1b[90m',
  cyan:   '\x1b[36m',
  yellow: '\x1b[33m',
  red:    '\x1b[31m',
  bold:   '\x1b[1m',
} as const;

/**
 * emit — the single write path for all log entries.
 *
 * dev  → human-readable single line to console
 * prod → JSON object to stdout (one line)
 *
 * TODO: integrate with Datadog / OpenTelemetry
 * TODO: send logs to centralized logging service
 */
function emit(entry: LogEntry): void {
  if (IS_DEV) {
    emitDev(entry);
  } else {
    emitProd(entry);
  }
}

/**
 * emitDev — pretty-prints a log entry to the developer console.
 *
 * Format:
 *   [CC] INFO  HH:MM:SS.mmm message  method endpoint  +NNNms  req=xxx  tenant=xxx
 */
function emitDev(entry: LogEntry): void {
  const time  = entry.timestamp.slice(11, 23); // HH:MM:SS.mmm
  const level = entry.level.padEnd(5);

  let levelColour: string;
  let logFn: typeof console.log;
  switch (entry.level) {
    case 'ERROR':
      levelColour = CLR.red    + CLR.bold;
      logFn       = console.error;
      break;
    case 'WARN':
      levelColour = CLR.yellow + CLR.bold;
      logFn       = console.warn;
      break;
    default:
      levelColour = CLR.cyan;
      logFn       = console.log;
  }

  // Build context suffix tokens
  const tokens: string[] = [];
  if (entry.method)               tokens.push(`${entry.method}`);
  if (entry.endpoint)             tokens.push(`${entry.endpoint}`);
  if (entry.status !== undefined) tokens.push(`HTTP ${entry.status}`);
  if (entry.durationMs !== undefined) tokens.push(`+${entry.durationMs}ms`);
  if (entry.requestId)            tokens.push(`${CLR.grey}req=${entry.requestId}${CLR.reset}`);
  if (entry.tenantId)             tokens.push(`${CLR.grey}tenant=${entry.tenantCode ?? entry.tenantId}${CLR.reset}`);
  if (entry.impersonatedUserId)   tokens.push(`${CLR.grey}impersonating=${entry.impersonatedUserEmail ?? entry.impersonatedUserId}${CLR.reset}`);
  if (entry.errorMessage)         tokens.push(`"${entry.errorMessage}"`);

  const suffix = tokens.length ? `  ${tokens.join('  ')}` : '';

  logFn(
    `${CLR.grey}[CC]${CLR.reset} ${levelColour}${level}${CLR.reset} ${CLR.grey}${time}${CLR.reset}  ${entry.message}${suffix}`,
  );

  // In dev, also log the stack if present
  if (entry.errorStack && entry.level === 'ERROR') {
    console.error(`${CLR.grey}${entry.errorStack}${CLR.reset}`);
  }
}

/**
 * emitProd — emits a JSON log line to stdout.
 *
 * Produces NDJSON (newline-delimited JSON): one compact JSON object per line,
 * suitable for CloudWatch Logs, Datadog, GCP Logging, and similar services.
 *
 * TODO: integrate with Datadog / OpenTelemetry
 * TODO: send logs to centralized logging service
 */
function emitProd(entry: LogEntry): void {
  // Build a clean object — strip undefined values so JSON is compact
  const out: Record<string, unknown> = {};
  for (const [k, v] of Object.entries(entry)) {
    if (v !== undefined) out[k] = v;
  }
  // eslint-disable-next-line no-console
  process.stdout.write(JSON.stringify(out) + '\n');
}

// ── Public API ────────────────────────────────────────────────────────────────

/**
 * logInfo — emit an INFO-level log entry.
 *
 * Use for normal operational events:
 *   - request start / success
 *   - cache hit / miss
 *   - context switches
 *
 * @param message  Short event label (e.g. "api.request.start")
 * @param meta     Optional structured fields (requestId, endpoint, …)
 */
export function logInfo(message: string, meta?: LogMeta): void {
  emit({
    level:     'INFO',
    message,
    timestamp: new Date().toISOString(),
    service:   SERVICE,
    ...meta,
  });
}

/**
 * logWarn — emit a WARN-level log entry.
 *
 * Use for degraded-but-recoverable conditions:
 *   - unexpected field values from the API
 *   - missing optional context
 *   - slow responses (above threshold)
 *
 * @param message  Short event label (e.g. "api.slow_response")
 * @param meta     Optional structured fields
 */
export function logWarn(message: string, meta?: LogMeta): void {
  emit({
    level:     'WARN',
    message,
    timestamp: new Date().toISOString(),
    service:   SERVICE,
    ...meta,
  });
}

/**
 * logError — emit an ERROR-level log entry, optionally including a serialised
 * error object.
 *
 * Use for failures that require investigation:
 *   - non-2xx API responses (4xx client errors, 5xx server errors)
 *   - network-level failures (DNS, connection refused, timeout)
 *   - unexpected exceptions in Server Actions
 *
 * Does NOT re-throw the error — callers remain responsible for propagation.
 *
 * @param message  Short event label (e.g. "api.request.error")
 * @param error    The thrown value (Error, ApiError, or unknown)
 * @param meta     Optional structured fields
 *
 * TODO: integrate with Datadog / OpenTelemetry
 * TODO: send logs to centralized logging service
 */
export function logError(message: string, error?: unknown, meta?: LogMeta): void {
  const errFields = error !== undefined ? serialiseError(error) : {};
  emit({
    level:     'ERROR',
    message,
    timestamp: new Date().toISOString(),
    service:   SERVICE,
    ...meta,
    ...errFields,
  });
}
