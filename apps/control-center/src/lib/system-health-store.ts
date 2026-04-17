import { promises as fs } from 'fs';
import path from 'path';
import crypto from 'crypto';

export type ServiceCategory = 'infrastructure' | 'product';

export interface ServiceDef {
  id:       string;
  name:     string;
  url:      string;
  category: ServiceCategory;
}

export interface ServiceInput {
  name:     string;
  url:      string;
  category: ServiceCategory;
}

const DEFAULT_SERVICES: Omit<ServiceDef, 'id'>[] = [
  { name: 'Gateway',          url: 'http://127.0.0.1:5010/health',        category: 'infrastructure' },
  { name: 'Identity',         url: 'http://127.0.0.1:5001/health',        category: 'infrastructure' },
  { name: 'Documents',        url: 'http://127.0.0.1:5006/health',        category: 'infrastructure' },
  { name: 'Notifications',    url: 'http://127.0.0.1:5008/health',        category: 'infrastructure' },
  { name: 'Audit',            url: 'http://127.0.0.1:5007/health',        category: 'infrastructure' },
  { name: 'Reports',          url: 'http://127.0.0.1:5029/api/v1/health', category: 'infrastructure' },
  { name: 'Workflow',         url: 'http://127.0.0.1:5012/health',        category: 'infrastructure' },
  { name: 'Synq Fund',        url: 'http://127.0.0.1:5002/health',        category: 'product' },
  { name: 'Synq CareConnect', url: 'http://127.0.0.1:5003/health',        category: 'product' },
  { name: 'Synq Liens',       url: 'http://127.0.0.1:5009/health',        category: 'product' },
];

function storeFilePath(): string {
  const override = process.env.SYSTEM_HEALTH_SERVICES_FILE;
  if (override && override.trim()) return override;
  return path.join(process.cwd(), 'data', 'system-health-services.json');
}

function isCategory(v: unknown): v is ServiceCategory {
  return v === 'infrastructure' || v === 'product';
}

function isServiceInputShape(v: unknown): v is { name: string; url: string; category: ServiceCategory } {
  if (!v || typeof v !== 'object') return false;
  const o = v as Record<string, unknown>;
  return typeof o.name === 'string'
      && typeof o.url === 'string'
      && isCategory(o.category);
}

function seedFromEnv(): Omit<ServiceDef, 'id'>[] | null {
  const raw = process.env.SYSTEM_HEALTH_SERVICES;
  if (!raw || !raw.trim()) return null;
  try {
    const parsed = JSON.parse(raw);
    if (!Array.isArray(parsed)) return null;
    const valid: Omit<ServiceDef, 'id'>[] = [];
    for (const item of parsed) {
      if (isServiceInputShape(item)) {
        valid.push({ name: item.name.trim(), url: item.url.trim(), category: item.category });
      }
    }
    return valid.length > 0 ? valid : null;
  } catch {
    return null;
  }
}

function withIds(items: Omit<ServiceDef, 'id'>[]): ServiceDef[] {
  return items.map(s => ({ id: crypto.randomUUID(), ...s }));
}

let writeChain: Promise<unknown> = Promise.resolve();

async function readFromDisk(): Promise<ServiceDef[] | null> {
  try {
    const buf = await fs.readFile(storeFilePath(), 'utf8');
    const parsed = JSON.parse(buf);
    if (!Array.isArray(parsed)) return null;
    const out: ServiceDef[] = [];
    for (const item of parsed) {
      if (isServiceInputShape(item)) {
        const id = (item as Record<string, unknown>).id;
        out.push({
          id:       typeof id === 'string' && id ? id : crypto.randomUUID(),
          name:     (item as { name: string }).name,
          url:      (item as { url: string }).url,
          category: (item as { category: ServiceCategory }).category,
        });
      }
    }
    return out;
  } catch (err) {
    if ((err as NodeJS.ErrnoException).code === 'ENOENT') return null;
    console.warn('[system-health-store] Failed to read store file', err);
    return null;
  }
}

async function writeToDisk(items: ServiceDef[]): Promise<void> {
  const file = storeFilePath();
  await fs.mkdir(path.dirname(file), { recursive: true });
  const tmp = `${file}.tmp`;
  await fs.writeFile(tmp, JSON.stringify(items, null, 2), 'utf8');
  await fs.rename(tmp, file);
}

async function ensureSeeded(): Promise<ServiceDef[]> {
  const existing = await readFromDisk();
  if (existing) return existing;
  const seed = seedFromEnv() ?? DEFAULT_SERVICES;
  const seeded = withIds(seed);
  await writeToDisk(seeded);
  return seeded;
}

export async function listServices(): Promise<ServiceDef[]> {
  return ensureSeeded();
}

function validateInput(input: Partial<ServiceInput>): string | null {
  if (!input.name || typeof input.name !== 'string' || !input.name.trim()) return 'name is required';
  if (!input.url  || typeof input.url  !== 'string' || !input.url.trim())  return 'url is required';
  try {
    new URL(input.url);
  } catch {
    return 'url must be a valid absolute URL';
  }
  if (!isCategory(input.category)) return 'category must be "infrastructure" or "product"';
  return null;
}

export async function addService(input: ServiceInput): Promise<{ ok: true; service: ServiceDef } | { ok: false; error: string }> {
  const err = validateInput(input);
  if (err) return { ok: false, error: err };
  const result = await (writeChain = writeChain.then(async () => {
    const items = await ensureSeeded();
    const svc: ServiceDef = {
      id:       crypto.randomUUID(),
      name:     input.name.trim(),
      url:      input.url.trim(),
      category: input.category,
    };
    items.push(svc);
    await writeToDisk(items);
    return svc;
  }));
  return { ok: true, service: result as ServiceDef };
}

export async function updateService(id: string, input: ServiceInput): Promise<{ ok: true; service: ServiceDef } | { ok: false; error: string; status: number }> {
  const err = validateInput(input);
  if (err) return { ok: false, error: err, status: 400 };
  let notFound = false;
  const result = await (writeChain = writeChain.then(async () => {
    const items = await ensureSeeded();
    const idx = items.findIndex(s => s.id === id);
    if (idx === -1) { notFound = true; return null; }
    const updated: ServiceDef = {
      id,
      name:     input.name.trim(),
      url:      input.url.trim(),
      category: input.category,
    };
    items[idx] = updated;
    await writeToDisk(items);
    return updated;
  }));
  if (notFound) return { ok: false, error: 'Service not found', status: 404 };
  return { ok: true, service: result as ServiceDef };
}

export async function removeService(id: string): Promise<{ ok: true } | { ok: false; error: string; status: number }> {
  let notFound = false;
  await (writeChain = writeChain.then(async () => {
    const items = await ensureSeeded();
    const idx = items.findIndex(s => s.id === id);
    if (idx === -1) { notFound = true; return; }
    items.splice(idx, 1);
    await writeToDisk(items);
  }));
  if (notFound) return { ok: false, error: 'Service not found', status: 404 };
  return { ok: true };
}
