import { requireOrg } from '@/lib/auth-guards';
import Link from 'next/link';

export default async function LienDashboardPage() {
  await requireOrg();
  return (
    <div className="space-y-6">
      {/* 2 × 2 grid of stat cards */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <StatCard
          title="Total Liens"
          total={2214}
          segments={[
            { label: 'Close', value: 4,    color: '#a78bfa' },
            { label: 'Open',  value: 2210, color: '#4f46e5' },
          ]}
          href="/lien/liens"
        />
        <StatCard
          title="Total Cases"
          total={472}
          segments={[
            { label: 'Case Settled', value: 1,   color: '#ec4899' },
            { label: 'Demand Sent',  value: 1,   color: '#6366f1' },
            { label: 'Pre-demand',   value: 470, color: '#f472b6' },
          ]}
          href="/lien/cases"
        />
        <StatCard
          title="Law Firm Case Allocation"
          total={472}
          icon="ri-scales-3-line"
          segments={[
            { label: 'Cortex QA LawFirm',    value: 4,   color: '#a78bfa' },
            { label: 'Cortex TBI of Nevada', value: 462, color: '#4f46e5' },
            { label: 'QA Lawfirm',           value: 6,   color: '#818cf8' },
          ]}
          href="/lien/cases"
        />
        <StatCard
          title="Medical Facility Case Allocation"
          total={2214}
          icon="ri-add-line"
          segments={[
            { label: '',                  value: 2203, color: '#22d3ee' },
            { label: 'Test med Disclosure', value: 7,  color: '#0ea5e9' },
            { label: 'Cortex Med Facility', value: 4,  color: '#38bdf8' },
          ]}
          href="/lien/cases"
        />
      </div>
    </div>
  );
}

// ── Stat card with SVG donut ──────────────────────────────────────────────────

interface Segment { label: string; value: number; color: string; }

function StatCard({
  title, total, segments, href, icon = 'ri-file-list-line',
}: {
  title: string;
  total: number;
  segments: Segment[];
  href: string;
  icon?: string;
}) {
  const grandTotal = segments.reduce((s, seg) => s + seg.value, 0);
  const dominant   = segments.reduce((a, b) => a.value > b.value ? a : b);
  const pct        = grandTotal > 0 ? ((dominant.value / grandTotal) * 100).toFixed(1) : '0';

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-5 flex flex-col gap-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold text-gray-800">{title}</h2>
        <Link
          href={href}
          className="flex items-center gap-1.5 text-xs text-gray-500 hover:text-gray-700 border border-gray-200 rounded-lg px-3 py-1.5 hover:bg-gray-50 transition-colors"
        >
          <i className="ri-file-list-line text-sm leading-none" />
          View Details
        </Link>
      </div>

      {/* Sub-label */}
      <div className="flex items-center gap-1.5 text-xs text-gray-400">
        <i className={`${icon} text-sm leading-none`} />
        <span>{title}</span>
      </div>

      {/* Body: number + legend on left, donut on right */}
      <div className="flex items-center gap-6">
        {/* Left: big number + legend */}
        <div className="flex flex-col gap-3 flex-1 min-w-0">
          <p className="text-[32px] font-bold text-gray-900 leading-none">
            {total.toLocaleString()}
          </p>
          <ul className="space-y-1.5">
            {segments.map((seg, i) => (
              <li key={i} className="flex items-center justify-between gap-4 text-xs text-gray-600">
                <span className="flex items-center gap-1.5">
                  <span className="w-2 h-2 rounded-full shrink-0" style={{ backgroundColor: seg.color }} />
                  {seg.label || <span className="text-gray-400 italic">Other</span>}
                </span>
                <span className="font-medium text-gray-700 tabular-nums">
                  {seg.value.toLocaleString()}
                </span>
              </li>
            ))}
          </ul>
        </div>

        {/* Right: donut chart */}
        <div className="shrink-0">
          <DonutChart segments={segments} pctLabel={`${pct}%`} />
        </div>
      </div>
    </div>
  );
}

// ── SVG donut chart ───────────────────────────────────────────────────────────

function DonutChart({ segments, pctLabel }: { segments: Segment[]; pctLabel: string }) {
  const SIZE  = 148;
  const CX    = SIZE / 2;
  const CY    = SIZE / 2;
  const R     = 54;
  const SW    = 22; // stroke-width
  const CIRC  = 2 * Math.PI * R;

  const total = segments.reduce((s, seg) => s + seg.value, 0);

  // Build arc slices
  const arcs: { offset: number; dash: string; color: string }[] = [];
  let cumulative = 0;

  for (const seg of segments) {
    const fraction = total > 0 ? seg.value / total : 0;
    const arcLen   = fraction * CIRC;
    // offset: negative means the arc starts at (cumulative into circle) rotated from top
    arcs.push({
      color:  seg.color,
      dash:   `${arcLen} ${CIRC - arcLen}`,
      offset: CIRC / 4 - cumulative,   // start from 12-o'clock
    });
    cumulative += arcLen;
  }

  return (
    <svg width={SIZE} height={SIZE} viewBox={`0 0 ${SIZE} ${SIZE}`}>
      {/* Track ring */}
      <circle cx={CX} cy={CY} r={R} fill="none" stroke="#f3f4f6" strokeWidth={SW} />

      {/* Data arcs */}
      {arcs.map((arc, i) => (
        <circle
          key={i}
          cx={CX}
          cy={CY}
          r={R}
          fill="none"
          stroke={arc.color}
          strokeWidth={SW}
          strokeDasharray={arc.dash}
          strokeDashoffset={arc.offset}
          strokeLinecap="butt"
        />
      ))}

      {/* Centre label */}
      <text
        x={CX}
        y={CY + 5}
        textAnchor="middle"
        fontSize="13"
        fontWeight="600"
        fill="#374151"
      >
        {pctLabel}
      </text>
    </svg>
  );
}
