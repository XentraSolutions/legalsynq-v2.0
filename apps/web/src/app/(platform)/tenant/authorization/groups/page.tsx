export default function AuthorizationGroupsPage() {
  return (
    <div className="rounded-xl border border-gray-200 bg-white p-8">
      <div className="flex items-center gap-3 mb-4">
        <div className="w-10 h-10 rounded-lg bg-indigo-50 flex items-center justify-center">
          <i className="ri-group-line text-xl text-indigo-600" />
        </div>
        <div>
          <h2 className="text-base font-semibold text-gray-900">Group Management</h2>
          <p className="text-sm text-gray-500">Coming in LS-TENANT-003</p>
        </div>
      </div>
      <p className="text-sm text-gray-600 leading-relaxed">
        Create and manage authorization groups to organize users and streamline permission assignments.
        Groups enable role-based access control at scale across your tenant.
      </p>
      <div className="mt-6 flex items-center gap-2 text-xs text-gray-400">
        <i className="ri-time-line" />
        <span>Scheduled for upcoming release</span>
      </div>
    </div>
  );
}
