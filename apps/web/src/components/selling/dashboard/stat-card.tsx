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
}: {
  title: string;
  total: number;
  segments: Segment[];
  additionalStats?: AdditionalStat[];
  icon?: string;
  totalStats: number;
  statsType: string;
}) {
  const filteredSegments = segments.filter((s) => s.value > 0);
  const displayValue =
    typeof totalStats === "number"
      ? `$${totalStats.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
      : totalStats;
  return (
    <div className="bg-white rounded-xl border border-gray-200 flex flex-col gap-4">
      <div className="flex items-center justify-between border-b border-gray-100 p-5">
        <h2 className="text-sm font-semibold text-gray-800">{title}</h2>
        <p className="text-sm font-semibold text-gray-800">
          Total {statsType}
          <span className="text-gray-600">{displayValue}</span>
        </p>
      </div>
      <div className="flex items-start gap-6 p-6 px-8">
        <div className="shrink-0">
          <DonutChart
            segments={
              filteredSegments.length > 0
                ? filteredSegments
                : [{ label: "None", value: 1, color: "#e5e7eb" }]
            }
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
          <ul className="space-y-2">
            {filteredSegments.map((seg, i) => (
              <li key={i}>
                <div className="flex items-center justify-between text-xs">
                  <span className="flex items-center gap-1.5">
                    <span
                      className="w-2 h-2 rounded-full shrink-0"
                      style={{ backgroundColor: seg.color }}
                    />
                    <span className="text-gray-700 font-medium">
                      {seg.label}
                    </span>
                  </span>
                  <span className="font-medium text-gray-700 tabular-nums">
                    {seg.value.toLocaleString()}
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
            ))}
          </ul>
        </div>
      </div>
    </div>
  );
}
