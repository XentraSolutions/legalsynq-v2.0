export interface SubStat {
  label: string;
  value: string | number;
}

export interface Segment {
  label: string;
  value: number;
  color: string;
  subStats?: SubStat[];
  /** Overrides the computed value/total percentage shown in legends, for sources whose segment values don't sum to the displayed total.
   * Named distinctly from "percent" — Recharts' Pie reads a same-named field off the raw data object for its own slice-percent calculation, so reusing that key here would silently override its computed value. */
  legendPercent?: number;
}

export interface AdditionalStat {
  label: string;
  value: string | number;
}

export interface ReportColumn<T> {
  label: string;
  render: (row: T) => React.ReactNode;
}

export interface ReportModalConfig<T = any> {
  title: string;
  totalLabel: string;
  total: number;
  segments: Segment[];
  columns: ReportColumn<T>[];
  rows: T[];
  rowKey: (row: T) => React.Key;
}
