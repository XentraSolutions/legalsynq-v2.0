import { cookies }              from 'next/headers';
import { requirePlatformAdmin }  from '@/lib/auth-guards';
import { GovernancePanel }       from '@/components/sms-governance/governance-panel';
import type {
  GovernanceSummary,
  RateLimitStatus,
  GeoStatus,
  PaginatedResult,
  GovernancePolicy,
  GovernanceDecision,
} from '@/lib/sms-governance-api';

export const dynamic = 'force-dynamic';

const API_BASE = process.env.CONTROL_CENTER_API_BASE ?? 'http://127.0.0.1:5010';
const NOTIF_BASE = `${API_BASE}/notifications/v1/admin/sms/governance`;

async function fetchJson<T>(url: string, token: string): Promise<T> {
  const res = await fetch(url, {
    headers: { Authorization: `Bearer ${token}` },
    cache: 'no-store',
  });
  if (!res.ok) throw new Error(`${url}: ${res.status}`);
  return res.json();
}

export default async function SmsGovernancePage() {
  await requirePlatformAdmin();

  const cookieStore = await cookies();
  const token = cookieStore.get('platform_session')?.value ?? '';

  const [policies, decisions, summary, rateLimits, geo] = await Promise.allSettled([
    fetchJson<PaginatedResult<GovernancePolicy>>(`${NOTIF_BASE}/policies?pageSize=100`, token),
    fetchJson<PaginatedResult<GovernanceDecision>>(`${NOTIF_BASE}/decisions?pageSize=50`, token),
    fetchJson<GovernanceSummary>(`${NOTIF_BASE}/summary?hours=24`, token),
    fetchJson<RateLimitStatus>(`${NOTIF_BASE}/rate-limits`, token),
    fetchJson<GeoStatus>(`${NOTIF_BASE}/geo`, token),
  ]);

  return (
    <div className="space-y-6 p-6">
      <div>
        <h1 className="text-xl font-semibold text-slate-900">SMS Governance</h1>
        <p className="text-sm text-slate-500 mt-1">
          Enterprise-level governance controls for SMS delivery — quiet hours, geographic
          restrictions, rate limiting, provider governance, and retry controls.
        </p>
      </div>

      <GovernancePanel
        policies={policies.status === 'fulfilled'    ? policies.value    : { total: 0, page: 1, pageSize: 100, items: [] }}
        decisions={decisions.status === 'fulfilled'  ? decisions.value   : { total: 0, page: 1, pageSize: 50,  items: [] }}
        summary={summary.status === 'fulfilled'      ? summary.value     : null}
        rateLimits={rateLimits.status === 'fulfilled' ? rateLimits.value  : null}
        geo={geo.status === 'fulfilled'              ? geo.value         : null}
      />
    </div>
  );
}
