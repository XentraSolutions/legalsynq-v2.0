"use client";

import Link from "next/link";
import { DonutChart } from "./donut-chart";
import type { AdditionalStat, Segment } from "./types";

export function StatCard({
  title,
  total,
  segments,
  href,
  additionalStats,
  icon = "ri-todo-line",
  onViewDetails,
}: {
  title: string;
  total: number;
  segments: Segment[];
  href: string;
  additionalStats?: AdditionalStat[];
  icon?: string;
  onViewDetails?: () => void;
}) {
  const filteredSegments = segments.filter((s) => s.value > 0);

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-5 flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold text-gray-800">{title}</h2>
        {onViewDetails ? (
          <button
            onClick={onViewDetails}
            className="flex items-center gap-1.5 text-xs text-gray-500 hover:text-gray-700 border border-gray-200 rounded-lg px-3 py-1.5 hover:bg-gray-50 transition-colors"
          >
            <i className="ri-file-list-line text-sm leading-none" />
            View Details
          </button>
        ) : (
          <Link
            href={href}
            className="flex items-center gap-1.5 text-xs text-gray-500 hover:text-gray-700 border border-gray-200 rounded-lg px-3 py-1.5 hover:bg-gray-50 transition-colors"
          >
            <i className="ri-file-list-line text-sm leading-none" />
            View Details
          </Link>
        )}
      </div>
      <div className="flex items-start gap-6">
        <div className="flex flex-col flex-1 min-w-0">
          <div className="space-y-3 mb-4">
            <div className="flex items-start gap-2">
              <i className={`${icon} text-gray-400 text-sm mt-0.5 shrink-0`} />
              <div>
                <p className="text-xs text-gray-500">{title}</p>
                <p className="text-2xl font-bold text-blue-600 leading-tight">
                  {total.toLocaleString()}
                </p>
              </div>
            </div>
            <div className="max-h-32 overflow-auto">
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
          </div>
          {filteredSegments.length > 0 && (
            <hr className="border-gray-100 mb-3" />
          )}
          <ul className="space-y-2 max-h-32 overflow-auto">
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
        <div className="shrink-0">
          <DonutChart
            segments={
              filteredSegments.length > 0
                ? filteredSegments
                : [{ label: "None", value: 1, color: "#e5e7eb" }]
            }
          />
        </div>
      </div>
    </div>
  );
}
