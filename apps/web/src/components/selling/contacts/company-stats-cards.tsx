function StatCard({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="bg-white border border-gray-200 rounded-xl px-6 py-5">
      <p className="text-sm text-gray-400">{label}</p>
      <p className="text-2xl font-bold text-gray-900 mt-1">{value}</p>
    </div>
  );
}

// Company-level case/billing stats have no backing endpoint yet (companies
// list only exposes contact info, not case counts or billing totals). Cards
// render as placeholders until that stats API exists.
export function CompanyStatsCards() {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
      <StatCard label="Total Cases" value="—" />
      <StatCard label="Active Cases" value="—" />
      <StatCard label="Total Billing For Active Case" value="—" />
    </div>
  );
}
