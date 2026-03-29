'use client';

import { useState, useEffect, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';

/**
 * Login form — calls the Next.js BFF route POST /api/auth/login.
 *
 * isDev is deferred to after mount so the server render and the initial
 * client render always agree (both see isDev = false), eliminating the
 * hydration mismatch caused by NEXT_PUBLIC_ENV being available in the
 * Node.js process but not necessarily inlined in the browser bundle.
 */
export function LoginForm() {
  const router = useRouter();

  const [mounted,    setMounted]    = useState(false);
  useEffect(() => { setMounted(true); }, []);
  const isDev = mounted && process.env.NEXT_PUBLIC_ENV === 'development';

  const [email,      setEmail]      = useState('');
  const [password,   setPassword]   = useState('');
  const [tenantCode, setTenantCode] = useState(process.env.NEXT_PUBLIC_TENANT_CODE ?? '');
  const [error,      setError]      = useState<string | null>(null);
  const [loading,    setLoading]    = useState(false);
  const [showPw,     setShowPw]     = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const body: Record<string, string> = { email, password };
      if (tenantCode) body.tenantCode = tenantCode;

      const res = await fetch('/api/auth/login', {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify(body),
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        setError(err.message ?? 'Invalid credentials. Please try again.');
        return;
      }

      router.push('/dashboard');
    } catch {
      setError('Network error. Please check your connection and try again.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-5" noValidate>

      {/* Dev-only tenant code */}
      {isDev && (
        <Field label="Tenant Code" hint="dev only">
          <input
            type="text"
            value={tenantCode}
            onChange={e => setTenantCode(e.target.value)}
            placeholder="e.g. HARTWELL"
            className={inputCls}
          />
        </Field>
      )}

      {/* Email */}
      <Field label="Email address">
        <input
          type="email"
          required
          value={email}
          onChange={e => setEmail(e.target.value)}
          autoComplete="email"
          placeholder="you@example.com"
          className={inputCls}
        />
      </Field>

      {/* Password */}
      <Field label="Password">
        <div className="relative">
          <input
            type={showPw ? 'text' : 'password'}
            required
            value={password}
            onChange={e => setPassword(e.target.value)}
            autoComplete="current-password"
            placeholder="••••••••"
            className={`${inputCls} pr-10`}
          />
          <button
            type="button"
            tabIndex={-1}
            onClick={() => setShowPw(v => !v)}
            className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 transition-colors"
            aria-label={showPw ? 'Hide password' : 'Show password'}
          >
            <i className={`${showPw ? 'ri-eye-off-line' : 'ri-eye-line'} text-[16px]`} />
          </button>
        </div>
      </Field>

      {/* Error banner */}
      {error && (
        <div className="flex items-start gap-2.5 rounded-lg border border-red-200 bg-red-50 px-3.5 py-3">
          <i className="ri-error-warning-line text-[15px] text-red-500 shrink-0 mt-0.5" />
          <p className="text-[13px] text-red-700 leading-snug">{error}</p>
        </div>
      )}

      {/* Submit */}
      <button
        type="submit"
        disabled={loading}
        className="w-full flex items-center justify-center gap-2 rounded-lg px-4 py-2.5 text-sm font-semibold text-white transition-opacity disabled:opacity-60"
        style={{ backgroundColor: '#f97316' }}
      >
        {loading
          ? <><i className="ri-loader-4-line animate-spin text-[15px]" /> Signing in…</>
          : 'Sign in'
        }
      </button>
    </form>
  );
}

// ── Helpers ───────────────────────────────────────────────────────────────────

const inputCls = [
  'w-full rounded-lg border border-gray-200 bg-white px-3.5 py-2.5 text-sm text-gray-900',
  'placeholder:text-gray-400',
  'focus:outline-none focus:ring-2 focus:border-transparent',
  'transition-shadow',
].join(' ') + ' focus:ring-[#f97316]/40 focus:border-[#f97316]';

function Field({
  label,
  hint,
  children,
}: {
  label: string;
  hint?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-1.5">
      <label className="flex items-center gap-1.5 text-[13px] font-medium text-gray-700">
        {label}
        {hint && (
          <span className="text-[11px] font-normal text-gray-400 bg-gray-100 px-1.5 py-0.5 rounded">
            {hint}
          </span>
        )}
      </label>
      {children}
    </div>
  );
}
