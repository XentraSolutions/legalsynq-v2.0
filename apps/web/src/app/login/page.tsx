'use client';

import { Suspense, useState, useEffect } from 'react';
import Image from 'next/image';
import { LoginForm } from './login-form';
import { useTenantBranding } from '@/providers/tenant-branding-provider';

export const dynamic = 'force-dynamic';


const HIGHLIGHTS = [
  {
    icon: 'ri-scales-3-line',
    text: 'Coordinate providers, referrals, and case workflows in one place',
  },
  {
    icon: 'ri-hospital-line',
    text: 'Built for law firms, medical providers, and operations teams',
  },
  {
    icon: 'ri-money-dollar-circle-line',
    text: 'Streamline lien management, funding, and settlement tracking',
  },
  {
    icon: 'ri-shield-check-line',
    text: 'Secure, auditable, and operationally efficient',
  },
];

function TenantLogo() {
  const branding = useTenantBranding();

  const sources: string[] = [];
  if (branding.logoDocumentId)
    sources.push(`/api/branding/logo/${branding.logoDocumentId}`);
  if (branding.logoUrl)
    sources.push(branding.logoUrl);
  if (branding.tenantCode)
    sources.push(`/api/branding/logo/public?tenantCode=${branding.tenantCode}`);

  const [srcIndex, setSrcIndex] = useState(0);
  const [exhausted, setExhausted] = useState(false);

  const sourcesKey = sources.join('|');
  useEffect(() => {
    setSrcIndex(0);
    setExhausted(false);
  }, [sourcesKey]);

  if (sources.length === 0 || exhausted) return null;

  function handleError() {
    const next = srcIndex + 1;
    if (next < sources.length) {
      setSrcIndex(next);
    } else {
      setExhausted(true);
    }
  }

  return (
    <div className="mb-6">
      <img
        src={sources[srcIndex]}
        alt={branding.displayName || 'Organization logo'}
        className="max-h-16 max-w-[220px] object-contain"
        onError={handleError}
      />
    </div>
  );
}

export default function LoginPage() {
  const [year, setYear] = useState<number | null>(null);
  useEffect(() => { setYear(new Date().getFullYear()); }, []);

  return (
    <div className="min-h-screen flex flex-col lg:flex-row">

      {/* ── Left panel — branded ──────────────────────────────────────────── */}
      <div
        className="hidden lg:flex lg:w-[45%] xl:w-[42%] flex-col p-10 xl:p-14 relative overflow-hidden"
        style={{ backgroundColor: '#0f1928' }}
      >
        {/* Subtle background texture ring */}
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

        {/* Logo */}
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

        {/* Hero copy */}
        <div className="relative z-10 py-12">
          {/* Orange accent rule */}
          <div
            className="w-10 h-0.5 mb-6 rounded-full"
            style={{ backgroundColor: '#f97316' }}
          />

          <h2 className="text-3xl xl:text-4xl font-bold text-white leading-tight tracking-tight mb-4">
            Synchronizing legal-medical workflows
          </h2>

          <p className="text-[15px] text-slate-400 leading-relaxed mb-10 max-w-xs">
            For law firms, medical providers, lien owners, and case managers
          </p>

          {/* Highlights */}
          <ul className="space-y-5">
            {HIGHLIGHTS.map(({ icon, text }) => (
              <li key={text} className="flex items-start gap-3">
                <span
                  className="shrink-0 w-7 h-7 rounded-lg flex items-center justify-center mt-0.5"
                  style={{ backgroundColor: 'rgba(249,115,22,0.12)' }}
                >
                  <i className={`${icon} text-[14px]`} style={{ color: '#f97316' }} />
                </span>
                <span className="text-[13px] text-slate-300 leading-snug">{text}</span>
              </li>
            ))}
          </ul>
        </div>

        {/* Footer */}
        <div className="relative z-10 pt-6 border-t" style={{ borderColor: 'rgba(255,255,255,0.08)' }}>
          <div className="flex items-center gap-3">
            <p className="text-[11px] text-slate-500" suppressHydrationWarning>
              &copy; {year ?? ''} LegalSynq
            </p>
            <span className="text-slate-700 text-[10px]">&bull;</span>
            <a href="/privacy-policy" className="text-[11px] text-slate-600 hover:text-slate-400 transition-colors">
              Privacy Policy
            </a>
            <span className="text-slate-700 text-[10px]">&bull;</span>
            <a href="/terms" className="text-[11px] text-slate-600 hover:text-slate-400 transition-colors">
              Terms &amp; Conditions
            </a>
          </div>
        </div>
      </div>

      {/* ── Right panel — login form ──────────────────────────────────────── */}
      <div className="flex-1 flex flex-col items-center justify-center min-h-screen lg:min-h-0 px-6 py-12 bg-gray-50">

        {/* Mobile-only logo */}
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
          <TenantLogo />

          {/* Heading */}
          <div className="mb-8">
            <h1 className="text-2xl font-bold text-gray-900 tracking-tight">Welcome back</h1>
            <p className="mt-1.5 text-sm text-gray-500">Sign in to your LegalSynq account</p>
          </div>

          <Suspense fallback={null}>
            <LoginForm />
          </Suspense>

          {/* Footer links */}
          <p className="mt-6 text-center text-xs text-gray-400">
            Need access?{' '}
            <a
              href="mailto:support@legalsynq.com"
              className="text-gray-600 hover:text-gray-900 underline underline-offset-2 transition-colors"
            >
              Contact support
            </a>
          </p>
        </div>
      </div>

    </div>
  );
}
