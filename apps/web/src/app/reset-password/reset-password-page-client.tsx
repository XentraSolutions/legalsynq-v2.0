'use client';

import { Suspense } from 'react';
import Image from 'next/image';
import { ResetPasswordForm } from './reset-password-form';

interface ResetPasswordPageClientProps {
  portalProductId: 'careconnect' | 'synqlien' | null;
  loginHref: string;
}

export function ResetPasswordPageClient({ portalProductId, loginHref }: ResetPasswordPageClientProps) {
  if (portalProductId === 'careconnect') {
    return <CareConnectResetPasswordLayout loginHref={loginHref} />;
  }

  if (portalProductId === 'synqlien') {
    return <SynqLienResetPasswordLayout loginHref={loginHref} />;
  }

  return <LegalSynqResetPasswordLayout loginHref={loginHref} />;
}

function CareConnectResetPasswordLayout({ loginHref }: { loginHref: string }) {
  const bg = '#0c4a6e';
  const accent = '#38bdf8';

  return (
    <div className="min-h-screen flex flex-col lg:flex-row">
      <div
        className="hidden lg:flex lg:w-[45%] xl:w-[42%] flex-col p-10 xl:p-14 relative overflow-hidden"
        style={{ backgroundColor: bg }}
      >
        <div
          className="absolute -bottom-40 -left-40 w-[520px] h-[520px] rounded-full opacity-[0.05]"
          style={{ border: `80px solid ${accent}` }}
          aria-hidden
        />
        <div
          className="absolute -top-24 -right-24 w-[320px] h-[320px] rounded-full opacity-[0.04]"
          style={{ border: `60px solid ${accent}` }}
          aria-hidden
        />

        <div className="relative z-10 mb-auto">
          <Image
            src="/careconnect-logo.png"
            alt="CareConnect"
            width={300}
            height={66}
            priority
            unoptimized
            className="w-full max-w-[300px] h-auto"
          />
        </div>

        <div className="relative z-10 py-12">
          <div
            className="w-10 h-0.5 mb-6 rounded-full"
            style={{ backgroundColor: accent }}
          />
          <h2 className="text-3xl xl:text-4xl font-bold text-white leading-tight tracking-tight mb-4">
            Set your new password
          </h2>
          <p className="text-[15px] leading-relaxed max-w-xs" style={{ color: 'rgba(186,230,253,0.8)' }}>
            Choose a strong password to restore secure access to your CareConnect account
          </p>
        </div>

        <div className="relative z-10 pt-6 border-t" style={{ borderColor: 'rgba(255,255,255,0.08)' }}>
          <div className="flex items-center gap-3">
            <p className="text-[11px]" style={{ color: 'rgba(186,230,253,0.4)' }}>
              &copy; {new Date().getFullYear()} LegalSynq CareConnect
            </p>
          </div>
        </div>
      </div>

      <div className="flex-1 flex flex-col items-center justify-center min-h-screen lg:min-h-0 px-6 py-12 bg-gray-50">
        <div className="w-full max-w-sm">
          <div className="mb-8">
            <h1 className="text-2xl font-bold text-gray-900 tracking-tight">Set new password</h1>
            <p className="mt-1.5 text-sm text-gray-500">
              Enter your new password below
            </p>
          </div>

          <Suspense fallback={null}>
            <ResetPasswordForm loginHref={loginHref} />
          </Suspense>
        </div>
      </div>
    </div>
  );
}

function SynqLienResetPasswordLayout({ loginHref }: { loginHref: string }) {
  const bg = '#111827';
  const accent = '#f97316';

  return (
    <div className="min-h-screen flex flex-col lg:flex-row">
      <div
        className="hidden lg:flex lg:w-[45%] xl:w-[42%] flex-col p-10 xl:p-14 relative overflow-hidden"
        style={{ backgroundColor: bg }}
      >
        <div
          className="absolute -bottom-40 -left-40 w-[520px] h-[520px] rounded-full opacity-[0.05]"
          style={{ border: `80px solid ${accent}` }}
          aria-hidden
        />
        <div
          className="absolute -top-24 -right-24 w-[320px] h-[320px] rounded-full opacity-[0.04]"
          style={{ border: `60px solid ${accent}` }}
          aria-hidden
        />

        <div className="relative z-10 mb-auto">
          <SynqLienLogo testId="sl-reset-desktop-logo" />
        </div>

        <div className="relative z-10 py-12">
          <div
            className="w-10 h-0.5 mb-6 rounded-full"
            style={{ backgroundColor: accent }}
          />
          <h2 className="text-3xl xl:text-4xl font-bold text-white leading-tight tracking-tight mb-4">
            Set your new password
          </h2>
          <p className="text-[15px] leading-relaxed max-w-sm" style={{ color: 'rgba(226,232,240,0.78)' }}>
            Choose a strong password to restore secure access to your SynqLien portal
          </p>
        </div>

        <div className="relative z-10 pt-6 border-t" style={{ borderColor: 'rgba(255,255,255,0.08)' }}>
          <div className="flex items-center gap-3">
            <p className="text-[11px]" style={{ color: 'rgba(226,232,240,0.42)' }}>
              &copy; {new Date().getFullYear()} LegalSynq SynqLien
            </p>
          </div>
        </div>
      </div>

      <div className="flex-1 flex flex-col items-center justify-center min-h-screen lg:min-h-0 px-6 py-12 bg-gray-50">
        <div className="lg:hidden mb-10">
          <SynqLienLogo compact dark testId="sl-reset-mobile-logo" />
        </div>

        <div className="w-full max-w-sm">
          <div className="mb-8">
            <h1 className="text-2xl font-bold text-gray-900 tracking-tight">Set new password</h1>
            <p className="mt-1.5 text-sm text-gray-500">
              Enter your new password below
            </p>
          </div>

          <Suspense fallback={null}>
            <ResetPasswordForm loginHref={loginHref} />
          </Suspense>
        </div>
      </div>
    </div>
  );
}

function LegalSynqResetPasswordLayout({ loginHref }: { loginHref: string }) {
  return (
    <div className="min-h-screen flex flex-col lg:flex-row">
      <div
        className="hidden lg:flex lg:w-[45%] xl:w-[42%] flex-col p-10 xl:p-14 relative overflow-hidden"
        style={{ backgroundColor: '#0f1928' }}
      >
        <div
          className="absolute -bottom-40 -left-40 w-[520px] h-[520px] rounded-full opacity-[0.04]"
          style={{ border: '80px solid #f97316' }}
          aria-hidden
        />
        <div
          className="absolute -top-24 -right-24 w-[320px] h-[320px] rounded-full opacity-[0.03]"
          style={{ border: '60px solid #f97316' }}
          aria-hidden
        />

        <div className="relative z-10 mb-auto">
          <Image
            src="/legalsynq-logo-white.png"
            alt="LegalSynq"
            width={220}
            height={52}
            priority
            unoptimized
            className="h-12 w-auto"
          />
        </div>

        <div className="relative z-10 py-12">
          <div
            className="w-10 h-0.5 mb-6 rounded-full"
            style={{ backgroundColor: '#f97316' }}
          />
          <h2 className="text-3xl xl:text-4xl font-bold text-white leading-tight tracking-tight mb-4">
            Set your new password
          </h2>
          <p className="text-[15px] text-slate-400 leading-relaxed max-w-xs">
            Choose a strong password to keep your account secure
          </p>
        </div>

        <div className="relative z-10 pt-6 border-t" style={{ borderColor: 'rgba(255,255,255,0.08)' }}>
          <div className="flex items-center gap-3">
            <p className="text-[11px] text-slate-500">
              &copy; {new Date().getFullYear()} LegalSynq
            </p>
          </div>
        </div>
      </div>

      <div className="flex-1 flex flex-col items-center justify-center min-h-screen lg:min-h-0 px-6 py-12 bg-gray-50">
        <div className="lg:hidden mb-10">
          <Image
            src="/legalsynq-logo.png"
            alt="LegalSynq"
            width={140}
            height={34}
            priority
            unoptimized
            className="h-8 w-auto mx-auto"
          />
        </div>

        <div className="w-full max-w-sm">
          <div className="mb-8">
            <h1 className="text-2xl font-bold text-gray-900 tracking-tight">Set new password</h1>
            <p className="mt-1.5 text-sm text-gray-500">
              Enter your new password below
            </p>
          </div>

          <Suspense fallback={null}>
            <ResetPasswordForm loginHref={loginHref} />
          </Suspense>
        </div>
      </div>
    </div>
  );
}

function SynqLienLogo({
  compact = false,
  dark = false,
  testId,
}: {
  compact?: boolean;
  dark?: boolean;
  testId: string;
}) {
  return (
    <div className="flex items-center gap-3" data-testid={testId}>
      <Image
        src="/product-icons/synqlien.png"
        alt=""
        width={64}
        height={64}
        priority
        unoptimized
        className={compact ? "h-9 w-9 rounded-lg" : "h-14 w-14 rounded-xl"}
      />
      <span
        className={
          compact
            ? "text-2xl font-semibold tracking-tight"
            : "text-[34px] font-semibold tracking-tight"
        }
        style={{ color: dark ? "#111827" : "#ffffff" }}
      >
        Synq<span style={{ color: "#f97316" }}>Lien</span>
      </span>
    </div>
  );
}
