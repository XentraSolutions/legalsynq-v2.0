import Link from "next/link";
import { OfferedLiensPageSizeSelect } from "@/components/synqlien-funding-portal/offered-liens-page-size-select";
import { OfferedLienRowActions } from "@/components/synqlien-funding-portal/offered-lien-row-actions";
import {
  OFFERED_LIENS_DEFAULT_PAGE_SIZE,
  buildOfferedLiensHref,
  formatFundingCurrency,
  formatFundingDate,
  formatFundingNumber,
  getOfferedLiens,
  getOfferedLiensDisplayRange,
  getOfferedLiensEmptyStateCopy,
  statusBadgeClass,
  type OfferedLienRow,
  type OfferedLiensQuery,
  type OfferedLiensResult,
  type OfferedLiensSortDirection,
  type OfferedLiensSortKey,
} from "@/lib/synqlien-funding-portal";

export const dynamic = "force-dynamic";

interface OfferedLiensPageProps {
  searchParams: Promise<{
    status?: string;
    search?: string;
    page?: string;
    pageSize?: string;
    sort?: string;
    direction?: string;
  }>;
}

const STATUS_FILTERS = ["", "Pending", "Accepted", "Declined"];
const DEFAULT_SORT_DIRECTION: OfferedLiensSortDirection = "asc";

export default async function OfferedLiensPage({
  searchParams,
}: OfferedLiensPageProps) {
  const sp = await searchParams;
  const sort = normalizeSort(sp.sort);
  const query: OfferedLiensQuery = {
    status: normalizeFilter(sp.status),
    search: normalizeFilter(sp.search),
    page: parsePositiveInt(sp.page, 1),
    pageSize: parsePositiveInt(sp.pageSize, OFFERED_LIENS_DEFAULT_PAGE_SIZE),
    sort,
    direction: sort ? normalizeDirection(sp.direction) : undefined,
  };
  const result = await getOfferedLiens(query);
  const hasFilters = Boolean(query.status || query.search);

  return (
    <div className="w-full space-y-4">
      <div>
        <h1 className="text-[28px] font-semibold leading-9 tracking-normal text-[#0a0a0a]">
          Offered Liens
        </h1>
        <p className="mt-1 text-[14px] font-normal leading-[1.6] text-[#737373]">
          Track and evaluate lien opportunities submitted directly to your portal.
        </p>
      </div>

      <SearchForm query={query} />
      <StatusTabs query={query} />

      <section className="overflow-hidden rounded-[16px] border border-[#e5e5e5] bg-white shadow-[0_1px_1.5px_rgba(0,0,0,0.08)]">
        <OfferedLiensTable result={result} query={query} hasFilters={hasFilters} />
        <Pagination result={result} query={query} />
      </section>
    </div>
  );
}

function SearchForm({ query }: { query: OfferedLiensQuery }) {
  return (
    <form action="/funding/offered-liens" className="w-full">
      {query.status ? <input type="hidden" name="status" value={query.status} /> : null}
      {query.pageSize && query.pageSize !== OFFERED_LIENS_DEFAULT_PAGE_SIZE ? (
        <input type="hidden" name="pageSize" value={query.pageSize} />
      ) : null}
      {query.sort ? <input type="hidden" name="sort" value={query.sort} /> : null}
      {query.sort && query.direction ? <input type="hidden" name="direction" value={query.direction} /> : null}
      <label className="relative block w-full">
        <i className="ri-search-line pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[16px] text-[#737373]" />
        <input
          type="search"
          name="search"
          defaultValue={query.search}
          placeholder="Search..."
          className="h-9 w-full rounded-[8px] border border-[#e5e5e5] bg-white pl-9 pr-3 text-[14px] font-normal leading-[1.6] text-[#0a0a0a] shadow-[0_1px_1px_rgba(0,0,0,0.04)] outline-none transition focus:border-[#f4a076] focus:ring-2 focus:ring-[#fdf1eb]"
        />
      </label>
    </form>
  );
}

function StatusTabs({ query }: { query: OfferedLiensQuery }) {
  return (
    <div className="grid h-9 grid-cols-4 overflow-hidden rounded-[8px] bg-[#f5f5f5] p-px">
      {STATUS_FILTERS.map(status => {
        const active = (query.status ?? "") === status;
        const href = buildOfferedLiensHref({
          ...query,
          status: status || undefined,
          page: 1,
        });
        return (
          <Link
            key={status || "all"}
            href={href}
            className={`flex items-center justify-center rounded-[7px] text-[12px] font-medium leading-[1.6] transition-colors ${
              active
                ? "border border-[#e5e5e5] bg-white text-[#0a0a0a] shadow-[0_1px_1px_rgba(0,0,0,0.08)]"
                : "text-[#737373] hover:bg-white/70 hover:text-[#0a0a0a]"
            }`}
          >
            {status || "All"}
          </Link>
        );
      })}
    </div>
  );
}

function OfferedLiensTable({
  result,
  query,
  hasFilters,
}: {
  result: OfferedLiensResult;
  query: OfferedLiensQuery;
  hasFilters: boolean;
}) {
  const emptyCopy = getOfferedLiensEmptyStateCopy(hasFilters);
  return (
    <div className="overflow-x-auto">
      <table className="min-w-[980px] w-full border-collapse">
        <thead className="bg-[#f5f5f5]">
          <tr>
            <SortableHeaderCell query={query} sortKey="lienNumber">Lien ID</SortableHeaderCell>
            <SortableHeaderCell query={query} sortKey="sellerName">Seller Name</SortableHeaderCell>
            <SortableHeaderCell query={query} sortKey="initialServiceDate">Initial Service Date</SortableHeaderCell>
            <SortableHeaderCell query={query} sortKey="billingAmount">Billing Amount</SortableHeaderCell>
            <SortableHeaderCell query={query} sortKey="askAmount">Ask Amount</SortableHeaderCell>
            <SortableHeaderCell query={query} sortKey="status">Status</SortableHeaderCell>
            <th aria-label="Actions" className="h-10 w-12 px-4" />
          </tr>
        </thead>
        <tbody>
          {result.rows.length === 0 ? (
            <tr>
              <td colSpan={7} className="px-5 py-14">
                <EmptyState
                  title={emptyCopy.title}
                  description={emptyCopy.description}
                />
              </td>
            </tr>
          ) : result.rows.map(row => (
            <OfferedLienTableRow key={row.id} row={row} />
          ))}
        </tbody>
      </table>
    </div>
  );
}

function SortableHeaderCell({
  children,
  query,
  sortKey,
}: {
  children: React.ReactNode;
  query: OfferedLiensQuery;
  sortKey: OfferedLiensSortKey;
}) {
  const active = query.sort === sortKey;
  const direction = active ? query.direction ?? DEFAULT_SORT_DIRECTION : DEFAULT_SORT_DIRECTION;
  const nextDirection: OfferedLiensSortDirection = active && direction === "asc" ? "desc" : "asc";
  const icon = active
    ? direction === "desc"
      ? "ri-arrow-down-s-line"
      : "ri-arrow-up-s-line"
    : "ri-arrow-up-down-line";
  const href = buildOfferedLiensHref({
    ...query,
    sort: sortKey,
    direction: nextDirection,
    page: 1,
  });

  return (
    <th
      className="h-10 px-4 text-left text-[14px] font-medium leading-[1.6] text-[#0a0a0a]"
      aria-sort={active ? (direction === "desc" ? "descending" : "ascending") : undefined}
    >
      <Link
        href={href}
        className="flex min-w-0 items-center gap-2 transition-colors hover:text-[#ee7132]"
      >
        <span className="truncate">{children}</span>
        <i className={`${icon} shrink-0 text-[14px] text-[#525252]`} />
      </Link>
    </th>
  );
}

function OfferedLienTableRow({ row }: { row: OfferedLienRow }) {
  const allowedActions = Array.isArray(row.allowedActions) ? row.allowedActions : [];
  const detailHref = allowedActions.includes("view") ? safeHref(row.detailHref) : null;
  const initialServiceDate = row.initialServiceDate ?? row.serviceDate ?? null;
  const billingAmount = row.billingAmount ?? row.originalAmount ?? null;
  const askAmount = row.askAmount ?? row.offeredAmount;

  return (
    <tr className="border-b border-[#e5e5e5] last:border-b-0">
      <BodyCell>
        {detailHref ? (
          <Link href={detailHref} className="transition-colors hover:text-[#ee7132]">
            {row.lienNumber}
          </Link>
        ) : (
          row.lienNumber
        )}
      </BodyCell>
      <BodyCell>{row.sellerName}</BodyCell>
      <BodyCell>{formatOptionalDate(initialServiceDate)}</BodyCell>
      <BodyCell>{formatOptionalCurrency(billingAmount)}</BodyCell>
      <BodyCell>{formatOptionalCurrency(askAmount)}</BodyCell>
      <td className="h-[53px] px-4 text-[14px] font-normal leading-[1.6] text-[#0a0a0a]">
        <span className={`inline-flex rounded-full px-3 py-1 text-[14px] font-medium leading-[1.6] ring-1 ${statusBadgeClass(row.status)}`}>
          {row.status}
        </span>
      </td>
      <td className="h-[53px] w-12 px-4 text-center">
        <OfferedLienRowActions
          id={row.id}
          lienNumber={row.lienNumber}
          detailHref={detailHref}
          sellerName={row.sellerName}
          askAmount={askAmount}
          allowedActions={allowedActions}
        />
      </td>
    </tr>
  );
}

function BodyCell({ children }: { children: React.ReactNode }) {
  return (
    <td className="h-[53px] px-4 text-[14px] font-normal leading-[1.6] text-[#0a0a0a]">
      <div className="truncate">{children}</div>
    </td>
  );
}

function Pagination({
  result,
  query,
}: {
  result: OfferedLiensResult;
  query: OfferedLiensQuery;
}) {
  const totalPages = result.pageSize > 0 ? Math.max(1, Math.ceil(result.total / result.pageSize)) : 1;
  const currentPage = Math.min(Math.max(result.page, 1), totalPages);
  const { firstItem, lastItem } = getOfferedLiensDisplayRange(result);
  const pageNumbers = buildPageNumbers(currentPage, totalPages);

  return (
    <div className="flex flex-col gap-4 px-6 pb-6 pt-4 sm:flex-row sm:items-center sm:justify-between">
      <div className="flex flex-wrap items-center gap-3 text-[14px] font-normal leading-5 text-[#737373]">
        <span>Showing</span>
        <OfferedLiensPageSizeSelect
          pageSize={result.pageSize}
          firstItem={firstItem}
          lastItem={lastItem}
        />
        <span>of {formatFundingNumber(result.total)} entries.</span>
      </div>

      <div className="flex items-center gap-2">
        <PaginationIcon
          href={buildOfferedLiensHref({ ...query, page: 1 })}
          disabled={currentPage <= 1}
          icon="ri-skip-left-line"
          label="First page"
        />
        <PaginationIcon
          href={buildOfferedLiensHref({ ...query, page: Math.max(1, currentPage - 1) })}
          disabled={currentPage <= 1}
          icon="ri-arrow-left-s-line"
          label="Previous page"
        />
        {pageNumbers.map(page => (
          <PaginationNumber
            key={page}
            href={buildOfferedLiensHref({ ...query, page })}
            active={page === currentPage}
            page={page}
          />
        ))}
        <PaginationIcon
          href={buildOfferedLiensHref({ ...query, page: Math.min(totalPages, currentPage + 1) })}
          disabled={currentPage >= totalPages}
          icon="ri-arrow-right-s-line"
          label="Next page"
        />
        <PaginationIcon
          href={buildOfferedLiensHref({ ...query, page: totalPages })}
          disabled={currentPage >= totalPages}
          icon="ri-skip-right-line"
          label="Last page"
        />
      </div>
    </div>
  );
}

function PaginationIcon({
  href,
  disabled,
  icon,
  label,
}: {
  href: string;
  disabled: boolean;
  icon: string;
  label: string;
}) {
  const className = "inline-flex h-9 w-9 items-center justify-center rounded-[10px] border border-[#e5e5e5] bg-white text-[#0a0a0a] shadow-[0_1px_2px_rgba(0,0,0,0.08)]";

  if (disabled) {
    return (
      <span aria-label={label} aria-disabled="true" className={`${className} opacity-50`}>
        <i className={`${icon} text-[16px]`} />
      </span>
    );
  }

  return (
    <Link href={href} aria-label={label} className={`${className} transition-colors hover:border-[#f4a076] hover:text-[#ee7132]`}>
      <i className={`${icon} text-[16px]`} />
    </Link>
  );
}

function PaginationNumber({
  href,
  active,
  page,
}: {
  href: string;
  active: boolean;
  page: number;
}) {
  const className =
    "inline-flex h-9 w-9 items-center justify-center rounded-[8px] text-[14px] font-medium leading-5 text-[#0a0a0a]";

  if (active) {
    return (
      <span className={`${className} border border-[#e5e5e5] bg-white shadow-[0_1px_1px_rgba(0,0,0,0.08)]`}>
        {page}
      </span>
    );
  }

  return (
    <Link href={href} className={`${className} transition-colors hover:bg-[#f5f5f5]`}>
      {page}
    </Link>
  );
}

function EmptyState({
  title,
  description,
}: {
  title: string;
  description: string;
}) {
  return (
    <div className="flex flex-col items-center justify-center text-center">
      <span className="flex h-11 w-11 items-center justify-center rounded-[8px] bg-[#f5f5f5] text-[#737373]">
        <i className="ri-file-list-3-line text-[20px]" />
      </span>
      <p className="mt-3 text-[14px] font-semibold leading-[1.6] text-[#525252]">{title}</p>
      <p className="mt-1 max-w-md text-[14px] font-normal leading-[1.6] text-[#737373]">{description}</p>
    </div>
  );
}

function buildPageNumbers(currentPage: number, totalPages: number): number[] {
  const count = Math.min(3, totalPages);
  const start = Math.min(Math.max(1, currentPage - 1), Math.max(1, totalPages - count + 1));
  return Array.from({ length: count }, (_, index) => start + index);
}

function normalizeFilter(value?: string): string | undefined {
  const trimmed = value?.trim();
  return trimmed ? trimmed : undefined;
}

function normalizeSort(value?: string): OfferedLiensSortKey | undefined {
  switch (value) {
    case "lienNumber":
    case "sellerName":
    case "initialServiceDate":
    case "billingAmount":
    case "askAmount":
    case "status":
      return value;
    default:
      return undefined;
  }
}

function normalizeDirection(value?: string): OfferedLiensSortDirection {
  return value === "desc" ? "desc" : DEFAULT_SORT_DIRECTION;
}

function parsePositiveInt(value: string | undefined, fallback: number): number {
  if (!value) return fallback;
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

function safeHref(value?: string | null): string | null {
  if (!value || !value.startsWith("/") || value.startsWith("//")) return null;
  return value;
}

function formatOptionalCurrency(value?: number | null): string {
  return value === undefined || value === null || !Number.isFinite(value)
    ? "-"
    : formatFundingCurrency(value);
}

function formatOptionalDate(value?: string | null): string {
  return value ? formatFundingDate(value) : "-";
}
