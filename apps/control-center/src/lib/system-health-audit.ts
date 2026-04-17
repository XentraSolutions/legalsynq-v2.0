import { promises as fs } from 'fs';
import path from 'path';
import crypto from 'crypto';
import type { ServiceDef } from './system-health-store';

export type AuditAction = 'add' | 'update' | 'remove';

export interface AuditActor {
  userId: string;
  email:  string;
}

export interface AuditEntry {
  id:        string;
  action:    AuditAction;
  serviceId: string;
  actor:     AuditActor;
  timestamp: string;
  before:    ServiceDef | null;
  after:     ServiceDef | null;
}

const MAX_ENTRIES = 200;

function auditFilePath(): string {
  const override = process.env.SYSTEM_HEALTH_AUDIT_FILE;
  if (override && override.trim()) return override;
  return path.join(process.cwd(), 'data', 'system-health-services-audit.json');
}

let writeChain: Promise<unknown> = Promise.resolve();

function isAuditAction(v: unknown): v is AuditAction {
  return v === 'add' || v === 'update' || v === 'remove';
}

function isService(v: unknown): v is ServiceDef {
  if (!v || typeof v !== 'object') return false;
  const o = v as Record<string, unknown>;
  return typeof o.id === 'string'
      && typeof o.name === 'string'
      && typeof o.url === 'string'
      && (o.category === 'infrastructure' || o.category === 'product');
}

async function readAll(): Promise<AuditEntry[]> {
  try {
    const buf = await fs.readFile(auditFilePath(), 'utf8');
    const parsed = JSON.parse(buf);
    if (!Array.isArray(parsed)) return [];
    const out: AuditEntry[] = [];
    for (const item of parsed) {
      if (!item || typeof item !== 'object') continue;
      const o = item as Record<string, unknown>;
      const actor = o.actor as Record<string, unknown> | undefined;
      if (typeof o.id !== 'string') continue;
      if (!isAuditAction(o.action)) continue;
      if (typeof o.serviceId !== 'string') continue;
      if (typeof o.timestamp !== 'string') continue;
      if (!actor || typeof actor.userId !== 'string' || typeof actor.email !== 'string') continue;
      out.push({
        id:        o.id,
        action:    o.action,
        serviceId: o.serviceId,
        actor:     { userId: actor.userId, email: actor.email },
        timestamp: o.timestamp,
        before:    isService(o.before) ? o.before : null,
        after:     isService(o.after)  ? o.after  : null,
      });
    }
    return out;
  } catch (err) {
    if ((err as NodeJS.ErrnoException).code === 'ENOENT') return [];
    console.warn('[system-health-audit] Failed to read audit file', err);
    return [];
  }
}

async function writeAll(entries: AuditEntry[]): Promise<void> {
  const file = auditFilePath();
  await fs.mkdir(path.dirname(file), { recursive: true });
  const tmp = `${file}.tmp`;
  await fs.writeFile(tmp, JSON.stringify(entries, null, 2), 'utf8');
  await fs.rename(tmp, file);
}

export async function recordAudit(input: {
  action:    AuditAction;
  serviceId: string;
  actor:     AuditActor;
  before:    ServiceDef | null;
  after:     ServiceDef | null;
}): Promise<AuditEntry> {
  const entry: AuditEntry = {
    id:        crypto.randomUUID(),
    action:    input.action,
    serviceId: input.serviceId,
    actor:     input.actor,
    timestamp: new Date().toISOString(),
    before:    input.before,
    after:     input.after,
  };
  await (writeChain = writeChain.then(async () => {
    const existing = await readAll();
    existing.push(entry);
    const trimmed = existing.length > MAX_ENTRIES
      ? existing.slice(existing.length - MAX_ENTRIES)
      : existing;
    await writeAll(trimmed);
  }));
  return entry;
}

export async function listAudit(limit = 50): Promise<AuditEntry[]> {
  const all = await readAll();
  return all
    .slice()
    .sort((a, b) => b.timestamp.localeCompare(a.timestamp))
    .slice(0, limit);
}
