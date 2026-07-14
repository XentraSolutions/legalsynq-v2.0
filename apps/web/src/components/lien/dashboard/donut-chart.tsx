'use client';

import { useMemo } from 'react';
import { Pie, PieChart, Cell } from 'recharts';
import { ChartContainer, ChartTooltip, ChartTooltipContent, type ChartConfig } from '@/components/ui/chart';
import type { Segment } from './types';

export function DonutChart({ segments, pctLabel, size = 120 }: { segments: Segment[]; pctLabel: string; size?: number }) {
  const chartConfig = useMemo(() => {
    const cfg: ChartConfig = {};
    segments.forEach((seg) => { cfg[seg.label] = { label: seg.label, color: seg.color }; });
    return cfg;
  }, [segments]);

  return (
    <div className="relative shrink-0" style={{ width: size, height: size }}>
      <ChartContainer config={chartConfig} className="mx-auto aspect-square" style={{ width: size, height: size }}>
        <PieChart>
          <ChartTooltip cursor={false} content={<ChartTooltipContent hideLabel nameKey="label" />} />
          <Pie
            data={segments}
            dataKey="value"
            nameKey="label"
            innerRadius={size * 0.37}
            outerRadius={size * 0.5}
            strokeWidth={3}
            stroke="#fff"
          >
            {segments.map((seg) => (
              <Cell key={seg.label} fill={seg.color} />
            ))}
          </Pie>
        </PieChart>
      </ChartContainer>
      <div className="pointer-events-none absolute inset-0 flex items-center justify-center">
        <span className="text-xs font-semibold text-gray-700">{pctLabel}</span>
      </div>
    </div>
  );
}
