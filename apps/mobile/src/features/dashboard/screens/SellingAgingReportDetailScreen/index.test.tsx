import { getMonthlyAgingRows, SellingAgingReportDetailScreen } from './index';

describe('SellingAgingReportDetailScreen', () => {
  it('exports the screen entrypoint', () => {
    expect(typeof SellingAgingReportDetailScreen).toBe('function');
  });

  it('returns no rows when a partial report omits data', () => {
    expect(getMonthlyAgingRows({})).toEqual([]);
  });
});
