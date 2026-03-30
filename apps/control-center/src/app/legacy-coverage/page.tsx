import { requirePlatformAdmin }              from '@/lib/auth-guards';
import { controlCenterServerApi }            from '@/lib/control-center-api';
import { CCShell }                           from '@/components/shell/cc-shell';
import { LegacyCoverageCard }               from '@/components/platform/legacy-coverage-card';

/**
 * /legacy-coverage — Legacy Migration Coverage Report (Step 4)
 *
 * Access: PlatformAdmin only.
 *
 * Shows a point-in-time snapshot of two active legacy migration paths:
 *
 *   1. EligibleOrgType → ProductOrganizationTypeRule
 *      Tracks how many ProductRoles have been fully migrated to the DB-rule
 *      eligibility path vs still relying on the legacy EligibleOrgType string.
 *      Phase F can only begin once legacyStringOnly === 0.
 *
 *   2. UserRoles → ScopedRoleAssignment (GLOBAL scope)
 *      Tracks dual-write adoption from AuthService. Once all users have
 *      ScopedRoleAssignment records the legacy UserRoles write path can be
 *      removed.
 *
 * Data: GET /identity/api/admin/legacy-coverage (cached 10 s, tag: cc:legacy-coverage).
 */
export default async function LegacyCoveragePage() {
  const session = await requirePlatformAdmin();

  let report    = null;
  let fetchError: string | null = null;

  try {
    report = await controlCenterServerApi.legacyCoverage.get();
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load legacy coverage report.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        {/* Header */}
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Legacy Migration Coverage</h1>
          <p className="mt-0.5 text-sm text-gray-500">
            Real-time snapshot of legacy path adoption across two active migration streams.
            Both metrics must reach 100% before the legacy write paths can be removed.
          </p>
        </div>

        {/* Action bar — contextual help */}
        <div className="flex items-start gap-3 bg-blue-50 border border-blue-200 rounded-lg px-4 py-3 text-xs text-blue-700">
          <span className="shrink-0 mt-0.5">ℹ️</span>
          <div>
            <strong>Migration targets:</strong> Eligibility rules must have{' '}
            <code className="bg-blue-100 px-1 rounded">legacyStringOnly = 0</code> before Phase F
            can begin. Role-assignment dual-write must reach{' '}
            <code className="bg-blue-100 px-1 rounded">100%</code> before the legacy{' '}
            <code className="bg-blue-100 px-1 rounded">UserRole</code> write path is retired.
            This page auto-refreshes every 10 seconds.
          </div>
        </div>

        {/* Error banner */}
        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Coverage report */}
        {report && <LegacyCoverageCard report={report} />}

      </div>
    </CCShell>
  );
}
