export interface StatCardData {
  label: string;
  value: string;
  trend: string;
  trendTone: 'positive' | 'negative';
}

export interface DonutSlice {
  label: string;
  value: number;
  color: string;
  amount?: string;
  percent?: string;
  details?: Array<{ label: string; value: string }>;
}

export const ORANGE = '#f97332';
export const BLUE = '#3b82f6';
export const GREEN = '#22c55e';
export const YELLOW = '#f5b800';
export const RED = '#ef4444';
export const MUTED = '#8f929b';

export const SLICE_COLORS = [BLUE, ORANGE, GREEN, YELLOW, RED];

export const LEGEND_PAGE_SIZE = 5;
