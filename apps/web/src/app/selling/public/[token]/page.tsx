import { headers } from "next/headers";
import {
  fetchPublicBuyerPortal,
  type PublicBuyerPortalData,
  type PublicBuyerPortalDocument,
  type PublicBuyerPortalError,
  type PublicBuyerPortalResult,
} from "@/lib/liens/public-buyer-portal";

export const dynamic = "force-dynamic";

export const metadata = {
  title: "Lien Offer | LegalSynq",
};

interface PublicBuyerPortalPageProps {
  params: Promise<{ token: string }>;
}

interface FieldRow {
  label: string;
  value: string | number | null | undefined;
  href?: string;
}

const portalFont = {
  fontFamily: '"Plus Jakarta Sans", Arial, "Helvetica Neue", sans-serif',
};

export default async function PublicBuyerPortalPage({
  params,
}: PublicBuyerPortalPageProps) {
  const { token } = await params;
  const hdrs = await headers();
  const result = await fetchPublicBuyerPortal(token, {
    requestHost: hdrs.get("x-forwarded-host") ?? hdrs.get("host"),
    requestProto:
      hdrs.get("x-forwarded-proto") ??
      (process.env.NODE_ENV === "development" ? "http" : "https"),
  });

  return <PublicBuyerPortalShell result={result} />;
}

export function PublicBuyerPortalShell({
  result,
}: {
  result: PublicBuyerPortalResult;
}) {
  return (
    <main
      className="min-h-screen overflow-hidden bg-white text-[#0a0a0a]"
      style={portalFont}
    >
      <style
        dangerouslySetInnerHTML={{
          __html:
            '@import url("https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&display=swap");' +
            ".public-portal-primary{background:#ee7132;color:#fff;}" +
            ".public-portal-primary:hover{background:#d85f25;}" +
            ".public-portal-primary:active{background:#c95720;}" +
            ".public-portal-details:not([open]){padding-top:16px!important;padding-bottom:16px!important;}" +
            ".public-portal-details:not([open]) .details-content{display:none!important;}",
        }}
      />
      <PortalHeader />
      {result.ok ? (
        <PortalContent data={result.data} />
      ) : (
        <LinkState error={result.error} />
      )}
    </main>
  );
}

function PortalHeader() {
  return (
    <header className="flex h-20 items-center justify-center bg-[#0c1d33] px-6 py-5 max-sm:h-[72px]">
      <img
        src="/legalsynq-logo-temp-portal.svg"
        alt="LegalSynq"
        className="h-[39.5px] w-[137px] object-contain"
      />
    </header>
  );
}

function PortalContent({ data }: { data: PublicBuyerPortalData }) {
  const sellerRows: FieldRow[] = [
    { label: "Seller Name", value: data.seller.name },
    { label: "Seller Company", value: data.seller.company },
  ];
  const lienRows: FieldRow[] = [
    { label: "Submitted Date", value: formatDateTime(data.lien.submittedAtUtc) },
    { label: "Listing Visibility", value: data.lien.listingVisibility },
    { label: "Initial Service Date", value: formatDate(data.lien.initialServiceDate) },
    { label: "End Service Date", value: formatDate(data.lien.endServiceDate) },
  ];
  const fundingRows: FieldRow[] = [
    { label: "Funding Company", value: data.buyer.company },
    { label: "Handling Law Firm", value: data.case.handlingLawFirm },
    { label: "Contact Person", value: data.seller.name },
    { label: "Case Manager", value: data.case.caseManager },
    {
      label: "Email Address",
      value: data.seller.email,
      href: data.seller.email ? `mailto:${data.seller.email}` : undefined,
    },
  ];

  return (
    <section
      className="flex flex-col items-center gap-6 bg-white px-5 py-6 pb-8 max-sm:px-3.5 max-sm:py-[18px]"
      aria-label="Temporary funding company portal"
    >
      <HeroBanner />
      <ResponseCard />
      <details
        open
        className="public-portal-details group w-full max-w-[700px] rounded-2xl border border-[#e5e5e5] bg-white px-6 py-6 pb-2 shadow-[0_1px_1.5px_rgba(0,0,0,0.1)] max-sm:rounded-[14px]"
        aria-labelledby="lien-summary-title"
      >
        <summary className="-mx-2 flex min-h-10 cursor-pointer list-none items-center justify-between gap-4 rounded-lg px-2 py-1 transition-colors hover:bg-[#f5f5f5] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132] max-sm:flex-col max-sm:items-start [&::-webkit-details-marker]:hidden">
          <div className="flex items-center gap-3">
            <i className="ri-arrow-down-s-line -rotate-90 text-2xl leading-none text-[#0a0a0a] transition-transform group-open:rotate-0" aria-hidden="true" />
            <h2 id="lien-summary-title" className="m-0 text-lg font-extrabold leading-[1.6] tracking-normal">
              Lien Summary
            </h2>
          </div>
          <span className="inline-flex h-7 items-center justify-center whitespace-nowrap rounded-full bg-yellow-500/15 px-3 py-1 text-sm font-semibold leading-[1.6] text-[#a16207] max-sm:text-xs">
            Awaiting Your Response
          </span>
        </summary>
        <div className="details-content mt-6 flex flex-col gap-6">
          <FieldSection title="Seller Information" icon="ri-file-text-line" rows={sellerRows} />
          <FieldSection
            title="Lien Information"
            icon="ri-file-text-line"
            rows={lienRows}
            notes={data.lien.notes}
          />
          <FieldSection title="Funding Company & Case Information" icon="ri-building-2-line" rows={fundingRows} />
        </div>
      </details>
      <DocumentsCard documents={data.documents} />
      <MessagesCard />
      <p className="m-0 w-full max-w-[700px] text-center text-sm leading-[1.6] text-[#737373]">
        Accessible only with the secure link from the email. The link will
        expire 30 days from the date it was sent.
      </p>
    </section>
  );
}

function HeroBanner() {
  return (
    <section
      className="relative w-full max-w-[700px] overflow-hidden rounded-2xl bg-[#0d1e34] p-8 text-[#fafafa] shadow-[0_1px_3px_rgba(0,0,0,0.1)] max-sm:rounded-[14px] max-sm:p-6"
      aria-labelledby="manage-offered-liens-title"
    >
      <div className="absolute inset-0 bg-[radial-gradient(circle_at_86%_36%,rgba(255,255,255,0.12),transparent_25%),linear-gradient(180deg,rgba(12,29,51,0),#0c1d33)] opacity-80" />
      <div
        className="absolute right-[-54px] top-7 h-44 w-44 bg-contain bg-center bg-no-repeat"
        style={{ backgroundImage: 'url("/legalsynq-temp-portal-watermark.svg")' }}
        aria-hidden="true"
      />
      <div className="relative z-10">
        <div className="mb-2 flex items-center justify-between gap-4 max-sm:flex-col max-sm:items-start">
          <h1 id="manage-offered-liens-title" className="m-0 text-lg font-extrabold leading-[1.6] tracking-normal">
            Manage Offered Liens
          </h1>
          <button
            type="button"
            className="public-portal-primary inline-flex h-[38px] cursor-pointer items-center justify-center whitespace-nowrap rounded-[10px] border border-transparent px-4 py-2 text-sm font-semibold leading-[1.6] text-white shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors hover:shadow-[0_4px_10px_rgba(238,113,50,0.28)] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132]"
          >
            Activate Free Account
          </button>
        </div>
        <p className="m-0 max-w-[560px] text-base leading-[1.6] text-white/90">
          Manage all lien submissions sent to your company, from initial review
          through the final purchase decision.
        </p>
      </div>
    </section>
  );
}

function ResponseCard() {
  return (
    <section
      className="flex w-full max-w-[700px] flex-col gap-10 rounded-2xl border border-[#e5e5e5] bg-white p-6 shadow-[0_1px_1.5px_rgba(0,0,0,0.1)] max-sm:rounded-[14px]"
      aria-labelledby="response-title"
    >
      <div className="flex flex-col gap-2">
        <h2 id="response-title" className="m-0 text-lg font-extrabold leading-[1.6] tracking-normal">
          Your Response
        </h2>
        <p className="m-0 text-base leading-[1.6] text-[#737373]">
          Respond directly from this page, or log in to your funding company dashboard.
        </p>
      </div>
      <div className="flex flex-col gap-2">
        <div className="flex gap-3 max-sm:flex-col">
          <button
            type="button"
            className="public-portal-primary inline-flex h-[38px] flex-1 cursor-pointer items-center justify-center rounded-[10px] border border-transparent px-4 py-2 text-sm font-semibold leading-[1.6] text-white shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors hover:shadow-[0_4px_10px_rgba(238,113,50,0.24)] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132]"
          >
            Accept Lien
          </button>
          <button
            type="button"
            className="inline-flex h-[38px] flex-1 cursor-pointer items-center justify-center rounded-[10px] border border-red-600 bg-white px-4 py-2 text-sm font-semibold leading-[1.6] text-red-600 shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors hover:bg-red-50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-red-600 active:bg-red-100"
          >
            Decline Lien
          </button>
        </div>
        <p className="m-0 text-sm leading-[1.6] text-[#737373]">
          Your response is securely recorded.{" "}
          <a href="/login" className="cursor-pointer text-[#ee7132] underline underline-offset-2 transition-colors hover:text-[#d85f25]">
            Log in
          </a>{" "}
          to manage from your dashboard.
        </p>
      </div>
    </section>
  );
}

function FieldSection({
  title,
  icon,
  rows,
  notes,
}: {
  title: string;
  icon: string;
  rows: FieldRow[];
  notes?: string | null;
}) {
  const visibleRows = rows.filter(row => row.value !== null && row.value !== undefined && `${row.value}`.trim());
  if (visibleRows.length === 0 && !notes) return null;

  return (
    <section className="border-b border-[#e5e5e5] pb-4 last:border-b-0">
      <div className="mb-4 flex items-center gap-2 text-sm font-bold leading-[1.6] text-[#0a0a0a]">
        <i className={`${icon} text-lg leading-none`} aria-hidden="true" />
        <span>{title}</span>
      </div>
      {visibleRows.length > 0 ? (
        <div className="grid grid-cols-2 gap-x-12 gap-y-4 max-sm:grid-cols-1 max-sm:gap-y-3.5">
          {visibleRows.map(row => (
            <div key={row.label} className="flex min-w-0 flex-col gap-1.5">
              <span className="text-sm leading-[1.6] text-[#737373]">{row.label}</span>
              {row.href ? (
                <a
                  href={row.href}
                  className="break-words text-sm font-semibold leading-[1.6] text-[#0a0a0a] no-underline"
                >
                  {row.value}
                </a>
              ) : (
                <span className="break-words text-sm font-semibold leading-[1.6] text-[#0a0a0a]">
                  {row.value}
                </span>
              )}
            </div>
          ))}
        </div>
      ) : null}
      {notes ? (
        <div className="mt-4 flex flex-col gap-1.5">
          <span className="text-sm leading-[1.6] text-[#737373]">Lien Notes</span>
          <p className="m-0 text-sm font-semibold leading-[1.6] text-[#0a0a0a]">
            {notes}
          </p>
        </div>
      ) : null}
    </section>
  );
}

function DocumentsCard({ documents }: { documents: PublicBuyerPortalDocument[] }) {
  return (
    <details
      open
      className="public-portal-details group w-full max-w-[700px] rounded-2xl border border-[#e5e5e5] bg-white p-6 shadow-[0_1px_1.5px_rgba(0,0,0,0.1)] max-sm:rounded-[14px]"
      aria-labelledby="documents-title"
    >
      <summary className="-mx-2 flex min-h-10 cursor-pointer list-none items-center gap-3 rounded-lg px-2 py-1 transition-colors hover:bg-[#f5f5f5] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132] [&::-webkit-details-marker]:hidden">
        <i className="ri-arrow-down-s-line -rotate-90 text-2xl leading-none text-[#0a0a0a] transition-transform group-open:rotate-0" aria-hidden="true" />
        <h2 id="documents-title" className="m-0 text-lg font-extrabold leading-[1.6] tracking-normal">
          Documents{documents.length > 0 ? ` (${documents.length})` : ""}
        </h2>
      </summary>
      <div className="details-content mt-6">
        {documents.length === 0 ? (
          <EmptyState icon="ri-file-text-line" message="No supporting documents are available for this lien." />
        ) : (
          <div className="flex flex-col gap-3">
            {documents.map(document => (
              <article
                key={document.fileName}
                className="flex items-center justify-between gap-6 rounded-xl border border-dashed border-[#e5e5e5] p-6 max-sm:flex-col max-sm:items-stretch"
              >
                <div className="flex min-w-0 items-start gap-3">
                  <span className="flex h-14 w-14 shrink-0 items-center justify-center rounded-xl bg-[#f5f5f5] text-[#333]">
                    <i className="ri-file-text-line text-2xl leading-none" aria-hidden="true" />
                  </span>
                  <div className="flex min-w-0 flex-col gap-2">
                    <div className="break-words text-base font-bold leading-5 text-[#0a0a0a]">
                      {document.fileName}
                    </div>
                    <div className="flex flex-wrap items-center gap-2 text-base leading-[1.6] text-[#737373]">
                      <span>{document.category ?? "Document"}</span>
                      {document.sizeOrType ? (
                        <>
                          <span aria-hidden="true">&middot;</span>
                          <span>{document.sizeOrType}</span>
                        </>
                      ) : null}
                    </div>
                  </div>
                </div>
                <div className="flex shrink-0 items-center gap-3 max-sm:self-end">
                  <button
                    type="button"
                    aria-label="View document"
                    className="flex h-9 w-9 cursor-pointer items-center justify-center rounded-[10px] border border-[#e5e5e5] bg-white text-[#333] shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors hover:border-[#d6d6d6] hover:bg-[#f5f5f5] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132] active:bg-[#ededed]"
                  >
                    <i className="ri-eye-line text-base leading-none" aria-hidden="true" />
                  </button>
                  <button
                    type="button"
                    aria-label="Download document"
                    className="flex h-9 w-9 cursor-pointer items-center justify-center rounded-[10px] border border-[#e5e5e5] bg-white text-[#333] shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors hover:border-[#d6d6d6] hover:bg-[#f5f5f5] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132] active:bg-[#ededed]"
                  >
                    <i className="ri-download-line text-base leading-none" aria-hidden="true" />
                  </button>
                </div>
              </article>
            ))}
          </div>
        )}
      </div>
    </details>
  );
}

function MessagesCard() {
  return (
    <details
      open
      className="public-portal-details group w-full max-w-[700px] rounded-2xl border border-[#e5e5e5] bg-white p-6 shadow-[0_1px_1.5px_rgba(0,0,0,0.1)] max-sm:rounded-[14px]"
      aria-labelledby="messages-title"
    >
      <summary className="-mx-2 flex min-h-10 cursor-pointer list-none items-center gap-3 rounded-lg px-2 py-1 transition-colors hover:bg-[#f5f5f5] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132] [&::-webkit-details-marker]:hidden">
        <i className="ri-arrow-down-s-line -rotate-90 text-2xl leading-none text-[#0a0a0a] transition-transform group-open:rotate-0" aria-hidden="true" />
        <h2 id="messages-title" className="m-0 text-lg font-extrabold leading-[1.6] tracking-normal">
          Messages
        </h2>
      </summary>
      <div className="details-content mt-6 flex flex-col gap-6">
        <EmptyState icon="ri-message-3-line" message="No messages yet. Send a message to the seller below." />
        <div className="flex w-full items-center gap-4 rounded-xl border border-[#e5e5e5] py-3 pl-4 pr-3 shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors focus-within:border-[#ee7132]">
          <input
            aria-label="Message"
            placeholder="Type a message..."
            maxLength={400}
            className="min-w-0 flex-1 border-0 text-sm text-[#737373] outline-none"
          />
          <span className="whitespace-nowrap text-sm text-[#737373]">0/400</span>
          <button
            type="button"
            aria-label="Send message"
            className="flex h-9 w-9 cursor-pointer items-center justify-center rounded-full border-0 bg-[#ee7132] text-white shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors hover:bg-[#d85f25] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132] active:bg-[#c95720]"
          >
            <i className="ri-send-plane-2-line text-base leading-none" aria-hidden="true" />
          </button>
        </div>
      </div>
    </details>
  );
}

function EmptyState({ icon, message }: { icon: string; message: string }) {
  return (
    <div className="flex flex-col items-center gap-4 py-10 text-center text-sm leading-[1.6] text-[#737373]">
      <span className="flex h-14 w-14 items-center justify-center rounded-xl bg-[#f5f5f5] text-[#333]">
        <i className={`${icon} text-2xl leading-none`} aria-hidden="true" />
      </span>
      <p className="m-0">{message}</p>
    </div>
  );
}

function LinkState({ error }: { error: PublicBuyerPortalError }) {
  return (
    <section className="flex min-h-[calc(100vh-80px)] items-center justify-center bg-white p-6 max-sm:min-h-[calc(100vh-72px)]">
      <div className="w-full max-w-[520px] rounded-2xl border border-[#e5e5e5] p-7 text-center shadow-[0_1px_1.5px_rgba(0,0,0,0.1)]">
        <h1 className="m-0 mb-2 text-[22px] font-bold text-[#0a0a0a]">
          {error.title}
        </h1>
        <p className="m-0 leading-[1.6] text-[#737373]">{error.message}</p>
      </div>
    </section>
  );
}

function formatDate(value: string | null): string | null {
  if (!value) return null;
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
  if (!match) return value;
  return `${match[2]}/${match[3]}/${match[1]}`;
}

function formatDateTime(value: string | null): string | null {
  if (!value) return null;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;

  const datePart = new Intl.DateTimeFormat("en-US", {
    timeZone: "UTC",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(date);
  const timePart = new Intl.DateTimeFormat("en-US", {
    timeZone: "UTC",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  }).format(date);

  return `${datePart} - ${timePart}`;
}
