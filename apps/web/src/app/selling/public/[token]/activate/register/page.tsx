import { headers } from "next/headers";
import {
  fetchPublicBuyerPortal,
  SYNQLIEN_BUYER_LOGIN_URL,
  type PublicBuyerPortalError,
  type PublicBuyerPortalResult,
} from "@/lib/liens/public-buyer-portal";
import { PublicBuyerActivationForm } from "../activation-form";

export const dynamic = "force-dynamic";

export const metadata = {
  title: "Create Portal Login | LegalSynq",
};

interface PublicBuyerActivationRegisterPageProps {
  params: Promise<{ token: string }>;
}

const portalFont = {
  fontFamily: '"Plus Jakarta Sans", Arial, "Helvetica Neue", sans-serif',
};

export default async function PublicBuyerActivationRegisterPage({
  params,
}: PublicBuyerActivationRegisterPageProps) {
  const { token } = await params;
  const hdrs = await headers();
  const result = await fetchPublicBuyerPortal(token, {
    requestHost: hdrs.get("x-forwarded-host") ?? hdrs.get("host"),
    requestProto:
      hdrs.get("x-forwarded-proto") ??
      (process.env.NODE_ENV === "development" ? "http" : "https"),
  });

  return <ActivationRegisterShell token={token} result={result} />;
}

function ActivationRegisterShell({
  token,
  result,
}: {
  token: string;
  result: PublicBuyerPortalResult;
}) {
  return (
    <main
      className="min-h-screen overflow-hidden bg-[#fafafa] text-[#0a0a0a]"
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
        <RegisterContent token={token} result={result} />
      ) : (
        <LinkState error={result.error} />
      )}
    </main>
  );
}

function RegisterContent({
  token,
  result,
}: {
  token: string;
  result: Extract<PublicBuyerPortalResult, { ok: true }>;
}) {
  const { data } = result;

  return (
    <section
      className="flex min-h-screen flex-col items-center gap-6 px-5 py-12 max-sm:px-3.5"
      aria-label="Create funding company portal login"
    >
      <div className="flex w-full max-w-[700px] items-center">
        <a
          href={`/selling/public/${encodeURIComponent(token)}/activate`}
          aria-label="Back to funding company intro"
          className="flex h-9 w-9 items-center justify-center rounded-full border border-[#e5e5e5] bg-white text-[#0a0a0a] shadow-[0_1px_2px_rgba(0,0,0,0.05)] transition-colors hover:bg-[#f5f5f5] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132]"
        >
          <i className="ri-arrow-left-line text-base leading-none" aria-hidden="true" />
        </a>
      </div>
      {data.account?.hasExistingAccount ? (
        <ExistingAccountCard loginUrl={data.account.loginUrl || SYNQLIEN_BUYER_LOGIN_URL} />
      ) : (
        <PublicBuyerActivationForm token={token} data={data} />
      )}
      {data.account?.hasExistingAccount ? null : (
        <p className="m-0 w-full max-w-[700px] text-center text-sm leading-[1.6] text-[#737373]">
          Already have platform access?{" "}
          <a
            href={data.account?.loginUrl || SYNQLIEN_BUYER_LOGIN_URL}
            className="cursor-pointer text-[#ee7132] underline underline-offset-2 transition-colors hover:text-[#d85f25]"
          >
            Log in
          </a>{" "}
          with your existing account.
        </p>
      )}
    </section>
  );
}

function ExistingAccountCard({ loginUrl }: { loginUrl: string }) {
  return (
    <section
      className="flex w-full max-w-[700px] flex-col gap-5 rounded-2xl border border-[#d1fae5] bg-white p-6 shadow-[0_1px_1.5px_rgba(0,0,0,0.1)] max-sm:rounded-[14px]"
      aria-labelledby="existing-account-title"
    >
      <div className="flex items-start gap-4">
        <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-emerald-500/15 text-emerald-700">
          <i className="ri-login-circle-line text-2xl leading-none" aria-hidden="true" />
        </span>
        <div className="min-w-0">
          <h1 id="existing-account-title" className="m-0 text-lg font-extrabold leading-[1.6] tracking-normal text-[#0a0a0a]">
            Account already exists
          </h1>
          <p className="m-0 text-sm leading-[1.6] text-[#737373]">
            Log in with your existing account to manage offered liens.
          </p>
        </div>
      </div>
      <a
        href={loginUrl}
        className="public-portal-primary inline-flex h-11 items-center justify-center rounded-[10px] px-4 py-2 text-sm font-semibold leading-[1.6] text-white shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors hover:shadow-[0_4px_10px_rgba(238,113,50,0.24)] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132]"
      >
        Log In
      </a>
    </section>
  );
}

function LinkState({ error }: { error: PublicBuyerPortalError }) {
  return (
    <section className="flex min-h-screen items-center justify-center bg-[#fafafa] p-6">
      <div className="w-full max-w-[520px] rounded-2xl border border-[#e5e5e5] bg-white p-7 text-center shadow-[0_1px_1.5px_rgba(0,0,0,0.1)]">
        <h1 className="m-0 mb-2 text-[22px] font-bold text-[#0a0a0a]">
          {error.title}
        </h1>
        <p className="m-0 leading-[1.6] text-[#737373]">{error.message}</p>
      </div>
    </section>
  );
}
