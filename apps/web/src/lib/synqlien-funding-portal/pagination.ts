import type { OfferedLiensResult } from './types';

export const OFFERED_LIENS_DEFAULT_PAGE_SIZE = 10;
export const OFFERED_LIENS_PAGE_SIZE_OPTIONS = [10, 25, 50, 100] as const;

export function getOfferedLiensPageSizeOptions(pageSize: number): number[] {
  const options: number[] = [...OFFERED_LIENS_PAGE_SIZE_OPTIONS];
  if (Number.isFinite(pageSize) && pageSize > 0 && !options.includes(pageSize)) {
    options.push(pageSize);
    options.sort((a, b) => a - b);
  }

  return options;
}

export function getOfferedLiensDisplayRange(
  result: Pick<OfferedLiensResult, 'page' | 'pageSize' | 'total'>,
): { firstItem: number; lastItem: number } {
  if (result.total <= 0 || result.pageSize <= 0) {
    return { firstItem: 0, lastItem: 0 };
  }

  const totalPages = Math.max(1, Math.ceil(result.total / result.pageSize));
  const currentPage = Math.min(Math.max(result.page, 1), totalPages);

  return {
    firstItem: (currentPage - 1) * result.pageSize + 1,
    lastItem: currentPage * result.pageSize,
  };
}

export function buildOfferedLiensPageSizeHref({
  pathname,
  searchParams,
  pageSize,
}: {
  pathname: string;
  searchParams: URLSearchParams | string;
  pageSize: number;
}): string {
  const params = new URLSearchParams(searchParams);
  params.delete('page');

  if (pageSize === OFFERED_LIENS_DEFAULT_PAGE_SIZE) {
    params.delete('pageSize');
  } else {
    params.set('pageSize', String(pageSize));
  }

  const encoded = params.toString();
  return encoded ? `${pathname}?${encoded}` : pathname;
}
