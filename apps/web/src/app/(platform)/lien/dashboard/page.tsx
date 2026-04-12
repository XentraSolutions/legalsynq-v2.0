import { requireOrg } from '@/lib/auth-guards';
import Link from 'next/link';
import { KpiCard } from '@/components/lien/kpi-card';
import { StatusBadge, PriorityBadge } from '@/components/lien/status-badge';
import { MOCK_RECENT_ACTIVITY, MOCK_DASHBOARD_TASKS, formatCurrency, timeAgo } from '@/lib/lien-mock-data';

export default async function LienDashboardPage() {
  await requireOrg();
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Dashboard</h1>
          <p className="text-sm text-gray-500 mt-0.5">SynqLien operational overview</p>
        </div>
        <div className="flex items-center gap-2">
          <Link href="/lien/cases" className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors">
            <i className="ri-add-line text-base" />
            New Case
          </Link>
        </div>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <KpiCard title="Total Liens" value={2214} change="+12 this month" changeType="up" icon="ri-stack-line" iconColor="text-indigo-600" href="/lien/liens" />
        <KpiCard title="Active Cases" value={472} change="+8 this month" changeType="up" icon="ri-folder-open-line" iconColor="text-blue-600" href="/lien/cases" />
        <KpiCard title="Pending Tasks" value={23} change="5 overdue" changeType="down" icon="ri-task-line" iconColor="text-amber-600" href="/lien/servicing" />
        <KpiCard title="Monthly Volume" value={formatCurrency(1250000)} change="+15% vs last month" changeType="up" icon="ri-money-dollar-circle-line" iconColor="text-emerald-600" />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <StatCard
          title="Total Liens"
          total={2214}
          segments={[
            { label: 'Close', value: 4, color: '#a78bfa' },
            { label: 'Open', value: 2210, color: '#4f46e5' },
          ]}
          href="/lien/liens"
        />
        <StatCard
          title="Total Cases"
          total={472}
          segments={[
            { label: 'Case Settled', value: 1, color: '#ec4899' },
            { label: 'Demand Sent', value: 1, color: '#6366f1' },
            { label: 'Pre-demand', value: 470, color: '#f472b6' },
          ]}
          href="/lien/cases"
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
        <div className="lg:col-span-2 bg-white border border-gray-200 rounded-xl">
          <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
            <h2 className="text-sm font-semibold text-gray-800">Task Queue</h2>
            <Link href="/lien/servicing" className="text-xs text-primary font-medium hover:underline">View All</Link>
          </div>
          <div className="divide-y divide-gray-100">
            {MOCK_DASHBOARD_TASKS.map((task) => (
              <div key={task.id} className="px-5 py-3 flex items-center justify-between hover:bg-gray-50 transition-colors">
                <div className="min-w-0 flex-1">
                  <p className="text-sm text-gray-700 font-medium truncate">{task.title}</p>
                  <p className="text-xs text-gray-400 mt-0.5">
                    {task.caseRef} &middot; Due {task.dueDate} &middot; {task.assignedTo}
                  </p>
                </div>
                <div className="flex items-center gap-2 ml-4">
                  <PriorityBadge priority={task.priority} />
                  <StatusBadge status={task.status} />
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className="bg-white border border-gray-200 rounded-xl">
          <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
            <h2 className="text-sm font-semibold text-gray-800">Recent Activity</h2>
          </div>
          <div className="divide-y divide-gray-100">
            {MOCK_RECENT_ACTIVITY.slice(0, 5).map((activity) => (
              <div key={activity.id} className="px-5 py-3 flex gap-3">
                <div className={`w-8 h-8 rounded-lg bg-gray-50 flex items-center justify-center shrink-0 ${activity.color}`}>
                  <i className={`${activity.icon} text-base`} />
                </div>
                <div className="min-w-0">
                  <p className="text-xs text-gray-700">{activity.description}</p>
                  <p className="text-xs text-gray-400 mt-0.5">{activity.actor} &middot; {timeAgo(activity.timestamp)}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>

      <div className="bg-white border border-gray-200 rounded-xl p-5">
        <h2 className="text-sm font-semibold text-gray-800 mb-4">Quick Actions</h2>
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
          {[
            { href: '/lien/cases', icon: 'ri-folder-add-line', label: 'New Case', color: 'text-blue-600' },
            { href: '/lien/liens', icon: 'ri-stack-line', label: 'New Lien', color: 'text-indigo-600' },
            { href: '/lien/bill-of-sales', icon: 'ri-receipt-line', label: 'Bill of Sale', color: 'text-green-600' },
            { href: '/lien/batch-entry', icon: 'ri-upload-2-line', label: 'Batch Import', color: 'text-purple-600' },
            { href: '/lien/document-handling', icon: 'ri-file-copy-2-line', label: 'Documents', color: 'text-amber-600' },
            { href: '/lien/contacts', icon: 'ri-contacts-book-line', label: 'Contacts', color: 'text-teal-600' },
          ].map((action) => (
            <Link key={action.href} href={action.href} className="flex flex-col items-center gap-2 p-4 rounded-lg border border-gray-100 hover:border-gray-200 hover:bg-gray-50 transition-colors">
              <div className={`w-10 h-10 rounded-lg bg-gray-50 flex items-center justify-center ${action.color}`}>
                <i className={`${action.icon} text-xl`} />
              </div>
              <span className="text-xs font-medium text-gray-600">{action.label}</span>
            </Link>
          ))}
        </div>
      </div>
    </div>
  );
}

interface Segment { label: string; value: number; color: string; }

function StatCard({ title, total, segments, href }: { title: string; total: number; segments: Segment[]; href: string }) {
  const grandTotal = segments.reduce((s, seg) => s + seg.value, 0);
  const dominant = segments.reduce((a, b) => a.value > b.value ? a : b);
  const pct = grandTotal > 0 ? ((dominant.value / grandTotal) * 100).toFixed(1) : '0';

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-5 flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold text-gray-800">{title}</h2>
        <Link href={href} className="flex items-center gap-1.5 text-xs text-gray-500 hover:text-gray-700 border border-gray-200 rounded-lg px-3 py-1.5 hover:bg-gray-50 transition-colors">
          <i className="ri-file-list-line text-sm leading-none" />
          View Details
        </Link>
      </div>
      <div className="flex items-center gap-6">
        <div className="flex flex-col gap-3 flex-1 min-w-0">
          <p className="text-[32px] font-bold text-gray-900 leading-none">{total.toLocaleString()}</p>
          <ul className="space-y-1.5">
            {segments.map((seg, i) => (
              <li key={i} className="flex items-center justify-between gap-4 text-xs text-gray-600">
                <span className="flex items-center gap-1.5">
                  <span className="w-2 h-2 rounded-full shrink-0" style={{ backgroundColor: seg.color }} />
                  {seg.label || <span className="text-gray-400 italic">Other</span>}
                </span>
                <span className="font-medium text-gray-700 tabular-nums">{seg.value.toLocaleString()}</span>
              </li>
            ))}
          </ul>
        </div>
        <div className="shrink-0">
          <DonutChart segments={segments} pctLabel={`${pct}%`} />
        </div>
      </div>
    </div>
  );
}

function DonutChart({ segments, pctLabel }: { segments: Segment[]; pctLabel: string }) {
  const SIZE = 120; const CX = SIZE / 2; const CY = SIZE / 2; const R = 44; const SW = 18;
  const CIRC = 2 * Math.PI * R;
  const total = segments.reduce((s, seg) => s + seg.value, 0);
  const arcs: { offset: number; dash: string; color: string }[] = [];
  let cumulative = 0;
  for (const seg of segments) {
    const fraction = total > 0 ? seg.value / total : 0;
    const arcLen = fraction * CIRC;
    arcs.push({ color: seg.color, dash: `${arcLen} ${CIRC - arcLen}`, offset: CIRC / 4 - cumulative });
    cumulative += arcLen;
  }
  return (
    <svg width={SIZE} height={SIZE} viewBox={`0 0 ${SIZE} ${SIZE}`}>
      <circle cx={CX} cy={CY} r={R} fill="none" stroke="#f3f4f6" strokeWidth={SW} />
      {arcs.map((arc, i) => (
        <circle key={i} cx={CX} cy={CY} r={R} fill="none" stroke={arc.color} strokeWidth={SW} strokeDasharray={arc.dash} strokeDashoffset={arc.offset} strokeLinecap="butt" />
      ))}
      <text x={CX} y={CY + 4} textAnchor="middle" fontSize="12" fontWeight="600" fill="#374151">{pctLabel}</text>
    </svg>
  );
}
