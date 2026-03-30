import type { PlatformReadinessSummary } from '@/types/control-center';

// ── Sub-components ─────────────────────────────────────────────────────────

function CoverageBar({ pct }: { pct: number }) {
  const colour = pct >= 90 ? 'bg-emerald-500' : pct >= 60 ? 'bg-amber-400' : 'bg-red-500';
  return (
    <div className="flex items-center gap-3">
      <div className="flex-1 h-2.5 bg-gray-100 rounded-full overflow-hidden">
        <div
          className={`h-full rounded-full transition-all ${colour}`}
          style={{ width: `${Math.min(Math.max(pct, 0), 100)}%` }}
        />
      </div>
      <span className="text-sm font-semibold tabular-nums w-14 text-right text-gray-700">
        {pct.toFixed(1)}%
      </span>
    </div>
  );
}

function StatRow({
  label,
  value,
  pill,
  pillColour = 'bg-gray-100 text-gray-600',
}: {
  label:        string;
  value:        number | string | boolean;
  pill?:        string;
  pillColour?:  string;
}) {
  const display = typeof value === 'boolean' ? (value ? 'Yes' : 'No') : String(value);
  return (
    <div className="flex items-center justify-between py-1.5">
      <span className="text-sm text-gray-500">{label}</span>
      <div className="flex items-center gap-2">
        {pill && (
          <span className={`text-[11px] font-medium px-2 py-0.5 rounded-full ${pillColour}`}>
            {pill}
          </span>
        )}
        <span className="text-sm font-semibold text-gray-800 tabular-nums">{display}</span>
      </div>
    </div>
  );
}

function SectionCard({ title, subtitle, children }: {
  title:    string;
  subtitle: string;
  children: React.ReactNode;
}) {
  return (
    <div className="bg-white border border-gray-200 rounded-xl p-6 space-y-4">
      <div>
        <h2 className="text-base font-semibold text-gray-900">{title}</h2>
        <p className="text-xs text-gray-400 mt-0.5">{subtitle}</p>
      </div>
      {children}
    </div>
  );
}

// ── Main component ─────────────────────────────────────────────────────────

interface PlatformReadinessCardProps {
  summary: PlatformReadinessSummary;
}

export function PlatformReadinessCard({ summary }: PlatformReadinessCardProps) {
  const { phaseGCompletion: pg, orgTypeCoverage: ot, productRoleEligibility: pr,
          orgRelationships: or, scopedAssignmentsByScope: sa } = summary;

  const allGreen =
    pg.userRolesRetired &&
    pg.soleRoleSourceIsSra &&
    ot.consistent &&
    pr.coveragePct >= 100 &&
    ot.coveragePct >= 100;

  return (
    <div className="space-y-6">

      {/* Timestamp + overall status */}
      <div className="flex items-center justify-between">
        <p className="text-xs text-gray-400">
          Snapshot generated at{' '}
          <time dateTime={summary.generatedAtUtc}>
            {new Date(summary.generatedAtUtc).toLocaleString()}
          </time>{' '}
          UTC · Refreshes every 30 s
        </p>
        <span className={`text-xs font-semibold px-3 py-1 rounded-full ${
          allGreen ? 'bg-emerald-100 text-emerald-700' : 'bg-amber-100 text-amber-700'
        }`}>
          {allGreen ? '✓ Platform Ready' : '⚠ Attention Required'}
        </span>
      </div>

      {/* Phase G — UserRoles retirement */}
      <SectionCard
        title="Phase G — Role Source Migration"
        subtitle="UserRoles and UserRoleAssignments tables retired. ScopedRoleAssignments (GLOBAL) is the sole authoritative role source."
      >
        <CoverageBar pct={pg.userRolesRetired ? 100 : 0} />
        <div className="divide-y divide-gray-50">
          <StatRow
            label="UserRoles table retired"
            value={pg.userRolesRetired}
            pill={pg.userRolesRetired ? 'complete' : 'pending'}
            pillColour={pg.userRolesRetired ? 'bg-emerald-100 text-emerald-700' : 'bg-red-100 text-red-700'}
          />
          <StatRow
            label="SRA is sole role source"
            value={pg.soleRoleSourceIsSra}
            pill={pg.soleRoleSourceIsSra ? 'complete' : 'pending'}
            pillColour={pg.soleRoleSourceIsSra ? 'bg-emerald-100 text-emerald-700' : 'bg-red-100 text-red-700'}
          />
          <StatRow label="Users with scoped role"              value={pg.usersWithScopedRole}          />
          <StatRow label="Global scoped assignments"           value={pg.globalScopedAssignments}      />
          <StatRow label="Total active scoped assignments"     value={pg.totalActiveScopedAssignments} />
        </div>
      </SectionCard>

      {/* Org type coverage */}
      <SectionCard
        title="Org Type Coverage"
        subtitle="Percentage of active organizations with a valid OrganizationTypeId foreign key."
      >
        <CoverageBar pct={ot.coveragePct} />
        <div className="divide-y divide-gray-50">
          <StatRow label="Total active organizations"           value={ot.totalActiveOrgs}            />
          <StatRow label="With OrganizationTypeId"             value={ot.orgsWithOrganizationTypeId} />
          <StatRow label="Missing TypeId"                      value={ot.orgsWithMissingTypeId}      />
          <StatRow label="Code mismatch"                       value={ot.orgsWithCodeMismatch}       />
          <StatRow
            label="Data consistent"
            value={ot.consistent}
            pill={ot.consistent ? 'consistent' : 'inconsistent'}
            pillColour={ot.consistent ? 'bg-emerald-100 text-emerald-700' : 'bg-red-100 text-red-700'}
          />
        </div>
      </SectionCard>

      {/* Product role eligibility */}
      <SectionCard
        title="Product Role Eligibility"
        subtitle="Percentage of active product roles covered by an OrgTypeRule (DB path)."
      >
        <CoverageBar pct={pr.coveragePct} />
        <div className="divide-y divide-gray-50">
          <StatRow label="Total active product roles" value={pr.totalActiveProductRoles} />
          <StatRow label="With OrgType rule"          value={pr.withOrgTypeRule}         />
          <StatRow label="Unrestricted"               value={pr.unrestricted}            />
        </div>
      </SectionCard>

      {/* Org relationships */}
      <SectionCard
        title="Organization Relationships"
        subtitle="Live graph edges between organizations used for referral auto-linking."
      >
        <div className="divide-y divide-gray-50">
          <StatRow label="Total relationships"  value={or.total}  />
          <StatRow label="Active relationships" value={or.active} />
        </div>
      </SectionCard>

      {/* Scoped assignments by scope type */}
      <SectionCard
        title="Scoped Assignments by Scope Type"
        subtitle="Confirms real non-global scope enforcement is in use at runtime (Phase I)."
      >
        <div className="divide-y divide-gray-50">
          <StatRow label="Global"       value={sa.global}       />
          <StatRow label="Organization" value={sa.organization} />
          <StatRow label="Product"      value={sa.product}      />
          <StatRow label="Relationship" value={sa.relationship} />
          <StatRow label="Tenant"       value={sa.tenant}       />
        </div>
      </SectionCard>

    </div>
  );
}
