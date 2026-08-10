export interface SubStat {
  label: string;
  value: string | number;
}

export interface Segment {
  label: string;
  value: number;
  color: string;
  subStats?: SubStat[];
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
