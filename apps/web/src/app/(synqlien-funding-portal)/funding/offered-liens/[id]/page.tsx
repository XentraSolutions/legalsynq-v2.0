import Link from "next/link";
import { notFound } from "next/navigation";
import { OfferedLienDetailActions } from "@/components/synqlien-funding-portal/offered-lien-detail-actions";
import { OfferedLienMessages } from "@/components/synqlien-funding-portal/offered-lien-messages";
import {
  formatFundingDate,
  getOfferedLienDetail,
  statusBadgeClass,
  type OfferedLienActivityItem,
  type OfferedLienDetail,
  type OfferedLienDocument,
} from "@/lib/synqlien-funding-portal";

export const dynamic = "force-dynamic";

type OfferedLienDetailTab = "overview" | "documents" | "messages";

interface OfferedLienDetailPageProps {
  params: Promise<{ id: string }>;
  searchParams: Promise<{ tab?: string }>;
}

const TABS: Array<{ key: OfferedLienDetailTab; label: string }> = [
  { key: "overview", label: "Overview" },
  { key: "documents", label: "Documents" },
  { key: "messages", label: "Messages" },
];

export default async function OfferedLienDetailPage({
  params,
  searchParams,
}: OfferedLienDetailPageProps) {
  const [{ id }, sp] = await Promise.all([params, searchParams]);
  const detail = await getOfferedLienDetail(id);
  if (!detail) notFound();

  const activeTab = normalizeTab(sp.tab);

  return (
    <div className="w-full space-y-4">
      <PageHeader detail={detail} />
      <DetailTabs id={detail.id} activeTab={activeTab} />

      {activeTab === "overview" ? <OverviewTab detail={detail} /> : null}
      {activeTab === "documents" ? <DocumentsTab documents={detail.documents} /> : null}
      {activeTab === "messages" ? <MessagesTab detail={detail} /> : null}
    </div>
  );
}

function PageHeader({ detail }: { detail: OfferedLienDetail }) {
  return (
    <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
      <div className="flex min-w-0 gap-4">
        <Link
          href="/funding/offered-liens"
          aria-label="Back to offered liens"
          className="mt-1 flex h-9 w-9 shrink-0 items-center justify-center rounded-full border border-[#e5e5e5] bg-white text-[#0a0a0a] shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors hover:border-[#f4a076] hover:text-[#ee7132]"
        >
          <i className="ri-arrow-left-line text-[16px]" />
        </Link>
        <div className="min-w-0">
          <h1 className="truncate text-[32px] font-bold leading-10 tracking-normal text-[#0a0a0a]">
            {detail.title || detail.lienNumber}
          </h1>
          <p className="mt-1 truncate text-[16px] font-normal leading-[1.6] text-[#737373]">
            {detail.subtitle || detail.lienNumber}
          </p>
        </div>
      </div>

      <OfferedLienDetailActions
        id={detail.id}
        status={detail.status}
        allowedActions={detail.allowedActions}
      />
    </div>
  );
}

function DetailTabs({
  id,
  activeTab,
}: {
  id: string;
  activeTab: OfferedLienDetailTab;
}) {
  return (
    <div className="grid h-9 grid-cols-3 overflow-hidden rounded-[10px] bg-[#fafafa] p-1">
      {TABS.map(tab => {
        const active = tab.key === activeTab;
        const href = tab.key === "overview"
          ? `/funding/offered-liens/${id}`
          : `/funding/offered-liens/${id}?tab=${tab.key}`;
        return (
          <Link
            key={tab.key}
            href={href}
            className={`flex items-center justify-center rounded-[8px] text-[14px] font-medium leading-[1.6] transition-colors ${
              active
                ? "border border-[#e5e5e5] bg-white text-[#0a0a0a] shadow-[0_1px_1.5px_rgba(0,0,0,0.1)]"
                : "text-[#737373] hover:bg-white/70 hover:text-[#0a0a0a]"
            }`}
          >
            {tab.label}
          </Link>
        );
      })}
    </div>
  );
}

function OverviewTab({ detail }: { detail: OfferedLienDetail }) {
  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(360px,0.85fr)]">
      <section className="rounded-[16px] border border-[#e5e5e5] bg-white px-6 pb-2 pt-6 shadow-[0_1px_1.5px_rgba(0,0,0,0.1)]">
        <h2 className="text-[18px] font-bold leading-[1.6] text-[#0a0a0a]">
          Seller &amp; Lien Information
        </h2>
        <div className="mt-4 grid gap-x-10 sm:grid-cols-2">
          <DetailField label="Seller Name" value={detail.seller.name || "-"} />
          <DetailField label="Seller Company" value={detail.seller.company || "-"} />
          <div className="pb-4">
            <p className="text-[16px] font-normal leading-[1.6] text-[#737373]">Status</p>
            <span className={`mt-2 inline-flex rounded-full px-3 py-1 text-[14px] font-medium leading-[1.6] ring-1 ${statusBadgeClass(detail.status)}`}>
              {detail.status}
            </span>
          </div>
          <DetailField label="Submitted Date" value={formatDateTimeParts(detail.submittedAtUtc)} />
          <DetailField label="Initial Service Date" value={formatOptionalDate(detail.initialServiceDate)} />
          <DetailField label="End Service Date" value={formatOptionalDate(detail.endServiceDate)} />
          <div className="pb-4 sm:col-span-2">
            <p className="text-[16px] font-normal leading-[1.6] text-[#737373]">Lien Notes</p>
            <p className="mt-2 max-w-[680px] whitespace-pre-wrap text-[16px] font-medium leading-[1.6] text-[#0a0a0a]">
              {detail.notes || "-"}
            </p>
          </div>
        </div>
      </section>

      <section className="min-h-[266px] rounded-[16px] border border-[#e5e5e5] bg-white p-6 shadow-[0_1px_1.5px_rgba(0,0,0,0.1)]">
        {detail.activity.length > 0 ? (
          <ActivityTimeline activity={detail.activity} />
        ) : (
          <CenteredEmptyState
            icon="ri-clipboard-line"
            title="No Activity Yet"
            description="There is no activity recorded for this lien yet. Any updates or actions related to this lien will appear here."
          />
        )}
      </section>
    </div>
  );
}

function DocumentsTab({ documents }: { documents: OfferedLienDocument[] }) {
  const documentRows = chunkDocuments(documents, 2);

  return (
    <section className="rounded-[16px] border border-[#e5e5e5] bg-white px-6 py-5 shadow-[0_1px_1.5px_rgba(0,0,0,0.1)]">
      {documents.length > 0 ? (
        <div aria-label="Attached documents" className="divide-y divide-[#e5e5e5]">
          {documentRows.map(row => (
            <div
              key={row.map(document => document.id).join("-")}
              className="grid grid-cols-1 divide-y divide-[#e5e5e5] md:grid-cols-2 md:divide-x md:divide-y-0"
            >
              {row.map(document => (
                <DocumentRow key={document.id} document={document} />
              ))}
            </div>
          ))}
        </div>
      ) : (
        <CenteredEmptyState
          icon="ri-file-list-3-line"
          title="No Uploaded Document"
          description="There are no documents uploaded for this lien yet. Any uploaded documents will be displayed here."
        />
      )}
    </section>
  );
}

function MessagesTab({ detail }: { detail: OfferedLienDetail }) {
  return <OfferedLienMessages id={detail.id} initialMessages={detail.messages} />;
}

function DetailField({
  label,
  value,
}: {
  label: string;
  value: React.ReactNode;
}) {
  return (
    <div className="pb-4">
      <p className="text-[16px] font-normal leading-[1.6] text-[#737373]">{label}</p>
      <p className="mt-2 text-[16px] font-medium leading-[1.6] text-[#0a0a0a]">{value}</p>
    </div>
  );
}

function ActivityTimeline({ activity }: { activity: OfferedLienActivityItem[] }) {
  return (
    <div>
      <h2 className="text-[18px] font-bold leading-[1.6] text-[#0a0a0a]">Activity</h2>
      <ol className="relative mt-5 space-y-6">
        {activity.length > 1 ? (
          <span
            aria-hidden="true"
            className="absolute left-[9px] top-5 h-[calc(100%-40px)] w-px bg-[#e5e5e5]"
          />
        ) : null}
        {activity.map(item => (
          <li key={item.id} className="relative flex gap-2">
            <span className="mt-[1px] flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-[#ee7132] shadow-[0_1px_1.5px_rgba(0,0,0,0.1)]">
              <span className="h-2.5 w-2.5 rounded-full bg-white" />
            </span>
            <div className="min-w-0">
              <p className="text-[14px] font-medium leading-[1.6] text-[#0a0a0a]">{formatActivityLabel(item.label)}</p>
              <p className="text-[14px] font-normal leading-[1.6] text-[#737373]">{formatActivityDateTime(item.occurredAtUtc)}</p>
              {item.notes ? (
                <p className="mt-1 whitespace-pre-wrap text-[14px] font-normal leading-[1.6] text-[#525252]">
                  {item.notes}
                </p>
              ) : null}
            </div>
          </li>
        ))}
      </ol>
    </div>
  );
}

function formatActivityLabel(value: string): string {
  return value.replace(/\s*->\s*/g, " → ");
}

function DocumentRow({
  document,
}: {
  document: OfferedLienDocument;
}) {
  const detail = [document.category, document.sizeOrType].filter(Boolean).join("  •  ");
  const viewUrl = safeHref(document.viewUrl ?? document.url);
  const downloadUrl = safeHref(document.downloadUrl);

  return (
    <div className="flex min-h-[84px] min-w-0 items-center gap-4 py-4 md:px-5 md:first:pl-0 md:last:pr-0 xl:px-6">
      <span className="flex h-14 w-14 shrink-0 items-center justify-center rounded-[12px] bg-[#f5f5f5] text-[#0a0a0a]">
        <i className="ri-file-text-line text-[28px]" />
      </span>
      <div className="min-w-0 flex-1">
        <div className="grid min-w-0 gap-1 xl:grid-cols-[minmax(0,1fr)_auto] xl:items-start">
          <p className="min-w-0 break-words text-[16px] font-semibold leading-5 text-[#0a0a0a]">
            {document.fileName}
          </p>
          <p className="shrink-0 whitespace-nowrap text-[14px] font-normal leading-[1.6] text-[#737373] xl:pl-4">
            {formatDateTimeParts(document.createdAtUtc)}
          </p>
        </div>
        <p className="mt-1 text-[16px] font-normal leading-[1.6] text-[#737373]">
          {detail || "Document"}
        </p>
      </div>
      <div className="flex shrink-0 items-center gap-2 self-start pt-1">
        <DocumentActionLink
          href={viewUrl}
          label={`View ${document.fileName}`}
          icon="ri-eye-line"
          openInNewTab
        />
        <DocumentActionLink
          href={downloadUrl}
          label={`Download ${document.fileName}`}
          icon="ri-download-line"
        />
      </div>
    </div>
  );
}

function chunkDocuments(
  documents: OfferedLienDocument[],
  size: number,
): OfferedLienDocument[][] {
  const rows: OfferedLienDocument[][] = [];
  for (let index = 0; index < documents.length; index += size) {
    rows.push(documents.slice(index, index + size));
  }
  return rows;
}

function DocumentActionLink({
  href,
  label,
  icon,
  openInNewTab = false,
}: {
  href?: string | null;
  label: string;
  icon: string;
  openInNewTab?: boolean;
}) {
  const url = safeHref(href);
  const className =
    "flex h-9 w-9 shrink-0 items-center justify-center rounded-[10px] border border-[#e5e5e5] bg-white text-[#ee7132] shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132]";

  if (!url) {
    return (
      <span
        aria-label={label}
        aria-disabled="true"
        className={`${className} cursor-not-allowed opacity-50`}
        role="button"
      >
        <i className={`${icon} text-[16px]`} />
      </span>
    );
  }

  const opensInNewTab = openInNewTab || isExternalHref(url);

  return (
    <a
      href={url}
      target={opensInNewTab ? "_blank" : undefined}
      rel={opensInNewTab ? "noopener noreferrer" : undefined}
      aria-label={label}
      className={`${className} hover:border-[#f4a076] hover:bg-[#fdf1eb]`}
    >
      <i className={`${icon} text-[16px]`} />
    </a>
  );
}

function CenteredEmptyState({
  icon,
  title,
  description,
}: {
  icon: string;
  title: string;
  description: string;
}) {
  return (
    <div className="flex min-h-[260px] w-full flex-1 flex-col items-center justify-center py-10 text-center">
      <span className="flex h-10 w-10 items-center justify-center rounded-[10px] bg-[#f5f5f5] text-[#0a0a0a]">
        <i className={`${icon} text-[22px]`} />
      </span>
      <h2 className="mt-6 text-[20px] font-semibold leading-7 tracking-normal text-[#0a0a0a]">
        {title}
      </h2>
      <p className="mt-2 max-w-[550px] text-[16px] font-normal leading-[1.6] text-[#404040]">
        {description}
      </p>
    </div>
  );
}

function normalizeTab(value?: string): OfferedLienDetailTab {
  return value === "documents" || value === "messages" ? value : "overview";
}

function safeHref(value?: string | null): string | null {
  if (!value) return null;
  if (value.startsWith("/") && !value.startsWith("//")) return value;

  try {
    const parsed = new URL(value);
    return parsed.protocol === "http:" || parsed.protocol === "https:" ? value : null;
  } catch {
    return null;
  }
}

function isExternalHref(value: string): boolean {
  return value.startsWith("http://") || value.startsWith("https://");
}

function formatOptionalDate(value?: string | null): string {
  return value ? formatFundingDate(value) : "-";
}

function formatDateTimeParts(value?: string | null): string {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "-";
  const datePart = new Intl.DateTimeFormat("en-US", {
    month: "2-digit",
    day: "2-digit",
    year: "numeric",
  }).format(date);
  const timePart = new Intl.DateTimeFormat("en-US", {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  }).format(date);
  return `${datePart}  •  ${timePart}`;
}

function formatActivityDateTime(value?: string | null): string {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "-";
  const datePart = new Intl.DateTimeFormat("en-US", {
    month: "long",
    day: "numeric",
    year: "numeric",
  }).format(date);
  const timePart = new Intl.DateTimeFormat("en-US", {
    hour: "2-digit",
    minute: "2-digit",
  }).format(date);
  return `${datePart}, ${timePart}`;
}
