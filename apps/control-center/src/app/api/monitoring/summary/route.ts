import { NextResponse } from 'next/server';

const SERVICES: ServiceDef[] = [
  { name: 'Gateway',       url: 'http://127.0.0.1:5010/health', category: 'infrastructure' },
  { name: 'Identity',      url: 'http://127.0.0.1:5001/health', category: 'infrastructure' },
  { name: 'Notifications', url: 'http://127.0.0.1:5008/health', category: 'infrastructure' },
  { name: 'Audit',         url: 'http://127.0.0.1:5007/health', category: 'infrastructure' },
  { name: 'SynqFund',      url: 'http://127.0.0.1:5002/health', category: 'product' },
  { name: 'CareConnect',   url: 'http://127.0.0.1:5003/health', category: 'product' },
];

interface ServiceDef {
  name:     string;
  url:      string;
  category: 'infrastructure' | 'product';
}

type Status = 'Healthy' | 'Degraded' | 'Down';

interface ProbeResult {
  name:             string;
  status:           Status;
  latencyMs?:       number;
  lastCheckedAtUtc: string;
  category:         string;
  detail?:          string;
}

async function probeService(svc: ServiceDef): Promise<ProbeResult> {
  const start = Date.now();
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 4000);

  try {
    const res = await fetch(svc.url, {
      signal: controller.signal,
      cache:  'no-store',
    });
    const latencyMs = Date.now() - start;

    let detail: string | undefined;
    try {
      const body = await res.json();
      detail = body.status ?? body.service ?? undefined;
    } catch {}

    const status: Status = !res.ok
      ? 'Degraded'
      : latencyMs > 2000
        ? 'Degraded'
        : 'Healthy';

    return {
      name:             svc.name,
      status,
      latencyMs,
      lastCheckedAtUtc: new Date().toISOString(),
      category:         svc.category,
      detail,
    };
  } catch {
    return {
      name:             svc.name,
      status:           'Down',
      latencyMs:        Date.now() - start,
      lastCheckedAtUtc: new Date().toISOString(),
      category:         svc.category,
      detail:           'Unreachable',
    };
  } finally {
    clearTimeout(timer);
  }
}

export async function GET() {
  const results = await Promise.all(SERVICES.map(probeService));

  const downCount     = results.filter(r => r.status === 'Down').length;
  const degradedCount = results.filter(r => r.status === 'Degraded').length;

  const overallStatus: Status =
    downCount > 0     ? 'Down' :
    degradedCount > 0 ? 'Degraded' :
                        'Healthy';

  const alerts = results
    .filter(r => r.status !== 'Healthy')
    .map(r => ({
      id:           `alert-${r.name.toLowerCase()}`,
      message:      `${r.name} is ${r.status.toLowerCase()}${r.detail ? `: ${r.detail}` : ''}`,
      severity:     r.status === 'Down' ? 'Critical' as const : 'Warning' as const,
      createdAtUtc: r.lastCheckedAtUtc,
    }));

  return NextResponse.json({
    system: {
      status:           overallStatus,
      lastCheckedAtUtc: new Date().toISOString(),
    },
    integrations: results.map(r => ({
      name:             r.name,
      status:           r.status,
      latencyMs:        r.latencyMs,
      lastCheckedAtUtc: r.lastCheckedAtUtc,
      category:         r.category,
    })),
    alerts,
  }, {
    headers: { 'Cache-Control': 'no-store, no-cache, must-revalidate' },
  });
}
