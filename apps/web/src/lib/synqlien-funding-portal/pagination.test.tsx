import { describe, expect, test } from 'vitest';
import {
  buildOfferedLiensPageSizeHref,
  getOfferedLiensDisplayRange,
  getOfferedLiensPageSizeOptions,
} from './pagination';

describe('SynqLien funding portal pagination', () => {
  test('defaults the first page working set to the full page size', () => {
    expect(getOfferedLiensDisplayRange({ page: 1, pageSize: 10, total: 8 }))
      .toEqual({ firstItem: 1, lastItem: 10 });
  });

  test('keeps empty results at a zero range', () => {
    expect(getOfferedLiensDisplayRange({ page: 1, pageSize: 10, total: 0 }))
      .toEqual({ firstItem: 0, lastItem: 0 });
  });

  test('includes the 1-100 working set option', () => {
    expect(getOfferedLiensPageSizeOptions(10)).toContain(100);
  });

  test('preserves filters and resets the current page when page size changes', () => {
    expect(buildOfferedLiensPageSizeHref({
      pathname: '/funding/offered-liens',
      searchParams: 'status=Pending&page=3&sort=status&direction=desc',
      pageSize: 25,
    })).toBe('/funding/offered-liens?status=Pending&sort=status&direction=desc&pageSize=25');
  });

  test('removes pageSize from the URL when returning to the default working set', () => {
    expect(buildOfferedLiensPageSizeHref({
      pathname: '/funding/offered-liens',
      searchParams: 'search=RL&page=2&pageSize=25',
      pageSize: 10,
    })).toBe('/funding/offered-liens?search=RL');
  });
});
