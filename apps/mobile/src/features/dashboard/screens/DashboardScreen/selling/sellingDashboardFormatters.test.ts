import {
  buildMonthlyAgingSlices,
  formatSellingAgingPeriod,
  resolveSellingAgingAsOfDate,
  visibleSellingAgingBuckets,
} from './sellingDashboardFormatters';

describe('selling dashboard aging formatters', () => {
  it('uses explicit monthly day periods and removes zero-amount graph buckets', () => {
    const slices = buildMonthlyAgingSlices({
      isSuccess: true,
      message: 'ok',
      asOfDate: '2026-08-25',
      currency: 'USD',
      page: 1,
      pageSize: 10,
      totalCount: 2,
      totalPages: 1,
      summaryTotals: {
        totalLiens: 2,
        days1To30: 100,
        days31To60: 0,
        days61To90: 50,
        days91To120: 0,
        moreThan120: 0,
        totalAmount: 150,
      },
      data: [],
    });

    expect(slices.map(({ label, value }) => ({ label, value }))).toEqual([
      { label: 'Days 1–30', value: 100 },
      { label: 'Days 61–90', value: 50 },
    ]);
  });

  it('handles a partial monthly response without summary totals', () => {
    expect(
      buildMonthlyAgingSlices({
        isSuccess: false,
        message: 'Summary unavailable',
        asOfDate: '2026-08-25',
        currency: 'USD',
        page: 1,
        pageSize: 10,
        totalCount: 0,
        totalPages: 0,
        data: [],
      })
    ).toEqual([]);
  });

  it('removes buyer buckets only when amount and lien count are both zero', () => {
    expect(
      visibleSellingAgingBuckets([
        { bucket: '1-30', amount: 0, lienCount: 0 },
        { bucket: '31-60', amount: 10, lienCount: 0 },
        { bucket: '61-90', amount: 0, lienCount: 1 },
      ])
    ).toHaveLength(2);
  });

  it('normalizes period labels and dashboard dates', () => {
    expect(formatSellingAgingPeriod('0-30')).toBe('Days 1–30');
    expect(formatSellingAgingPeriod('120+')).toBe('Days 121+');
    expect(resolveSellingAgingAsOfDate('08/25/2026')).toBe('2026-08-25');
  });
});
