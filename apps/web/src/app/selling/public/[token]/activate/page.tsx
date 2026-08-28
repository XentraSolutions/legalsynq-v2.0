import { headers } from "next/headers";
import {
  fetchPublicBuyerPortal,
  SYNQLIEN_BUYER_LOGIN_URL,
  type PublicBuyerPortalError,
  type PublicBuyerPortalResult,
} from "@/lib/liens/public-buyer-portal";

export const dynamic = "force-dynamic";

export const metadata = {
  title: "Activate Buyer Account | LegalSynq",
};

interface PublicBuyerActivationPageProps {
  params: Promise<{ token: string }>;
}

const portalFont = {
  fontFamily: '"Plus Jakarta Sans", Arial, "Helvetica Neue", sans-serif',
};

export default async function PublicBuyerActivationPage({
  params,
}: PublicBuyerActivationPageProps) {
  const { token } = await params;
  const hdrs = await headers();
  const result = await fetchPublicBuyerPortal(token, {
    requestHost: hdrs.get("x-forwarded-host") ?? hdrs.get("host"),
    requestProto:
      hdrs.get("x-forwarded-proto") ??
      (process.env.NODE_ENV === "development" ? "http" : "https"),
  });

  return <ActivationShell token={token} result={result} />;
}

function ActivationShell({
  token,
  result,
}: {
  token: string;
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
            ".public-portal-primary:active{background:#c95720;}",
        }}
      />
      {result.ok ? (
        <ActivationContent token={token} result={result} />
      ) : (
        <LinkState error={result.error} />
      )}
    </main>
  );
}

function ActivationContent({
  token,
  result,
}: {
  token: string;
  result: Extract<PublicBuyerPortalResult, { ok: true }>;
}) {
  const { data } = result;

  return (
    <section
      className="flex flex-col items-center bg-[#fafafa] px-5 pb-12 max-sm:px-3.5"
      aria-label="Activate SynqLien buyer account"
    >
      <IntroScreen
        token={token}
        loginUrl={data.account?.loginUrl || SYNQLIEN_BUYER_LOGIN_URL}
        hasExistingAccount={data.account?.hasExistingAccount === true}
      />
    </section>
  );
}

function IntroScreen({
  token,
  loginUrl,
  hasExistingAccount,
}: {
  token: string;
  loginUrl: string;
  hasExistingAccount: boolean;
}) {
  return (
    <section className="flex min-h-screen w-full flex-col items-center justify-center py-12">
      <div className="flex w-full max-w-[574px] flex-col gap-4 pb-3">
        <a
          href={`/selling/public/${encodeURIComponent(token)}`}
          aria-label="Back to lien offer"
          className="flex h-9 w-9 items-center justify-center rounded-full border border-[#e5e5e5] bg-white text-[#0a0a0a] shadow-[0_1px_2px_rgba(0,0,0,0.05)] transition-colors hover:bg-[#f5f5f5] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132]"
        >
          <img
            src="/figma/synqlien-funding-intro/arrow-left.svg"
            alt=""
            className="h-4 w-4"
          />
        </a>
      </div>
      <div className="flex w-full max-w-[574px] flex-col items-center gap-8">
        <div className="flex w-full flex-col items-start">
          <div className="flex w-full items-center justify-center pb-4 pt-3">
            <div className="border-r border-[#d4d4d4] pr-4">
              <img
                src="/figma/synqlien-funding-intro/legalsynq-logo.svg"
                alt="LegalSynq"
                className="h-10 w-[141px] object-contain"
              />
            </div>
            <div className="flex h-10 items-center pl-4 text-xl font-normal leading-7 text-black">
              <span className="font-semibold text-[#0d1e34]">Funding</span>
              <span className="text-[#ee7132]">Company</span>
            </div>
          </div>
          <div className="flex w-full flex-col gap-4 py-4 text-center">
            <h1 className="m-0 w-full text-[52px] font-semibold leading-[56px] tracking-normal text-[#0f172a] max-sm:text-[36px] max-sm:leading-[40px]">
              Review, and manage liens in one place
            </h1>
            <p className="m-0 w-full text-base font-normal leading-[1.6] text-[#737373]">
              A centralized workspace for funding companies to evaluate, and manage medical liens seamlessly.
            </p>
          </div>
          <div className="grid w-full grid-cols-2 gap-4 pb-5 pt-4 max-sm:grid-cols-1">
            <IntroFeature
              iconSrc="/figma/synqlien-funding-intro/file-input.svg"
              title="Manage Offered Liens"
              description="Review, accept, evaluate, or reject incoming medical lien offers."
            />
            <IntroFeature
              iconSrc="/figma/synqlien-funding-intro/layout-dashboard.svg"
              title="Portal Overview"
              description="Track key metrics, including total pending liens, purchased liens, and capital deployed."
            />
            <IntroFeature
              iconSrc="/figma/synqlien-funding-intro/bell-ring.svg"
              title="Real-Time Notifications"
              description="Receive instant alerts and stay updated whenever new lien offers or updates require action."
            />
            <IntroFeature
              iconSrc="/figma/synqlien-funding-intro/receipt-text.svg"
              title="Track Purchases & Capital"
              description="Monitor active investments, track pending offers, and audit settled payouts in one place."
            />
          </div>
        </div>
        <div className="flex w-full flex-col items-center gap-4 pt-5">
          <p className="m-0 text-center text-base leading-[1.6] text-[#737373]">
            Takes less than 10 minutes <span aria-hidden="true">&bull;</span> Token secure link verified
          </p>
          {hasExistingAccount ? (
            <a
              href={loginUrl}
              className="public-portal-primary inline-flex h-[38px] w-full items-center justify-center rounded-[10px] px-4 py-2 text-sm font-medium leading-[1.6] text-white shadow-[0_1px_2px_rgba(0,0,0,0.1)]"
            >
              Log In
            </a>
          ) : (
            <a
              href={`/selling/public/${encodeURIComponent(token)}/activate/register`}
              className="public-portal-primary inline-flex h-[38px] w-full items-center justify-center overflow-hidden rounded-[10px] text-sm font-medium leading-[1.6] text-white shadow-[0_1px_2px_rgba(0,0,0,0.1)]"
            >
              <span className="flex h-full flex-1 items-center justify-center px-4">Get Started</span>
              <span className="flex h-full w-9 items-center justify-center border-l border-[#f4a076]">
                <img
                  src="/figma/synqlien-funding-intro/arrow-right.svg"
                  alt=""
                  className="h-4 w-4"
                />
              </span>
            </a>
          )}
          <p className="m-0 flex flex-wrap justify-center gap-1 text-center text-base leading-[1.6] text-[#737373]">
            <span>Already have an activated portal account?</span>
            <a href={loginUrl} className="font-semibold text-[#ee7132] no-underline hover:underline">
              Log In
            </a>
          </p>
        </div>
      </div>
    </section>
  );
}

function IntroFeature({
  iconSrc,
  title,
  description,
}: {
  iconSrc: string;
  title: string;
  description: string;
}) {
  return (
    <article className="flex min-h-[162px] flex-col items-start rounded-lg bg-white px-4 pb-8 pt-4">
      <div className="mb-5 flex w-full justify-end">
        <img src={iconSrc} alt="" className="h-6 w-6" />
      </div>
      <h2 className="m-0 whitespace-pre-line text-2xl font-medium leading-8 text-[#404040]">
        {title}
      </h2>
      <p className="m-0 mt-5 text-base font-normal leading-[1.6] text-[#404040]">
        {description}
      </p>
    </article>
  );
}

function LinkState({ error }: { error: PublicBuyerPortalError }) {
  return (
    <section className="flex min-h-screen items-center justify-center bg-[#fafafa] p-6">
      <div className="w-full max-w-[520px] rounded-2xl border border-[#e5e5e5] p-7 text-center shadow-[0_1px_1.5px_rgba(0,0,0,0.1)]">
        <h1 className="m-0 mb-2 text-[22px] font-bold text-[#0a0a0a]">
          {error.title}
        </h1>
        <p className="m-0 leading-[1.6] text-[#737373]">{error.message}</p>
      </div>
    </section>
  );
}
