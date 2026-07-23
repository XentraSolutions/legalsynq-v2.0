import { headers } from "next/headers";
import {
  fetchPublicBuyerPortal,
  type PublicBuyerPortalDocument,
  type PublicBuyerPortalError,
  type PublicBuyerPortalResult,
} from "@/lib/liens/public-buyer-portal";
import { PublicBuyerPortalInteractiveContent } from "./response-client";

export const dynamic = "force-dynamic";

export const metadata = {
  title: "Lien Offer | LegalSynq",
};

interface PublicBuyerPortalPageProps {
  params: Promise<{ token: string }>;
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

  return <PublicBuyerPortalShell result={result} token={token} />;
}

function PublicBuyerPortalShell({
  result,
  token,
}: {
  result: PublicBuyerPortalResult;
  token: string;
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
        <PortalContent token={token} result={result} />
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

function PortalContent({
  token,
  result,
}: {
  token: string;
  result: Extract<PublicBuyerPortalResult, { ok: true }>;
}) {
  const { data } = result;
  return (
    <section
      className="flex flex-col items-center gap-6 bg-white px-5 py-6 pb-8 max-sm:px-3.5 max-sm:py-[18px]"
      aria-label="Temporary funding company portal"
    >
      <HeroBanner />
      <PublicBuyerPortalInteractiveContent token={token} data={data} />
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
