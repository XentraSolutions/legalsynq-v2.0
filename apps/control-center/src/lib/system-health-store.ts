import { promises as fs } from 'fs';
import path from 'path';
import crypto from 'crypto';
import mysql, { type Pool, type PoolOptions, type RowDataPacket } from 'mysql2/promise';

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

const TABLE_NAME = 'system_health_services';

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

function legacyFilePath(): string {
  const override = process.env.SYSTEM_HEALTH_SERVICES_FILE;
  if (override && override.trim()) return override;
  return path.join(process.cwd(), 'data', 'system-health-services.json');
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

async function seedFromLegacyFile(): Promise<Omit<ServiceDef, 'id'>[] | null> {
  try {
    const buf = await fs.readFile(legacyFilePath(), 'utf8');
    const parsed = JSON.parse(buf);
    if (!Array.isArray(parsed)) return null;
    const out: Omit<ServiceDef, 'id'>[] = [];
    for (const item of parsed) {
      if (isServiceInputShape(item)) {
        out.push({
          name:     (item as { name: string }).name,
          url:      (item as { url: string }).url,
          category: (item as { category: ServiceCategory }).category,
        });
      }
    }
    return out.length > 0 ? out : null;
  } catch (err) {
    if ((err as NodeJS.ErrnoException).code === 'ENOENT') return null;
    console.warn('[system-health-store] Failed to read legacy seed file', err);
    return null;
  }
}

/**
 * Parse a .NET-style connection string like:
 *   "server=host;port=3306;database=foo;user=admin;password=secret"
 * into a mysql2 PoolOptions object.
 */
function parseDotNetConnString(raw: string): PoolOptions | null {
  const opts: Record<string, string> = {};
  for (const part of raw.split(';')) {
    const eq = part.indexOf('=');
    if (eq <= 0) continue;
    const k = part.slice(0, eq).trim().toLowerCase();
    const v = part.slice(eq + 1).trim();
    if (k && v) opts[k] = v;
  }
  const host = opts.server ?? opts.host;
  if (!host) return null;
  return {
    host,
    port:     opts.port ? Number(opts.port) : 3306,
    user:     opts.user ?? opts.uid ?? opts.username,
    password: opts.password ?? opts.pwd,
    database: opts.database ?? opts.db,
  };
}

function resolvePoolOptions(): PoolOptions | null {
  const url = process.env.SYSTEM_HEALTH_DB_URL;
  if (url && url.trim()) {
    return { uri: url.trim() };
  }

  const host = process.env.SYSTEM_HEALTH_DB_HOST;
  if (host && host.trim()) {
    return {
      host:     host.trim(),
      port:     process.env.SYSTEM_HEALTH_DB_PORT ? Number(process.env.SYSTEM_HEALTH_DB_PORT) : 3306,
      user:     process.env.SYSTEM_HEALTH_DB_USER,
      password: process.env.SYSTEM_HEALTH_DB_PASSWORD,
      database: process.env.SYSTEM_HEALTH_DB_NAME,
    };
  }

  // Fall back to the platform identity DB (shared across replicas).
  const identity = process.env.ConnectionStrings__IdentityDb;
  if (identity && identity.trim()) {
    return parseDotNetConnString(identity);
  }

  return null;
}

let poolPromise: Promise<Pool> | null = null;
let initPromise: Promise<void> | null = null;

async function getPool(): Promise<Pool> {
  if (!poolPromise) {
    const opts = resolvePoolOptions();
    if (!opts) {
      throw new Error(
        '[system-health-store] No database configuration found. Set SYSTEM_HEALTH_DB_URL, ' +
        'SYSTEM_HEALTH_DB_HOST, or ConnectionStrings__IdentityDb.',
      );
    }
    poolPromise = Promise.resolve(mysql.createPool({
      ...opts,
      connectionLimit: 4,
      waitForConnections: true,
    }));
  }
  return poolPromise;
}

const SEED_LOCK_NAME = 'legalsynq_system_health_services_seed';

async function ensureSchemaAndSeed(): Promise<void> {
  if (!initPromise) {
    initPromise = (async () => {
      const pool = await getPool();
      await pool.query(`
        CREATE TABLE IF NOT EXISTS ${TABLE_NAME} (
          id          VARCHAR(64)  NOT NULL PRIMARY KEY,
          name        VARCHAR(255) NOT NULL,
          url         TEXT         NOT NULL,
          category    VARCHAR(32)  NOT NULL,
          position    INT          NOT NULL DEFAULT 0,
          created_at  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
          updated_at  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
          INDEX ix_${TABLE_NAME}_position (position)
        )
      `);

      // Cheap fast-path: if anything already exists, no seeding needed.
      const [precheck] = await pool.query<RowDataPacket[]>(
        `SELECT 1 AS x FROM ${TABLE_NAME} LIMIT 1`,
      );
      if (precheck.length > 0) return;

      // Serialize seeding across replicas using a MySQL named lock so two
      // instances cannot both observe an empty table and both insert seeds.
      const conn = await pool.getConnection();
      try {
        const [lockRows] = await conn.query<RowDataPacket[]>(
          `SELECT GET_LOCK(?, 10) AS got`,
          [SEED_LOCK_NAME],
        );
        const got = Number((lockRows[0] as { got: number | string | null }).got);
        if (got !== 1) {
          // Another replica is seeding right now; assume it will succeed and
          // fall through. A subsequent listServices() call will see the rows.
          return;
        }
        try {
          const [rows] = await conn.query<RowDataPacket[]>(
            `SELECT COUNT(*) AS n FROM ${TABLE_NAME}`,
          );
          const count = Number((rows[0] as { n: number | string }).n) || 0;
          if (count > 0) return;

          const seed =
            (await seedFromLegacyFile()) ??
            seedFromEnv() ??
            DEFAULT_SERVICES;

          if (seed.length === 0) return;

          const values = seed.map((s, i) => [
            crypto.randomUUID(),
            s.name.trim(),
            s.url.trim(),
            s.category,
            i,
          ]);
          await conn.query(
            `INSERT INTO ${TABLE_NAME} (id, name, url, category, position) VALUES ?`,
            [values],
          );
        } finally {
          await conn.query(`SELECT RELEASE_LOCK(?)`, [SEED_LOCK_NAME]);
        }
      } finally {
        conn.release();
      }
    })().catch(err => {
      // Reset so a future call can retry.
      initPromise = null;
      throw err;
    });
  }
  return initPromise;
}

async function nextPosition(): Promise<number> {
  const pool = await getPool();
  const [rows] = await pool.query<RowDataPacket[]>(
    `SELECT COALESCE(MAX(position), -1) + 1 AS next FROM ${TABLE_NAME}`,
  );
  return Number((rows[0] as { next: number | string }).next) || 0;
}

export async function listServices(): Promise<ServiceDef[]> {
  await ensureSchemaAndSeed();
  const pool = await getPool();
  const [rows] = await pool.query<RowDataPacket[]>(
    `SELECT id, name, url, category FROM ${TABLE_NAME} ORDER BY position ASC, created_at ASC`,
  );
  return rows.map(r => ({
    id:       String(r.id),
    name:     String(r.name),
    url:      String(r.url),
    category: r.category as ServiceCategory,
  }));
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

export async function addService(
  input: ServiceInput,
): Promise<{ ok: true; service: ServiceDef } | { ok: false; error: string }> {
  const err = validateInput(input);
  if (err) return { ok: false, error: err };

  await ensureSchemaAndSeed();
  const pool = await getPool();
  const svc: ServiceDef = {
    id:       crypto.randomUUID(),
    name:     input.name.trim(),
    url:      input.url.trim(),
    category: input.category,
  };
  const position = await nextPosition();
  await pool.query(
    `INSERT INTO ${TABLE_NAME} (id, name, url, category, position) VALUES (?, ?, ?, ?, ?)`,
    [svc.id, svc.name, svc.url, svc.category, position],
  );
  return { ok: true, service: svc };
}

export async function updateService(
  id: string,
  input: ServiceInput,
): Promise<{ ok: true; service: ServiceDef } | { ok: false; error: string; status: number }> {
  const err = validateInput(input);
  if (err) return { ok: false, error: err, status: 400 };

  await ensureSchemaAndSeed();
  const pool = await getPool();
  const updated: ServiceDef = {
    id,
    name:     input.name.trim(),
    url:      input.url.trim(),
    category: input.category,
  };
  const [result] = await pool.query(
    `UPDATE ${TABLE_NAME} SET name = ?, url = ?, category = ? WHERE id = ?`,
    [updated.name, updated.url, updated.category, id],
  );
  const affected = (result as { affectedRows?: number }).affectedRows ?? 0;
  if (affected === 0) return { ok: false, error: 'Service not found', status: 404 };
  return { ok: true, service: updated };
}

export async function removeService(
  id: string,
): Promise<{ ok: true } | { ok: false; error: string; status: number }> {
  await ensureSchemaAndSeed();
  const pool = await getPool();
  const [result] = await pool.query(
    `DELETE FROM ${TABLE_NAME} WHERE id = ?`,
    [id],
  );
  const affected = (result as { affectedRows?: number }).affectedRows ?? 0;
  if (affected === 0) return { ok: false, error: 'Service not found', status: 404 };
  return { ok: true };
}
