export default function AuthorizationUsersPage() {
  return (
    <div className="rounded-xl border border-gray-200 bg-white p-8">
      <div className="flex items-center gap-3 mb-4">
        <div className="w-10 h-10 rounded-lg bg-blue-50 flex items-center justify-center">
          <i className="ri-user-line text-xl text-blue-600" />
        </div>
        <div>
          <h2 className="text-base font-semibold text-gray-900">Users Management</h2>
          <p className="text-sm text-gray-500">Coming in LS-TENANT-002</p>
        </div>
      </div>
      <p className="text-sm text-gray-600 leading-relaxed">
        Manage tenant user accounts, assign roles, and configure individual access permissions.
        This module will provide full CRUD operations for user lifecycle management within your tenant.
      </p>
      <div className="mt-6 flex items-center gap-2 text-xs text-gray-400">
        <i className="ri-time-line" />
        <span>Scheduled for next release</span>
      </div>
    </div>
  );
}
