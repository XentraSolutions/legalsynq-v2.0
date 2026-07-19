'use client';

import { useMemo, useState, type ReactNode } from 'react';
import { clsx } from 'clsx';

type OfferStatus = 'Pending' | 'Accepted' | 'Declined';

type OfferedLienRow = {
  lienId: string;
  sellerName: string;
  serviceDate: string;
  billingAmount: string;
  askAmount: string;
  highestBid: string;
  status: OfferStatus;
};

const statusFilters: Array<'All' | OfferStatus> = ['All', 'Pending', 'Accepted', 'Declined'];

const offeredLiens: OfferedLienRow[] = [
  {
    lienId: 'LN-40218',
    sellerName: 'John Doe',
    serviceDate: '01/15/2026',
    billingAmount: '$48,750.00',
    askAmount: '$34,125.00',
    highestBid: '$28,500.00',
    status: 'Pending',
  },
  {
    lienId: 'LN-40219',
    sellerName: 'Sarah Thompson',
    serviceDate: '02/03/2026',
    billingAmount: '$22,300.00',
    askAmount: '$15,610.00',
    highestBid: '$12,840.00',
    status: 'Accepted',
  },
  {
    lienId: 'LN-40220',
    sellerName: 'David Chen',
    serviceDate: '03/22/2026',
    billingAmount: '$115,400.00',
    askAmount: '$80,780.00',
    highestBid: '$67,200.00',
    status: 'Pending',
  },
  {
    lienId: 'LN-40221',
    sellerName: 'Maria Rodriguez',
    serviceDate: '01/28/2026',
    billingAmount: '$36,900.00',
    askAmount: '$25,830.00',
    highestBid: '$21,450.00',
    status: 'Accepted',
  },
  {
    lienId: 'LN-40222',
    sellerName: 'Robert Kim',
    serviceDate: '04/10/2026',
    billingAmount: '$67,250.00',
    askAmount: '$47,075.00',
    highestBid: '$39,500.00',
    status: 'Pending',
  },
  {
    lienId: 'LN-40223',
    sellerName: 'Jennifer Walsh',
    serviceDate: '02/18/2026',
    billingAmount: '$19,800.00',
    askAmount: '$13,860.00',
    highestBid: '$11,200.00',
    status: 'Accepted',
  },
  {
    lienId: 'LN-40224',
    sellerName: 'Michael Patel',
    serviceDate: '05/06/2026',
    billingAmount: '$84,100.00',
    askAmount: '$58,870.00',
    highestBid: '$49,300.00',
    status: 'Pending',
  },
  {
    lienId: 'LN-40225',
    sellerName: 'Laura Bennett',
    serviceDate: '03/14/2026',
    billingAmount: '$29,450.00',
    askAmount: '$20,615.00',
    highestBid: '$17,100.00',
    status: 'Accepted',
  },
  {
    lienId: 'LN-40226',
    sellerName: 'Thomas Nguyen',
    serviceDate: '04/29/2026',
    billingAmount: '$53,600.00',
    askAmount: '$37,520.00',
    highestBid: '$31,250.00',
    status: 'Declined',
  },
  {
    lienId: 'LN-40227',
    sellerName: 'Angela Morrison',
    serviceDate: '06/02/2026',
    billingAmount: '$41,200.00',
    askAmount: '$28,840.00',
    highestBid: '$24,100.00',
    status: 'Pending',
  },
];

const headers = [
  'Lien ID',
  'Seller Name',
  'Initial Service Date',
  'Billing Amount',
  'Ask Amount',
  'Highest Bid',
  'Status',
];

export function SynqLienOfferedLiens() {
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState<'All' | OfferStatus>('All');

  const visibleRows = useMemo(() => {
    const normalizedQuery = query.trim().toLowerCase();
    return offeredLiens.filter((row) => {
      const matchesStatus = status === 'All' || row.status === status;
      if (!matchesStatus) return false;
      if (!normalizedQuery) return true;
      return [
        row.lienId,
        row.sellerName,
        row.serviceDate,
        row.billingAmount,
        row.askAmount,
        row.highestBid,
        row.status,
      ].some((value) => value.toLowerCase().includes(normalizedQuery));
    });
  }, [query, status]);

  return (
    <div className="mx-auto flex w-full max-w-[1440px] flex-col gap-6 text-neutral-950">
      <section>
        <h1 className="text-[32px] font-bold leading-tight tracking-normal">Offered Liens</h1>
        <p className="mt-2 text-sm leading-6 text-neutral-500">
          Track and evaluate lien opportunities submitted directly to your portal.
        </p>
      </section>

      <section className="flex flex-col gap-5">
        <div className="relative">
          <i
            className="ri-search-line absolute left-4 top-1/2 -translate-y-1/2 text-base text-neutral-400"
            aria-hidden
          />
          <input
            type="search"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Search..."
            className="h-10 w-full rounded-[10px] border border-neutral-200 bg-white py-2 pl-11 pr-4 text-sm text-neutral-950 shadow-sm outline-none transition focus:border-[#ee7132] focus:ring-2 focus:ring-[#ee7132]/20"
          />
        </div>

        <div className="grid h-10 grid-cols-4 rounded-xl bg-neutral-50 p-1 text-sm text-neutral-500">
          {statusFilters.map((option) => (
            <button
              key={option}
              type="button"
              onClick={() => setStatus(option)}
              className={clsx(
                'rounded-lg px-3 transition-colors',
                status === option
                  ? 'bg-white font-medium text-neutral-950 shadow-[0_1px_3px_rgba(0,0,0,0.16)]'
                  : 'hover:text-neutral-950',
              )}
            >
              {option}
            </button>
          ))}
        </div>

        <div className="overflow-hidden rounded-2xl border border-neutral-200 bg-white">
          <div className="overflow-x-auto">
            <table className="w-full min-w-[1080px] text-left text-sm">
              <thead className="bg-neutral-100 text-neutral-950">
                <tr>
                  {headers.map((header) => (
                    <th key={header} className="h-10 px-4 text-sm font-medium">
                      <span className="flex items-center justify-between gap-3">
                        {header}
                        <i className="ri-arrow-up-s-line text-neutral-500" aria-hidden />
                      </span>
                    </th>
                  ))}
                  <th className="h-10 w-12 bg-neutral-100 px-4" aria-label="Actions" />
                </tr>
              </thead>
              <tbody>
                {visibleRows.map((row) => (
                  <tr key={row.lienId} className="border-t border-neutral-200">
                    <TableCell>{row.lienId}</TableCell>
                    <TableCell>{row.sellerName}</TableCell>
                    <TableCell>{row.serviceDate}</TableCell>
                    <TableCell>{row.billingAmount}</TableCell>
                    <TableCell>{row.askAmount}</TableCell>
                    <TableCell>{row.highestBid}</TableCell>
                    <TableCell>
                      <StatusBadge status={row.status} />
                    </TableCell>
                    <td className="h-[53px] w-12 px-4 text-center">
                      <button
                        type="button"
                        className="inline-flex h-7 w-7 items-center justify-center rounded-md text-neutral-500 hover:bg-neutral-100 hover:text-neutral-950"
                        aria-label={`Actions for ${row.lienId}`}
                      >
                        <i className="ri-more-2-fill text-lg" aria-hidden />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="flex flex-col gap-4 px-6 py-4 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-center gap-3 text-sm text-neutral-500">
              <span>Showing</span>
              <button
                type="button"
                className="inline-flex h-9 items-center gap-2 rounded-lg border border-neutral-200 bg-white px-3 text-neutral-950 shadow-sm"
              >
                1-10
                <i className="ri-arrow-down-s-line text-neutral-500" aria-hidden />
              </button>
              <span>of 200 entries.</span>
            </div>

            <div className="flex items-center gap-2">
              <PageButton disabled icon="ri-skip-left-line" label="First page" />
              <PageButton disabled icon="ri-arrow-left-s-line" label="Previous page" />
              <button
                type="button"
                aria-label="Page 1"
                className="inline-flex h-9 w-9 items-center justify-center rounded-lg border border-neutral-200 bg-white text-sm font-medium text-neutral-950 shadow-sm"
              >
                1
              </button>
              <PageNumber page="2" />
              <PageNumber page="3" />
              <PageButton icon="ri-arrow-right-s-line" label="Next page" />
              <PageButton icon="ri-skip-right-line" label="Last page" />
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}

function TableCell({ children }: { children: ReactNode }) {
  return <td className="h-[53px] px-4 text-sm text-neutral-950">{children}</td>;
}

function StatusBadge({ status }: { status: OfferStatus }) {
  const styles: Record<OfferStatus, string> = {
    Pending: 'bg-[#eab308]/15 text-[#a16207]',
    Accepted: 'bg-[#17c964]/15 text-[#15803d]',
    Declined: 'bg-[#ef4444]/15 text-[#b91c1c]',
  };

  return (
    <span className={clsx('inline-flex h-7 items-center rounded-full px-3 text-sm font-medium', styles[status])}>
      {status}
    </span>
  );
}

function PageNumber({ page }: { page: string }) {
  return (
    <button
      type="button"
      aria-label={`Page ${page}`}
      className="inline-flex h-9 w-9 items-center justify-center rounded-lg text-sm font-medium text-neutral-950 hover:bg-neutral-100"
    >
      {page}
    </button>
  );
}

function PageButton({
  icon,
  label,
  disabled = false,
}: {
  icon: string;
  label: string;
  disabled?: boolean;
}) {
  return (
    <button
      type="button"
      aria-label={label}
      disabled={disabled}
      className="inline-flex h-9 w-9 items-center justify-center rounded-[10px] border border-neutral-200 bg-white text-neutral-700 shadow-sm transition hover:bg-neutral-50 disabled:cursor-default disabled:opacity-50 disabled:hover:bg-white"
    >
      <i className={`${icon} text-base`} aria-hidden />
    </button>
  );
}
