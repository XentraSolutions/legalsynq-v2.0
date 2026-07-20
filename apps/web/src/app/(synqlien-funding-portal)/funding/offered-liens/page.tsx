import Link from "next/link";
import {
  formatFundingCurrency,
  formatFundingDate,
  formatFundingNumber,
  getOfferedLiens,
  getOfferedLiensEmptyStateCopy,
  statusBadgeClass,
  type OfferedLienRow,
  type OfferedLiensQuery,
  type OfferedLiensResult,
} from "@/lib/synqlien-funding-portal";

export const dynamic = "force-dynamic";

interface OfferedLiensPageProps {
  searchParams: Promise<{
    status?: string;
    search?: string;
    page?: string;
    pageSize?: string;
  }>;
}

const STATUS_FILTERS = ["", "Pending", "Accepted", "Declined", "Expired"];

export default async function OfferedLiensPage({
  searchParams,
}: OfferedLiensPageProps) {
  const sp = await searchParams;
  const query: OfferedLiensQuery = {
    status: normalizeFilter(sp.status),
    search: normalizeFilter(sp.search),
    page: parsePositiveInt(sp.page, 1),
    pageSize: parsePositiveInt(sp.pageSize, 10),
  };
  const result = await getOfferedLiens(query);
  const hasFilters = Boolean(query.status || query.search);

  return (
    <div className="mx-auto max-w-[1440px] space-y-5">
      <div className="flex flex-col gap-4 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-orange-600">
            Offer Inbox
          </p>
          <h1 className="mt-2 text-2xl font-semibold tracking-tight text-slate-950 sm:text-3xl">
            Offered Liens
          </h1>
          <p className="mt-1 text-sm text-slate-500">
            Review lien offers returned by the SynqLien buyer API.
          </p>
        </div>
        <SearchForm query={query} />
      </div>

      <section className="rounded-lg border border-slate-200 bg-white shadow-sm">
        <div className="border-b border-slate-100 px-4 py-3 sm:px-5">
          <StatusTabs query={query} />
        </div>
        <OfferedLiensTable result={result} hasFilters={hasFilters} />
        <Pagination result={result} query={query} />
      </section>
    </div>
  );
}

function SearchForm({ query }: { query: OfferedLiensQuery }) {
  return (
    <form action="/funding/offered-liens" className="flex flex-col gap-2 sm:flex-row sm:items-center">
      {query.status ? <input type="hidden" name="status" value={query.status} /> : null}
      <label className="relative block">
        <i className="ri-search-line pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[16px] text-slate-400" />
        <input
          type="search"
          name="search"
          defaultValue={query.search}
          placeholder="Search lien, provider, or seller"
          className="h-10 w-full min-w-0 rounded-md border border-slate-200 bg-white pl-9 pr-3 text-sm text-slate-700 outline-none transition focus:border-orange-400 focus:ring-2 focus:ring-orange-100 sm:w-[320px]"
        />
      </label>
      <button
        type="submit"
        className="inline-flex h-10 items-center justify-center gap-2 rounded-md bg-slate-950 px-4 text-sm font-medium text-white transition-colors hover:bg-slate-800"
      >
        <i className="ri-filter-3-line text-[15px]" />
        Apply
      </button>
    </form>
  );
}

function StatusTabs({ query }: { query: OfferedLiensQuery }) {
  return (
    <div className="flex gap-1 overflow-x-auto">
      {STATUS_FILTERS.map(status => {
        const active = (query.status ?? "") === status;
        const href = buildHref({
          ...query,
          status: status || undefined,
          page: 1,
        });
        return (
          <Link
            key={status || "all"}
            href={href}
            className={`inline-flex h-9 shrink-0 items-center rounded-md px-3 text-sm font-medium transition-colors ${
              active
                ? "bg-slate-950 text-white"
                : "text-slate-600 hover:bg-slate-50 hover:text-slate-950"
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
  hasFilters,
}: {
  result: OfferedLiensResult;
  hasFilters: boolean;
}) {
  const emptyCopy = getOfferedLiensEmptyStateCopy(hasFilters);
  return (
    <div className="overflow-x-auto">
      <table className="min-w-full divide-y divide-slate-100">
        <thead className="bg-slate-50/70">
          <tr className="text-left text-xs font-semibold uppercase tracking-[0.12em] text-slate-400">
            <th className="px-5 py-3">Lien</th>
            <th className="px-5 py-3">Provider</th>
            <th className="px-5 py-3">Seller / Law Firm</th>
            <th className="px-5 py-3">Offered Amount</th>
            <th className="px-5 py-3">Received</th>
            <th className="px-5 py-3">Due</th>
            <th className="px-5 py-3">Status</th>
            <th className="px-5 py-3 text-right">Actions</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100 bg-white">
          {result.rows.length === 0 ? (
            <tr>
              <td colSpan={8} className="px-5 py-14">
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

function OfferedLienTableRow({ row }: { row: OfferedLienRow }) {
  const detailHref = row.allowedActions.includes("view") ? safeHref(row.detailHref) : null;
  const decisionActions = row.allowedActions.filter(action => action !== "view");

  return (
    <tr className="transition-colors hover:bg-slate-50/70">
      <td className="px-5 py-4">
        {detailHref ? (
          <Link href={detailHref} className="text-sm font-semibold text-slate-950 hover:text-orange-700">
            {row.lienNumber}
          </Link>
        ) : (
          <span className="text-sm font-semibold text-slate-950">{row.lienNumber}</span>
        )}
      </td>
      <td className="px-5 py-4 text-sm text-slate-600">{row.providerName}</td>
      <td className="px-5 py-4 text-sm text-slate-600">{row.sellerName}</td>
      <td className="px-5 py-4 text-sm font-medium text-slate-900">
        {formatFundingCurrency(row.offeredAmount)}
      </td>
      <td className="px-5 py-4 text-sm text-slate-600">{formatFundingDate(row.receivedAtUtc)}</td>
      <td className="px-5 py-4 text-sm text-slate-600">{formatFundingDate(row.responseDueAtUtc)}</td>
      <td className="px-5 py-4">
        <span className={`inline-flex rounded-full px-2 py-1 text-xs font-medium ring-1 ${statusBadgeClass(row.status)}`}>
          {row.status}
        </span>
      </td>
      <td className="px-5 py-4">
        <div className="flex justify-end gap-2">
          {detailHref ? (
            <Link
              href={detailHref}
              className="inline-flex h-8 items-center justify-center rounded-md border border-slate-200 px-3 text-xs font-medium text-slate-700 transition-colors hover:border-orange-200 hover:bg-orange-50 hover:text-orange-700"
            >
              View
            </Link>
          ) : null}
          {decisionActions.length > 0 ? (
            <span className="inline-flex h-8 items-center justify-center rounded-md bg-slate-50 px-3 text-xs font-medium text-slate-500">
              {decisionActions.join(", ")}
            </span>
          ) : null}
          {!detailHref && decisionActions.length === 0 ? (
            <span className="text-sm text-slate-300">-</span>
          ) : null}
        </div>
      </td>
    </tr>
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
  const firstItem = result.total === 0 ? 0 : (currentPage - 1) * result.pageSize + 1;
  const lastItem = Math.min(result.total, currentPage * result.pageSize);

  return (
    <div className="flex flex-col gap-3 border-t border-slate-100 px-5 py-4 sm:flex-row sm:items-center sm:justify-between">
      <p className="text-sm text-slate-500">
        Showing {formatFundingNumber(firstItem)}-{formatFundingNumber(lastItem)} of {formatFundingNumber(result.total)}
      </p>
      <div className="flex items-center gap-2">
        <PaginationLink
          href={buildHref({ ...query, page: Math.max(1, currentPage - 1) })}
          disabled={currentPage <= 1}
          label="Previous"
          icon="ri-arrow-left-line"
        />
        <span className="min-w-16 text-center text-sm font-medium text-slate-600">
          {currentPage} / {totalPages}
        </span>
        <PaginationLink
          href={buildHref({ ...query, page: Math.min(totalPages, currentPage + 1) })}
          disabled={currentPage >= totalPages}
          label="Next"
          icon="ri-arrow-right-line"
          iconAfter
        />
      </div>
    </div>
  );
}

function PaginationLink({
  href,
  disabled,
  label,
  icon,
  iconAfter = false,
}: {
  href: string;
  disabled: boolean;
  label: string;
  icon: string;
  iconAfter?: boolean;
}) {
  if (disabled) {
    return (
      <span className="inline-flex h-9 items-center gap-2 rounded-md border border-slate-100 px-3 text-sm font-medium text-slate-300">
        {!iconAfter ? <i className={`${icon} text-[15px]`} /> : null}
        {label}
        {iconAfter ? <i className={`${icon} text-[15px]`} /> : null}
      </span>
    );
  }

  return (
    <Link
      href={href}
      className="inline-flex h-9 items-center gap-2 rounded-md border border-slate-200 px-3 text-sm font-medium text-slate-700 transition-colors hover:border-orange-200 hover:bg-orange-50 hover:text-orange-700"
    >
      {!iconAfter ? <i className={`${icon} text-[15px]`} /> : null}
      {label}
      {iconAfter ? <i className={`${icon} text-[15px]`} /> : null}
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
      <span className="flex h-11 w-11 items-center justify-center rounded-md bg-slate-50 text-slate-400">
        <i className="ri-file-list-3-line text-[20px]" />
      </span>
      <p className="mt-3 text-sm font-semibold text-slate-700">{title}</p>
      <p className="mt-1 max-w-md text-sm text-slate-500">{description}</p>
    </div>
  );
}

function buildHref(query: OfferedLiensQuery): string {
  const params = new URLSearchParams();
  if (query.status) params.set("status", query.status);
  if (query.search) params.set("search", query.search);
  if (query.page && query.page > 1) params.set("page", String(query.page));
  if (query.pageSize && query.pageSize !== 10) params.set("pageSize", String(query.pageSize));

  const encoded = params.toString();
  return encoded ? `/funding/offered-liens?${encoded}` : "/funding/offered-liens";
}

function normalizeFilter(value?: string): string | undefined {
  const trimmed = value?.trim();
  return trimmed ? trimmed : undefined;
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
