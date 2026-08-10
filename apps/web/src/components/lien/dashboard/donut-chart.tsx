'use client';

import { useMemo } from 'react';
import { Pie, PieChart, Cell } from 'recharts';
import { ChartContainer, ChartTooltip, ChartTooltipContent, type ChartConfig } from '@/components/ui/chart';
import type { Segment } from './types';

const RADIAN = Math.PI / 180;

function makePercentLabel(fontSize: number) {
  return function renderPercentLabel({
    cx = 0,
    cy = 0,
    midAngle = 0,
    innerRadius = 0,
    outerRadius = 0,
    percent = 0,
  }: {
    cx?: number;
    cy?: number;
    midAngle?: number;
    innerRadius?: number;
    outerRadius?: number;
    percent?: number;
  }) {
    if (percent < 0.03) return null;
    const radius = innerRadius + (outerRadius - innerRadius) * 0.5;
    const x = cx + radius * Math.cos(-midAngle * RADIAN);
    const y = cy + radius * Math.sin(-midAngle * RADIAN);
    return (
      <text x={x} y={y} fill="#fff" textAnchor="middle" dominantBaseline="central" fontSize={fontSize} fontWeight={600} className="pointer-events-none">
        {(percent * 100).toFixed(1)}%
      </text>
    );
  };
}

export function DonutChart({ segments, size = 240 }: { segments: Segment[]; size?: number }) {
  const chartConfig = useMemo(() => {
    const cfg: ChartConfig = {};
    segments.forEach((seg) => { cfg[seg.label] = { label: seg.label, color: seg.color }; });
    return cfg;
  }, [segments]);

  const renderPercentLabel = useMemo(() => makePercentLabel(Math.max(7, size * 0.05)), [size]);

  return (
    <div className="relative shrink-0" style={{ width: size, height: size }}>
      <ChartContainer config={chartConfig} className="mx-auto aspect-square" style={{ width: size, height: size }}>
        <PieChart>
          <ChartTooltip cursor={false} content={<ChartTooltipContent hideLabel nameKey="label" />} />
          <Pie
            data={segments}
            dataKey="value"
            nameKey="label"
            innerRadius={size * 0.30}
            outerRadius={size * 0.5}
            strokeWidth={3}
            stroke="#fff"
            label={renderPercentLabel}
            labelLine={false}
            isAnimationActive={false}
          >
            {segments.map((seg) => (
              <Cell key={seg.label} fill={seg.color} />
            ))}
          </Pie>
        </PieChart>
      </ChartContainer>
    </div>
  );
}
