import { promises as fs } from 'fs';
import path from 'path';
import crypto from 'crypto';
import type { AuditIngestPayload } from '@/types/control-center';
import { controlCenterServerApi } from './control-center-api';

/**
 * Durable retry queue ("outbox") for canonical audit emissions.
 *
 * The Control Center mirrors monitoring-config changes to the Platform
 * Audit Event Service via /audit-service/audit/ingest. That call is
 * fire-and-observe so it never blocks user-facing config changes — but
 * a transient outage of the audit service used to mean those events
 * were logged to the console and dropped, leaving a permanent gap in
 * the central Audit Logs page.
 *
 * This module persists failed emissions to a small JSON file and a
 * background timer keeps retrying with exponential backoff. Because
 * each entry already carries a server-generated idempotencyKey and
 * occurredAtUtc, a successful retry — whether seconds or hours later —
 * shows up in the central Audit Logs with the original timestamp and
 * key, and re-delivery is safely deduplicated by the audit service.
 *
 * Entries that exhaust the retry budget are kept in the file and marked
 * as persistent failures so the operator UI can surface a banner. They
 * are never silently swallowed.
 */

const MAX_ENTRIES = 500;
const MAX_ATTEMPTS = 8;
const RETRY_INTERVAL_MS = 30_000;

// Exponential backoff with a 24h ceiling: 30s, 1m, 2m, 4m, 8m, 16m, 32m, 24h.
function backoffMs(attempts: number): number {
  const base = 30_000 * Math.pow(2, Math.max(0, attempts - 1));
  return Math.min(base, 24 * 60 * 60 * 1000);
}

export interface OutboxEntry {
  id:             string;
  payload:        AuditIngestPayload;
  enqueuedAt:     string;
  attempts:       number;
  lastAttemptAt:  string | null;
  nextAttemptAt:  string;
  lastError:      string | null;
  persistentFailure: boolean;
}

export interface OutboxStatus {
  pending:           number;
  persistentFailures: number;
  oldestEnqueuedAt:  string | null;
  lastError:         string | null;
}

function outboxFilePath(): string {
  const override = process.env.SYSTEM_HEALTH_AUDIT_OUTBOX_FILE;
  if (override && override.trim()) return override;
  return path.join(process.cwd(), 'data', 'system-health-audit-outbox.json');
}

let writeChain: Promise<unknown> = Promise.resolve();

async function readAll(): Promise<OutboxEntry[]> {
  try {
    const buf = await fs.readFile(outboxFilePath(), 'utf8');
    const parsed = JSON.parse(buf);
    if (!Array.isArray(parsed)) return [];
    const out: OutboxEntry[] = [];
    for (const item of parsed) {
      if (!item || typeof item !== 'object') continue;
      const o = item as Record<string, unknown>;
      if (typeof o.id !== 'string') continue;
      if (!o.payload || typeof o.payload !== 'object') continue;
      out.push({
        id:                o.id,
        payload:           o.payload as AuditIngestPayload,
        enqueuedAt:        typeof o.enqueuedAt === 'string' ? o.enqueuedAt : new Date().toISOString(),
        attempts:          typeof o.attempts === 'number' ? o.attempts : 0,
        lastAttemptAt:     typeof o.lastAttemptAt === 'string' ? o.lastAttemptAt : null,
        nextAttemptAt:     typeof o.nextAttemptAt === 'string' ? o.nextAttemptAt : new Date().toISOString(),
        lastError:         typeof o.lastError === 'string' ? o.lastError : null,
        persistentFailure: Boolean(o.persistentFailure),
      });
    }
    return out;
  } catch (err) {
    if ((err as NodeJS.ErrnoException).code === 'ENOENT') return [];
    console.warn('[system-health-audit-outbox] Failed to read outbox file', err);
    return [];
  }
}

async function writeAll(entries: OutboxEntry[]): Promise<void> {
  const file = outboxFilePath();
  await fs.mkdir(path.dirname(file), { recursive: true });
  const tmp = `${file}.tmp`;
  const trimmed = entries.length > MAX_ENTRIES
    ? entries.slice(entries.length - MAX_ENTRIES)
    : entries;
  await fs.writeFile(tmp, JSON.stringify(trimmed, null, 2), 'utf8');
  await fs.rename(tmp, file);
}

function chain<T>(fn: () => Promise<T>): Promise<T> {
  const next = writeChain.then(fn, fn);
  writeChain = next.catch(() => undefined);
  return next;
}

export async function enqueueFailedEmission(
  payload: AuditIngestPayload,
  initialError: unknown,
): Promise<void> {
  const now = new Date().toISOString();
  const entry: OutboxEntry = {
    id:                crypto.randomUUID(),
    payload,
    enqueuedAt:        now,
    attempts:          1,
    lastAttemptAt:     now,
    nextAttemptAt:     new Date(Date.now() + backoffMs(1)).toISOString(),
    lastError:         describeError(initialError),
    persistentFailure: false,
  };
  await chain(async () => {
    const all = await readAll();
    all.push(entry);
    await writeAll(all);
  });
  ensureWorkerStarted();
}

function describeError(err: unknown): string {
  if (err instanceof Error) return err.message;
  if (typeof err === 'string') return err;
  try {
    return JSON.stringify(err);
  } catch {
    return String(err);
  }
}

export async function getOutboxStatus(): Promise<OutboxStatus> {
  const all = await readAll();
  if (all.length === 0) {
    return { pending: 0, persistentFailures: 0, oldestEnqueuedAt: null, lastError: null };
  }
  const persistentFailures = all.filter(e => e.persistentFailure).length;
  const sorted = all.slice().sort((a, b) => a.enqueuedAt.localeCompare(b.enqueuedAt));
  const lastErrEntry = all
    .slice()
    .filter(e => e.lastError)
    .sort((a, b) => (b.lastAttemptAt ?? '').localeCompare(a.lastAttemptAt ?? ''))[0];
  return {
    pending:            all.length,
    persistentFailures,
    oldestEnqueuedAt:   sorted[0]?.enqueuedAt ?? null,
    lastError:          lastErrEntry?.lastError ?? null,
  };
}

export async function processOutboxOnce(): Promise<{ delivered: number; failed: number }> {
  const all = await chain(readAll);
  const now = Date.now();
  const due = all.filter(e => !e.persistentFailure && Date.parse(e.nextAttemptAt) <= now);

  let delivered = 0;
  let failed = 0;
  const updates = new Map<string, OutboxEntry | null>();

  for (const entry of due) {
    try {
      await controlCenterServerApi.auditIngest.emit(entry.payload);
      updates.set(entry.id, null); // remove on success
      delivered += 1;
    } catch (err) {
      failed += 1;
      const attempts = entry.attempts + 1;
      const nowIso = new Date().toISOString();
      const persistent = attempts >= MAX_ATTEMPTS;
      if (persistent) {
        console.warn(
          '[system-health-audit-outbox] Canonical audit event reached max retry attempts; ' +
          'leaving in outbox for operator visibility',
          {
            id:        entry.id,
            attempts,
            eventType: entry.payload.eventType,
            action:    entry.payload.action,
            entityId:  entry.payload.entity?.id,
            err:       describeError(err),
          },
        );
      }
      updates.set(entry.id, {
        ...entry,
        attempts,
        lastAttemptAt:     nowIso,
        nextAttemptAt:     new Date(Date.now() + backoffMs(attempts)).toISOString(),
        lastError:         describeError(err),
        persistentFailure: persistent,
      });
    }
  }

  if (updates.size > 0) {
    await chain(async () => {
      const fresh = await readAll();
      const merged: OutboxEntry[] = [];
      for (const e of fresh) {
        if (!updates.has(e.id)) {
          merged.push(e);
          continue;
        }
        const next = updates.get(e.id);
        if (next) merged.push(next);
        // null = drop (delivered)
      }
      await writeAll(merged);
    });
  }

  return { delivered, failed };
}

let workerTimer: NodeJS.Timeout | null = null;

function ensureWorkerStarted(): void {
  if (workerTimer) return;
  // Only run a background loop in long-lived server contexts. In tests or
  // build-time evaluation the timer is harmless because it is unref'd.
  workerTimer = setInterval(() => {
    void processOutboxOnce().catch(err => {
      console.warn('[system-health-audit-outbox] Outbox processing failed', err);
    });
  }, RETRY_INTERVAL_MS);
  if (typeof workerTimer.unref === 'function') workerTimer.unref();
}

/** Kick the worker on module load so a restart resumes pending entries. */
export function resumeOutboxOnStartup(): void {
  ensureWorkerStarted();
  void processOutboxOnce().catch(err => {
    console.warn('[system-health-audit-outbox] Initial outbox drain failed', err);
  });
}

/** Test-only: stop the background timer. */
export function _stopOutboxWorkerForTests(): void {
  if (workerTimer) {
    clearInterval(workerTimer);
    workerTimer = null;
  }
}
