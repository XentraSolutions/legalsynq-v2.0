export default function AuthorizationAccessPage() {
  return (
    <div className="rounded-xl border border-gray-200 bg-white p-8">
      <div className="flex items-center gap-3 mb-4">
        <div className="w-10 h-10 rounded-lg bg-emerald-50 flex items-center justify-center">
          <i className="ri-shield-keyhole-line text-xl text-emerald-600" />
        </div>
        <div>
          <h2 className="text-base font-semibold text-gray-900">Access &amp; Explainability</h2>
          <p className="text-sm text-gray-500">Coming in LS-TENANT-004</p>
        </div>
      </div>
      <p className="text-sm text-gray-600 leading-relaxed">
        View and audit access policies, understand why users have specific permissions, and trace
        authorization decisions with full explainability across your tenant resources.
      </p>
      <div className="mt-6 flex items-center gap-2 text-xs text-gray-400">
        <i className="ri-time-line" />
        <span>Scheduled for upcoming release</span>
      </div>
    </div>
  );
}
