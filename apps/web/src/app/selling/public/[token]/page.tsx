import { headers } from "next/headers";
import {
  fetchPublicBuyerPortal,
  SYNQLIEN_BUYER_LOGIN_URL,
  type PublicBuyerPortalAccount,
  type PublicBuyerPortalDocument,
  type PublicBuyerPortalError,
  type PublicBuyerPortalResult,
} from "@/lib/liens/public-buyer-portal";
import { PublicPortalMessagesCard } from "./messages-client";
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
            ".public-portal-sidebar-ring{border:64px solid rgba(250,250,250,0.05);}" +
            ".public-portal-details:not([open]){padding-top:16px!important;padding-bottom:16px!important;}" +
            ".public-portal-details:not([open]) .details-content{display:none!important;}",
        }}
      />
      {result.ok ? (
        <PortalContent token={token} result={result} />
      ) : (
        <LinkState error={result.error} />
      )}
    </main>
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
  const isSellerView = data.audience === "seller";
  const sidebarTitle = isSellerView ? "View Offered Liens" : "Manage Offered Liens";
  const sidebarCopy = isSellerView
    ? "Review the lien details and funding company contact tied to this submitted offer."
    : "Manage all lien submissions sent to your company, from initial review through the final purchase decision.";

  return (
    <section
      className="flex min-h-screen justify-center bg-white px-6 pb-6 pt-10 max-lg:flex-col max-lg:items-center max-lg:gap-6 max-sm:px-3.5 max-sm:pt-4"
      aria-label={isSellerView ? "Temporary seller lien portal" : "Temporary funding company portal"}
    >
      <PortalSidebar
        token={token}
        audience={data.audience}
        account={data.account}
        title={sidebarTitle}
        copy={sidebarCopy}
      />
      <div className="flex w-full max-w-[700px] flex-col items-center gap-6 pl-6 pr-3 max-lg:px-0">
        <PublicBuyerPortalInteractiveContent token={token} data={data} />
        <DocumentsCard documents={data.documents} />
        <PublicPortalMessagesCard token={token} audience={data.audience} initialMessages={data.messages} />
        <p className="m-0 w-full text-center text-sm leading-[1.6] text-[#737373]">
          Accessible only with the secure link from the email. The link will
          expire 30 days from the date it was sent.
        </p>
      </div>
    </section>
  );
}

function PortalSidebar({
  token,
  audience,
  account,
  title,
  copy,
}: {
  token: string;
  audience: "buyer" | "seller";
  account?: PublicBuyerPortalAccount | null;
  title: string;
  copy: string;
}) {
  const isSellerView = audience === "seller";
  const hasExistingAccount = account?.hasExistingAccount === true;
  const ctaHref = hasExistingAccount
    ? account?.loginUrl || SYNQLIEN_BUYER_LOGIN_URL
    : `/selling/public/${encodeURIComponent(token)}/activate`;
  const ctaLabel = hasExistingAccount ? "Log In" : "Activate Free Account";

  return (
    <aside
      className="relative flex h-[984px] min-h-[680px] w-[380px] shrink-0 flex-col overflow-hidden rounded-[20px] bg-[#0c1d33] p-8 text-[#fafafa] lg:sticky lg:top-10 max-lg:h-auto max-lg:min-h-[420px] max-lg:w-full max-lg:max-w-[700px] max-sm:min-h-[360px] max-sm:rounded-2xl max-sm:p-6"
      aria-labelledby="manage-offered-liens-title"
    >
      <div className="public-portal-sidebar-ring absolute right-[-118px] top-[-93px] h-[289px] w-[300px] rounded-full" aria-hidden="true" />
      <div className="public-portal-sidebar-ring absolute bottom-[-126px] left-[-134px] h-[365px] w-[379px] rounded-full" aria-hidden="true" />
      <div className="relative z-10 flex items-start gap-2">
        <img
          src="/figma/synqlien-funding-public/icon-logo.svg"
          alt=""
          className="h-6 w-[23.683px] object-contain"
        />
        <div className="flex flex-col gap-2">
          <p className="m-0 text-sm font-normal leading-[1.6] text-[#d4d4d4]">LEGALSYNQ</p>
          <p className="m-0 text-xl font-medium leading-7 text-[#fafafa]">Funding Company Portal</p>
        </div>
      </div>
      <div className="relative z-10 flex flex-1 flex-col justify-center gap-6">
        <div className="flex flex-col gap-4">
          <span className="h-[3px] w-[53px] rounded-full bg-[#ee7132]" aria-hidden="true" />
          <h1 id="manage-offered-liens-title" className="m-0 text-[32px] font-bold leading-10 tracking-normal text-white">
            {title}
          </h1>
          <p className="m-0 text-base font-normal leading-[1.6] text-[#fafafa]">
            {copy}
          </p>
        </div>
        {isSellerView ? null : (
          <a
            href={ctaHref}
            className="public-portal-primary inline-flex h-[38px] w-full cursor-pointer items-center justify-center rounded-[10px] border border-transparent px-4 py-2 text-sm font-medium leading-[1.6] text-white shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132]"
          >
            {ctaLabel}
          </a>
        )}
      </div>
    </aside>
  );
}

function DocumentsCard({ documents }: { documents: PublicBuyerPortalDocument[] }) {
  return (
    <details
      open
      className="public-portal-details group w-full max-w-[700px] rounded-2xl border border-[#e5e5e5] bg-white p-6 shadow-[0_1px_1.5px_rgba(0,0,0,0.1)] max-sm:rounded-[14px] max-sm:p-4"
      aria-labelledby="documents-title"
    >
      <summary className="-mx-2 flex min-h-10 cursor-pointer list-none items-center gap-3 rounded-lg px-2 py-1 transition-colors hover:bg-[#f5f5f5] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132] [&::-webkit-details-marker]:hidden">
        <i className="ri-arrow-down-s-line -rotate-90 text-2xl leading-none text-[#0a0a0a] transition-transform group-open:rotate-0" aria-hidden="true" />
        <h2 id="documents-title" className="m-0 text-lg font-bold leading-[1.6] tracking-normal">
          Documents{" "}
          {documents.length > 0 ? (
            <span className="text-[#737373]">({documents.length})</span>
          ) : null}
        </h2>
      </summary>
      <div className="details-content mt-6">
        {documents.length === 0 ? (
          <EmptyState icon="ri-file-text-line" message="No supporting documents are available for this lien." />
        ) : (
          <div className="flex flex-col gap-4">
            {documents.map(document => (
              <article
                key={document.id ?? document.fileName}
                className="flex items-center justify-between gap-6 rounded-xl border border-dashed border-[#e5e5e5] p-6 max-sm:flex-col max-sm:items-stretch max-sm:p-4"
              >
                <div className="flex min-w-0 items-start gap-3">
                  <span className="flex h-14 w-14 shrink-0 items-center justify-center rounded-xl bg-[#f5f5f5] text-[#333]">
                    <i className="ri-file-text-line text-2xl leading-none" aria-hidden="true" />
                  </span>
                  <div className="flex min-w-0 flex-col gap-2">
                    <div className="break-words text-base font-semibold capitalize leading-5 text-[#0a0a0a]">
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
                  <DocumentActionLink
                    href={document.viewUrl}
                    label={`View ${document.fileName}`}
                    icon="ri-eye-line"
                    openInNewTab
                  />
                  <DocumentActionLink
                    href={document.downloadUrl}
                    label={`Download ${document.fileName}`}
                    icon="ri-download-line"
                  />
                </div>
              </article>
            ))}
          </div>
        )}
      </div>
    </details>
  );
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
  const safeUrl = safeHref(href);
  const className =
    "flex h-9 w-9 items-center justify-center rounded-[10px] border border-[#e5e5e5] bg-white text-[#333] shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132]";

  if (!safeUrl) {
    return (
      <span
        aria-label={label}
        aria-disabled="true"
        className={`${className} cursor-not-allowed opacity-50`}
        role="button"
      >
        <i className={`${icon} text-base leading-none`} aria-hidden="true" />
      </span>
    );
  }

  const opensInNewTab = openInNewTab || isExternalHref(safeUrl);

  return (
    <a
      href={safeUrl}
      target={opensInNewTab ? "_blank" : undefined}
      rel={opensInNewTab ? "noopener noreferrer" : undefined}
      aria-label={label}
      className={`${className} cursor-pointer hover:border-[#d6d6d6] hover:bg-[#f5f5f5] active:bg-[#ededed]`}
    >
      <i className={`${icon} text-base leading-none`} aria-hidden="true" />
    </a>
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

function safeHref(value?: string | null): string | null {
  const trimmed = value?.trim();
  if (!trimmed) return null;
  if (trimmed.startsWith("/")) return trimmed;

  try {
    const parsed = new URL(trimmed);
    if (parsed.protocol === "http:" || parsed.protocol === "https:") {
      return parsed.toString();
    }
  } catch {
    return null;
  }

  return null;
}

function isExternalHref(value: string): boolean {
  try {
    const parsed = new URL(value, "https://portal.legalsynq.local");
    return parsed.origin !== "https://portal.legalsynq.local";
  } catch {
    return false;
  }
}

function LinkState({ error }: { error: PublicBuyerPortalError }) {
  return (
    <section className="flex min-h-screen items-center justify-center bg-white p-6">
      <div className="w-full max-w-[520px] rounded-2xl border border-[#e5e5e5] p-7 text-center shadow-[0_1px_1.5px_rgba(0,0,0,0.1)]">
        <h1 className="m-0 mb-2 text-[22px] font-bold text-[#0a0a0a]">
          {error.title}
        </h1>
        <p className="m-0 leading-[1.6] text-[#737373]">{error.message}</p>
      </div>
    </section>
  );
}
