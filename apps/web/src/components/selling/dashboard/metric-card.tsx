interface MetricCardProps {
  label: string;
  trend?: "up" | "down" | undefined;
  trendDescription?: string;
  value: number;
  description?: string;
  statsPercentage?: number;
  formatAsCurrency: boolean;
}

export function MetricCard({
  label,
  trend,
  trendDescription,
  value,
  description,
  statsPercentage,
  formatAsCurrency,
}: MetricCardProps) {
  const displayValue =
    typeof value === "number" && formatAsCurrency
      ? `$${value.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
      : value;
  return (
    <article
      key={label}
      className="rounded-2xl border border-neutral-200 bg-white p-6 shadow-[0_1px_1.5px_rgba(0,0,0,0.1)]"
    >
      <div className="flex items-start justify-between gap-3">
        <p className="text-sm leading-5 text-neutral-500">{label}</p>
        {trend && (
          <span
            className={
              trend === "up"
                ? "inline-flex items-center gap-1 rounded-full bg-[#17c964]/15 px-2 py-0.5 text-xs font-medium text-[#15803d]"
                : "inline-flex items-center gap-1 rounded-full bg-[#ef4444]/10 px-2 py-0.5 text-xs font-medium text-[#dc2626]"
            }
          >
            <i
              className={
                trend === "up"
                  ? "ri-arrow-right-up-line"
                  : "ri-arrow-right-down-line"
              }
              aria-hidden
            />
            {statsPercentage}%
          </span>
        )}
      </div>
      <p className="mt-3 text-2xl font-bold leading-8">{displayValue}</p>
      <p className="mt-5 text-xs font-bold text-neutral-950">
        {trendDescription ?? ""}
        {trend && (
          <i
            className={
              trend === "up"
                ? "ri-arrow-right-up-line"
                : "ri-arrow-right-down-line"
            }
            aria-hidden
          />
        )}
      </p>
      <p className="mt-1 text-xs leading-5 text-neutral-500">{description}</p>
    </article>
  );
}
