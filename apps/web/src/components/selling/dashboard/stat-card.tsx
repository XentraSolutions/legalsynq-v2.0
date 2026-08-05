"use client";

import { DonutChart } from "@/components/lien/dashboard/donut-chart";
import { AdditionalStat, Segment } from "@/components/lien/dashboard/types";
import Link from "next/link";

export function StatCard({
  title,
  total,
  segments,
  additionalStats,
  icon = "ri-todo-line",
  totalStats,
  statsType,
  showHeaderStat = true,
  centerValue,
  centerLabel,
  valueFormat = "currency",
}: {
  title: string;
  total: number;
  segments: Segment[];
  additionalStats?: AdditionalStat[];
  icon?: string;
  totalStats: number;
  statsType: string;
  /** Hides the "Total X: $Y" line in the header — some cards (e.g. Liens by Status) only show a title. */
  showHeaderStat?: boolean;
  /** Big value shown in the center of the donut, e.g. "$3.8M" or "1,248". */
  centerValue?: string;
  /** Small label under the center value, e.g. "Total A/R". */
  centerLabel?: string;
  /** How to format each legend row's value. */
  valueFormat?: "currency" | "number";
}) {
  const filteredSegments = segments.filter((s) => s.value > 0);
  const segmentTotal = filteredSegments.reduce((sum, s) => sum + s.value, 0);
  const displayValue =
    typeof totalStats === "number"
      ? `$${totalStats.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
      : totalStats;
  return (
    <div className="bg-white rounded-xl border border-gray-200 flex flex-col gap-4">
      <div className="flex items-center justify-between border-b border-gray-100 p-5">
        <h2 className="text-sm font-semibold text-gray-800">{title}</h2>
        {showHeaderStat && (
          <p className="text-sm font-semibold text-gray-800">
            Total {statsType} <span className="text-gray-600 font-normal">{displayValue}</span>
          </p>
        )}
      </div>
      <div className="flex items-start gap-4 p-5">
        <div className="shrink-0">
          <DonutChart
            size={180}
            segments={
              filteredSegments.length > 0
                ? filteredSegments
                : [{ label: "None", value: 1, color: "#e5e7eb" }]
            }
            centerValue={centerValue}
            centerLabel={centerLabel}
          />
        </div>
        <div className="flex flex-col flex-1 min-w-0">
          <div className="space-y-3 mb-4">
            {additionalStats?.map((stat, i) => (
              <div key={i} className="flex items-start gap-2">
                <i
                  className={`${icon} text-gray-400 text-sm mt-0.5 shrink-0`}
                />
                <div>
                  <p className="text-xs text-gray-500">{stat.label}</p>
                  <p className="text-sm font-bold text-blue-600">
                    {stat.value}
                  </p>
                </div>
              </div>
            ))}
          </div>
          {additionalStats && additionalStats?.length > 0 && (
            <hr className="border-gray-100 mb-3" />
          )}
          <ul className="space-y-5">
            {filteredSegments.map((seg, i) => {
              const percent =
                seg.legendPercent ??
                (segmentTotal > 0 ? (seg.value / segmentTotal) * 100 : 0);
              const formattedValue =
                valueFormat === "currency"
                  ? `$${seg.value.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
                  : seg.value.toLocaleString();
              return (
              <li key={i}>
                <div className="flex items-center justify-between text-xs">
                  <span className="flex items-center gap-2 whitespace-nowrap">
                    <span
                      className="h-3 w-2 shrink-0 rounded-xl"
                      style={{ backgroundColor: seg.color }}
                    />
                    <span className="font-medium text-gray-800">
                      {seg.label}
                    </span>
                  </span>
                  <span className="tabular-nums whitespace-nowrap font-medium text-gray-900">
                    {formattedValue}{" "}
                    <span className="font-normal text-gray-400">
                      ({percent.toFixed(1)}%)
                    </span>
                  </span>
                </div>
                {seg.subStats && seg.subStats.length > 0 && (
                  <ul className="mt-1 space-y-0.5 pl-3.5">
                    {seg.subStats.map((sub, j) => (
                      <li
                        key={j}
                        className="flex items-center justify-between text-xs text-gray-400"
                      >
                        <span>{sub.label}</span>
                        <span className="tabular-nums">{sub.value}</span>
                      </li>
                    ))}
                  </ul>
                )}
              </li>
              );
            })}
          </ul>
        </div>
      </div>
    </div>
  );
}
