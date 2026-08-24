import { ArrowDownRight, ArrowUpRight } from "lucide-react";

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
  const TrendIcon = trend === "up" ? ArrowUpRight : ArrowDownRight;
  return (
    <article
      key={label}
      className="rounded-2xl border border-neutral-200 bg-white p-6 shadow-[0_1px_1.5px_rgba(0,0,0,0.1)]"
    >
      <div className="flex items-start justify-between gap-3">
        <p className="text-sm leading-5 text-neutral-500 break-words">
          {label}
        </p>
        {trend ? (
          <span
            className={
              trend === "up"
                ? "inline-flex items-center gap-1 rounded-full bg-[#17c964]/15 px-2 py-0.5 text-xs font-medium text-[#15803d] break-words"
                : "inline-flex items-center gap-1 rounded-full bg-[#ef4444]/10 px-2 py-0.5 text-xs font-medium text-[#dc2626] break-words"
            }
          >
            <TrendIcon className="h-4 w-4" aria-hidden />
            {statsPercentage}%
          </span>
        ) : (
          <span className="inline-flex items-center gap-1 rounded-full bg-neutral-100 px-2 py-0.5 text-xs font-medium text-neutral-400 break-words">
            —
          </span>
        )}
      </div>
      <p className="mt-3 text-2xl font-bold leading-8 break-words">
        {displayValue}
      </p>
      <p className="mt-5 text-xs font-bold text-neutral-950">
        {trend ? (
          <>
            {trendDescription ?? ""}
            <TrendIcon className="h-4 w-4 inline" aria-hidden />
          </>
        ) : (
          <span className="text-neutral-400">No trend data</span>
        )}
      </p>
      <p className="mt-1 text-xs leading-5 text-neutral-500">
        {trend ? description : "—"}
      </p>
    </article>
  );
}
