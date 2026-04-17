import { NextResponse } from 'next/server';
import { listServices, type ServiceDef } from '@/lib/system-health-store';

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
      const text = await res.text();
      try {
        const body = JSON.parse(text);
        detail = body.status ?? body.service ?? undefined;
      } catch {
        if (text) detail = text.trim();
      }
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
  const services = await listServices();
  const results = await Promise.all(services.map(probeService));

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
