import type { DonutSlice, StatCardData } from '../dashboardShared';
import { BLUE, GREEN, ORANGE, RED, YELLOW } from '../dashboardShared';

export interface SellerRisk {
  name: string;
  balance: string;
  share: string;
  risk: 'High' | 'Medium';
  rows?: Array<{ label: string; value: string }>;
}

export const SELLING_STATS: StatCardData[] = [
  { label: 'Total Lien Revenue', value: '$4,782,350.72', trend: '8.9%', trendTone: 'positive' },
  { label: 'Total Outstanding', value: '$3,842,196.18', trend: '6.4%', trendTone: 'positive' },
  { label: 'Past Amount Due', value: '$1,287,542.63', trend: '8.9%', trendTone: 'positive' },
  { label: 'Payments', value: '$635,251.44', trend: '5.0%', trendTone: 'negative' },
];

export const SELLING_AGING: DonutSlice[] = [
  { label: 'Days 1–30', value: 32.7, amount: '$1,125,842.50', percent: '(32.7%)', color: BLUE },
  { label: 'Days 31–60', value: 21.2, amount: '$987,651.22', percent: '(21.2%)', color: ORANGE },
  { label: 'Days 61–90', value: 19.2, amount: '$987,651.22', percent: '(19.2%)', color: GREEN },
  { label: 'Days 91–120', value: 11.2, amount: '$754,221.17', percent: '(11.2%)', color: YELLOW },
  { label: 'Days 121+', value: 10.8, amount: '$411,601.15', percent: '(10.8%)', color: RED },
];

export const SELLING_STATUS: DonutSlice[] = [
  { label: 'Active', value: 67.5, amount: '842', percent: '(67.5%)', color: BLUE },
  { label: 'Settled', value: 17.1, amount: '214', percent: '(17.1%)', color: ORANGE },
  { label: 'In Reduction', value: 9, amount: '112', percent: '(9.0%)', color: GREEN },
  { label: 'Paid', value: 4.5, amount: '56', percent: '(4.5%)', color: YELLOW },
  { label: 'Other / Closed', value: 1.9, amount: '24', percent: '(1.9%)', color: RED },
];

export const SELLING_TOP_BALANCES = [
  {
    name: 'Apex Mutual',
    subtitle: 'Active Accounts: 182',
    balance: '$1,125,842.50',
    share: '23.5%',
    mark: 'pie',
  },
  {
    name: 'Nova Care',
    subtitle: 'Active Accounts: 132',
    balance: '$687,421.88',
    share: '14.4%',
    mark: 'cube',
  },
  {
    name: 'Summit Ins.',
    subtitle: 'Active Accounts: 98',
    balance: '$456,218.33',
    share: '9.5%',
    mark: 'wave',
  },
  {
    name: 'Beacon Life',
    subtitle: 'Active Accounts: 76',
    balance: '$321,775.19',
    share: '6.7%',
    mark: 'bars',
  },
  {
    name: 'Vanguard',
    subtitle: 'Active Accounts: 64',
    balance: '$289,114.22',
    share: '6.0%',
    mark: 'v',
  },
];

export const SELLING_SELLERS: SellerRisk[] = [
  {
    name: 'Apex Mutual',
    balance: '$1,125,842.50',
    share: '17.1%',
    risk: 'High',
    rows: [
      { label: '0 - 30 Days:', value: '$412,512.00' },
      { label: '31 - 60 Days:', value: '$298,451.23' },
      { label: '61 - 90 Days:', value: '$221,114.55' },
      { label: '91 - 120 Days:', value: '$112,662.11' },
      { label: '120+ Days:', value: '$81,102.30' },
    ],
  },
  { name: 'Nova Care', balance: '$687,421.88', share: '29.1%', risk: 'High' },
  { name: 'Summit Ins.', balance: '$456,218.33', share: '22.8%', risk: 'Medium' },
  { name: 'Beacon Life', balance: '$321,775.19', share: '29.7%', risk: 'High' },
  { name: 'Vanguard', balance: '$289,114.22', share: '40.3%', risk: 'High' },
];

export const LINE_POINTS = [2.4, 3.7, 2.6, 1.0, 2.5, 2.6];

export function buildLineChart(width: number, height: number, values: number[]) {
  const min = 0;
  const max = 4;
  const top = 8;
  const bottom = height - 12;
  const xStep = width / (values.length - 1);
  const points = values.map((value, index) => ({
    x: index * xStep,
    y: top + ((max - value) / (max - min)) * (bottom - top),
  }));
  const pointsString = points.map((point) => `${point.x},${point.y}`).join(' ');
  const first = points[0];
  const last = points[points.length - 1];
  const linePath = points
    .map((point, index) => `${index === 0 ? 'M' : 'L'} ${point.x},${point.y}`)
    .join(' ');
  const areaPath = `${linePath} L ${last.x},${bottom} L ${first.x},${bottom} Z`;

  return { areaPath, points, pointsString };
}
