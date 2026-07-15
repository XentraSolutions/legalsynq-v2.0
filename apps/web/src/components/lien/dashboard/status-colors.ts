import { CHART_COLORS } from './chart-colors';

// Mirrors the legacy admin portal (legalsynq-synqlien-admin-portal): a single
// CHART_COLORS palette assigned purely by segment position, not a per-status
// map. As long as a segment list is built once and that same array/order is
// reused everywhere it's rendered (stat card, donut, legend, modal tiles),
// index-based assignment still keeps every view of the same segment in sync.
export function getAllocationColor(index: number): string {
  return CHART_COLORS[index % CHART_COLORS.length];
}

// Light tint of a segment's own color, used for tile/legend backgrounds so
// they visually match the donut slice and legend dot instead of an unrelated
// fixed palette.
export function tintColor(hex: string, alpha = 0.12): string {
  const parsed = hex.replace('#', '');
  const r = parseInt(parsed.substring(0, 2), 16);
  const g = parseInt(parsed.substring(2, 4), 16);
  const b = parseInt(parsed.substring(4, 6), 16);
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}
