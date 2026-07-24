import { headers } from "next/headers";
import {
  fetchPublicBuyerPortal,
  type PublicBuyerPortalData,
  type PublicBuyerPortalError,
  type PublicBuyerPortalResult,
} from "@/lib/liens/public-buyer-portal";
import { PublicBuyerActivationForm } from "./activation-form";

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
      <PortalHeader />
      {result.ok ? (
        <ActivationContent token={token} data={result.data} />
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

function ActivationContent({
  token,
  data,
}: {
  token: string;
  data: PublicBuyerPortalData;
}) {
  return (
    <section
      className="flex flex-col items-center gap-6 bg-white px-5 py-6 pb-8 max-sm:px-3.5 max-sm:py-[18px]"
      aria-label="Activate SynqLien buyer account"
    >
      <HeroBanner token={token} />
      <PublicBuyerActivationForm token={token} data={data} />
      <p className="m-0 w-full max-w-[700px] text-center text-sm leading-[1.6] text-[#737373]">
        Already have platform access?{" "}
        <a
          href="/login?returnTo=%2Ffunding%2Foffered-liens&reason=synqlien-buyer-activation"
          className="cursor-pointer text-[#ee7132] underline underline-offset-2 transition-colors hover:text-[#d85f25]"
        >
          Log in
        </a>{" "}
        with your existing account.
      </p>
    </section>
  );
}

function HeroBanner({ token }: { token: string }) {
  return (
    <section
      className="relative w-full max-w-[700px] overflow-hidden rounded-2xl bg-[#0d1e34] p-8 text-[#fafafa] shadow-[0_1px_3px_rgba(0,0,0,0.1)] max-sm:rounded-[14px] max-sm:p-6"
      aria-labelledby="activate-buyer-account-title"
    >
      <div className="absolute inset-0 bg-[radial-gradient(circle_at_86%_36%,rgba(255,255,255,0.12),transparent_25%),linear-gradient(180deg,rgba(12,29,51,0),#0c1d33)] opacity-80" />
      <div
        className="absolute right-[-54px] top-7 h-44 w-44 bg-contain bg-center bg-no-repeat"
        style={{ backgroundImage: 'url("/legalsynq-temp-portal-watermark.svg")' }}
        aria-hidden="true"
      />
      <div className="relative z-10">
        <div className="mb-2 flex items-center justify-between gap-4 max-sm:flex-col max-sm:items-start">
          <h1 id="activate-buyer-account-title" className="m-0 text-lg font-extrabold leading-[1.6] tracking-normal">
            Activate Buyer Account
          </h1>
          <a
            href={`/selling/public/${encodeURIComponent(token)}`}
            className="public-portal-primary inline-flex h-[38px] cursor-pointer items-center justify-center whitespace-nowrap rounded-[10px] border border-transparent px-4 py-2 text-sm font-semibold leading-[1.6] text-white shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors hover:shadow-[0_4px_10px_rgba(238,113,50,0.28)] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132]"
          >
            Back to Lien Offer
          </a>
        </div>
        <p className="m-0 max-w-[560px] text-base leading-[1.6] text-white/90">
          Create your funding company login to manage offered liens, responses,
          documents, and purchase workflow from your dashboard.
        </p>
      </div>
    </section>
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
