/**
 * GET /api/monitoring/latency
 *
 * Internal-only BFF route that returns per-component latency history
 * for sparkline rendering on the internal /monitoring page.
 *
 * ── Source modes ──────────────────────────────────────────────────────────────
 *
 *   MONITORING_SOURCE=service → fetches rollups (for entityIds), then fetches
 *     per-entity history in parallel and extracts avgLatencyMs / maxLatencyMs.
 *   MONITORING_SOURCE=local   → returns { components: [] } (no aggregation
 *     engine in local mode; do not fabricate metrics).
 *
 * ── What is exposed ───────────────────────────────────────────────────────────
 *
 *   - component display name (safe; already shown in internal list)
 *   - bucketStartUtc (timestamp of each hourly bucket)
 *   - avgLatencyMs   (primary chart value)
 *   - maxLatencyMs   (secondary reference line)
 *   - insufficientData flag
 *
 * ── What is NOT exposed ───────────────────────────────────────────────────────
 *
 *   - entityId (internal UUID — stripped at this layer)
 *   - raw check counts (upCount, degradedCount, …)
 *   - backend URLs or service tokens
 *
 * ── Security boundary ─────────────────────────────────────────────────────────
 *
 *   This route must NEVER be linked from the public /status page.
 *   It is consumed exclusively by the internal ComponentStatusList on /monitoring.
 *   The Control Center middleware enforces authentication for all /api/* routes
 *   in this application.
 */

import { NextResponse } from 'next/server';

export const dynamic = 'force-dynamic';

const MONITORING_SOURCE = process.env.MONITORING_SOURCE ?? 'local';
const GATEWAY_URL       = process.env.GATEWAY_URL       ?? 'http://localhost:5010';

// ── Internal Monitoring Service shapes ────────────────────────────────────────

interface RollupsComponent {
  entityId:        string;
  entityName:      string;
  uptimePercent:   number;
  insufficientData: boolean;
}

interface RollupsResponse {
  components: RollupsComponent[];
}

interface RawHistoryBucket {
  bucketStartUtc:  string;
  avgLatencyMs:    number | null;
  maxLatencyMs:    number;
  insufficientData: boolean;
}

interface RawHistoryResponse {
  buckets: RawHistoryBucket[];
}

// ── Internal-safe output shapes ───────────────────────────────────────────────

export interface LatencyBucket {
  bucketStartUtc: string;
  avgLatencyMs:   number | null;
  maxLatencyMs:   number | null;
  insufficientData: boolean;
}

export interface LatencyComponent {
  name:    string;
  buckets: LatencyBucket[];
}

export interface InternalLatencyResponse {
  source:     'service' | 'local';
  window:     string;
  components: LatencyComponent[];
}

// ── Helpers ───────────────────────────────────────────────────────────────────

async function fetchRollups(): Promise<RollupsComponent[]> {
  const url = `${GATEWAY_URL}/monitoring/monitoring/uptime/rollups?window=24h`;
  const res = await fetch(url, { cache: 'no-store', headers: { Accept: 'application/json' } });
  if (!res.ok) throw new Error(`Rollups returned HTTP ${res.status}`);
  const data: RollupsResponse = await res.json();
  return data.components ?? [];
}

async function fetchHistory(entityId: string): Promise<RawHistoryBucket[]> {
  const url = `${GATEWAY_URL}/monitoring/monitoring/uptime/history?entityId=${entityId}&window=24h`;
  const res = await fetch(url, { cache: 'no-store', headers: { Accept: 'application/json' } });
  if (!res.ok) return [];
  const data: RawHistoryResponse = await res.json();
  return data.buckets ?? [];
}

const NO_STORE = { 'Cache-Control': 'no-store, no-cache, must-revalidate' };

// ── Handler ───────────────────────────────────────────────────────────────────

export async function GET(): Promise<NextResponse> {
  if (MONITORING_SOURCE !== 'service') {
    const empty: InternalLatencyResponse = { source: 'local', window: '24h', components: [] };
    return NextResponse.json(empty, { headers: NO_STORE });
  }

  try {
    const rollups = await fetchRollups();

    const historyResults = await Promise.allSettled(
      rollups.map(c => fetchHistory(c.entityId)),
    );

    const components: LatencyComponent[] = rollups.map((c, i) => {
      const result = historyResults[i];
      const rawBuckets = result.status === 'fulfilled' ? result.value : [];

      const buckets: LatencyBucket[] = rawBuckets.map(b => ({
        bucketStartUtc:  b.bucketStartUtc,
        avgLatencyMs:    typeof b.avgLatencyMs === 'number' && isFinite(b.avgLatencyMs)
                           ? Math.round(b.avgLatencyMs * 10) / 10
                           : null,
        maxLatencyMs:    typeof b.maxLatencyMs === 'number' && isFinite(b.maxLatencyMs) && b.maxLatencyMs > 0
                           ? b.maxLatencyMs
                           : null,
        insufficientData: b.insufficientData,
      }));

      return { name: c.entityName, buckets };
    });

    const response: InternalLatencyResponse = { source: 'service', window: '24h', components };
    return NextResponse.json(response, { headers: NO_STORE });
  } catch {
    const empty: InternalLatencyResponse = { source: 'service', window: '24h', components: [] };
    return NextResponse.json(empty, { status: 502, headers: NO_STORE });
  }
}
