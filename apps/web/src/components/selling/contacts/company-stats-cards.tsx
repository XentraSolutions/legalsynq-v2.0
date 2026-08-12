function StatCard({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="bg-white border border-gray-200 rounded-xl px-6 py-5">
      <p className="text-sm text-gray-400">{label}</p>
      <p className="text-2xl font-bold text-gray-900 mt-1">{value}</p>
    </div>
  );
}

function formatCurrency(amount?: number): string {
  if (amount == null) return "—";
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
    maximumFractionDigits: 0,
  }).format(amount);
}

export function CompanyStatsCards({
  totalCases,
  activeCases,
  totalBillingForActiveCases,
}: {
  totalCases?: number;
  activeCases?: number;
  totalBillingForActiveCases?: number;
}) {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
      <StatCard label="Total Cases" value={totalCases ?? "—"} />
      <StatCard label="Active Cases" value={activeCases ?? "—"} />
      <StatCard
        label="Total Billing For Active Case"
        value={formatCurrency(totalBillingForActiveCases)}
      />
    </div>
  );
}
