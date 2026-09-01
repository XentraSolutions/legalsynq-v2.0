"use client";

import { useId } from "react";
import { Area, AreaChart, CartesianGrid, Tooltip, XAxis, YAxis } from "recharts";
import { ChartContainer, type ChartConfig } from "@/components/ui/chart";

export interface LiensOverTimePoint {
  month: string;
  value: number;
}

const chartConfig: ChartConfig = {
  value: { label: "Revenue", color: "#3b82f6" },
};

function formatCurrency(value: number): string {
  return `$${value.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

function LiensOverTimeTooltip({
  active,
  payload,
  label,
}: {
  active?: boolean;
  payload?: { value: number }[];
  label?: string;
}) {
  if (!active || !payload?.length) return null;
  return (
    <div className="rounded-lg border border-gray-100 bg-white px-3 py-2 text-xs shadow-lg">
      <div className="mb-1 font-medium text-gray-900">{label}</div>
      <div className="flex items-center gap-1.5">
        <span className="h-2 w-2 shrink-0 rounded-full bg-blue-500" />
        <span className="font-medium text-gray-700">
          {formatCurrency(payload[0].value)}
        </span>
      </div>
    </div>
  );
}

export function LiensOverTimeChart({ data }: { data: LiensOverTimePoint[] }) {
  // Scoped per-instance so two charts on the same page never emit duplicate SVG ids
  // (duplicate ids would make `fill="url(#...)"` resolve to whichever gradient the
  // browser finds first in the DOM, per the SVG spec).
  const gradientId = `liensOverTimeFill-${useId().replace(/:/g, "")}`;
  if (data.length === 0) {
    return (
      <div className="flex h-64 w-full items-center justify-center text-sm text-gray-400">
        No data available.
      </div>
    );
  }
  return (
    <ChartContainer config={chartConfig} className="aspect-auto h-64 w-full">
      <AreaChart data={data} margin={{ left: 16, right: 16, top: 10, bottom: 0 }}>
        <defs>
          <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
            <stop offset="5%" stopColor="#3b82f6" stopOpacity={0.35} />
            <stop offset="95%" stopColor="#3b82f6" stopOpacity={0.02} />
          </linearGradient>
        </defs>
        <CartesianGrid vertical={false} strokeDasharray="3 3" />
        <XAxis
          dataKey="month"
          tickLine={false}
          axisLine={false}
          tickMargin={10}
          fontSize={11}
          interval={0}
        />
        <YAxis hide domain={["dataMin - 500000", "dataMax + 500000"]} />
        <Tooltip cursor={{ stroke: "#e5e7eb", strokeWidth: 1 }} content={<LiensOverTimeTooltip />} />
        <Area
          dataKey="value"
          type="monotone"
          fill={`url(#${gradientId})`}
          stroke="#3b82f6"
          strokeWidth={2}
          activeDot={{ r: 5, strokeWidth: 2, stroke: "#fff" }}
          isAnimationActive={false}
        />
      </AreaChart>
    </ChartContainer>
  );
}
