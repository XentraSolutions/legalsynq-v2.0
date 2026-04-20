/**
 * GET /api/monitoring/uptime
 *
 * Public-safe BFF route that returns sanitized uptime history data
 * for availability bar rendering on the public /status page.
 *
 * Behavior by MONITORING_SOURCE:
 *   local   — returns { components: [] } (no aggregation engine)
 *   service — fetches rollups from Monitoring Service (to get entityIds),
 *             then fetches per-entity history in parallel, strips all
 *             internal IDs from the response.
 *
 * What is exposed publicly:
 *   - component name (safe display name)
 *   - uptimePercent (rounded to 2dp)
 *   - hourly bucket dominant status (Healthy | Degraded | Down | Unknown)
 *   - bucket uptimePercent
 *   - insufficientData flag
 *
 * What is NOT exposed:
 *   - entityId (internal UUID — stripped at this layer)
 *   - raw check counts
 *   - latency aggregates (internal metric)
 *   - backend URLs
 *   - service tokens
 */

import { NextResponse } from 'next/server';

export const dynamic = 'force-dynamic';

const MONITORING_SOURCE = process.env.MONITORING_SOURCE ?? 'local';
const GATEWAY_URL       = process.env.GATEWAY_URL ?? 'http://localhost:5010';

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

interface HistoryBucket {
  bucketStartUtc:  string;
  uptimePercent:   number;
  dominantStatus:  string;
  insufficientData: boolean;
}

interface HistoryResponse {
  buckets: HistoryBucket[];
}

// ── Public-safe output shapes (exposed to browser) ───────────────────────────

export interface PublicUptimeBucket {
  bucketStartUtc:  string;
  dominantStatus:  'Healthy' | 'Degraded' | 'Down' | 'Unknown';
  uptimePercent:   number;
  insufficientData: boolean;
}

export interface PublicUptimeComponent {
  name:            string;
  uptimePercent:   number | null;
  buckets:         PublicUptimeBucket[];
}

export interface PublicUptimeResponse {
  window:          string;
  components:      PublicUptimeComponent[];
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function sanitizeStatus(raw: string): 'Healthy' | 'Degraded' | 'Down' | 'Unknown' {
  if (raw === 'Healthy' || raw === 'Degraded' || raw === 'Down') return raw;
  return 'Unknown';
}

async function fetchRollups(): Promise<RollupsComponent[]> {
  const url = `${GATEWAY_URL}/monitoring/monitoring/uptime/rollups?window=24h`;
  const res = await fetch(url, { cache: 'no-store', headers: { Accept: 'application/json' } });
  if (!res.ok) throw new Error(`Rollups returned HTTP ${res.status}`);
  const data: RollupsResponse = await res.json();
  return data.components ?? [];
}

async function fetchHistory(entityId: string): Promise<HistoryBucket[]> {
  const url = `${GATEWAY_URL}/monitoring/monitoring/uptime/history?entityId=${entityId}&window=24h`;
  const res = await fetch(url, { cache: 'no-store', headers: { Accept: 'application/json' } });
  if (!res.ok) return [];
  const data: HistoryResponse = await res.json();
  return data.buckets ?? [];
}

// ── Handler ───────────────────────────────────────────────────────────────────

export async function GET(): Promise<NextResponse> {
  if (MONITORING_SOURCE !== 'service') {
    const empty: PublicUptimeResponse = { window: '24h', components: [] };
    return NextResponse.json(empty, {
      headers: { 'Cache-Control': 'no-store, no-cache, must-revalidate' },
    });
  }

  try {
    const rollupComponents = await fetchRollups();

    const historyResults = await Promise.allSettled(
      rollupComponents.map(c => fetchHistory(c.entityId)),
    );

    const components: PublicUptimeComponent[] = rollupComponents.map((c, i) => {
      const histResult = historyResults[i];
      const rawBuckets = histResult.status === 'fulfilled' ? histResult.value : [];

      const buckets: PublicUptimeBucket[] = rawBuckets.map(b => ({
        bucketStartUtc:  b.bucketStartUtc,
        dominantStatus:  sanitizeStatus(b.dominantStatus),
        uptimePercent:   Math.round(b.uptimePercent * 100) / 100,
        insufficientData: b.insufficientData,
      }));

      return {
        name:          c.entityName,
        uptimePercent: c.insufficientData ? null : Math.round(c.uptimePercent * 100) / 100,
        buckets,
      };
    });

    const response: PublicUptimeResponse = { window: '24h', components };
    return NextResponse.json(response, {
      headers: { 'Cache-Control': 'no-store, no-cache, must-revalidate' },
    });
  } catch {
    const empty: PublicUptimeResponse = { window: '24h', components: [] };
    return NextResponse.json(empty, {
      headers: { 'Cache-Control': 'no-store, no-cache, must-revalidate' },
    });
  }
}
