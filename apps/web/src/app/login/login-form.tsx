'use client';

import { useState, useEffect, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';

/**
 * Login form — calls the Next.js BFF route POST /api/auth/login.
 *
 * The BFF route:
 *   1. Resolves tenantCode from the Host header (production)
 *      OR accepts it explicitly from the form body (dev mode)
 *   2. Forwards credentials to the Identity service
 *   3. Receives the JWT and sets it as an HttpOnly cookie (platform_session)
 *   4. Returns only the session envelope — the raw token never reaches browser JS
 *
 * In local dev (NEXT_PUBLIC_ENV=development), a tenantCode field is shown
 * because localhost has no subdomain for automatic resolution.
 */
export function LoginForm() {
  const router = useRouter();

  // Defer env-dependent state to after hydration to avoid server/client mismatch.
  const [isDev, setIsDev]           = useState(false);
  const [email,      setEmail]      = useState('');
  const [password,   setPassword]   = useState('');
  const [tenantCode, setTenantCode] = useState('');
  const [error,      setError]      = useState<string | null>(null);
  const [loading,    setLoading]    = useState(false);

  // Runs only on the client — safe to read NEXT_PUBLIC_ env vars here.
  useEffect(() => {
    setIsDev(process.env.NEXT_PUBLIC_ENV === 'development');
    setTenantCode(process.env.NEXT_PUBLIC_TENANT_CODE ?? '');
  }, []);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const body: Record<string, string> = { email, password };
      if (tenantCode) body.tenantCode = tenantCode;

      // POST to the Next.js BFF — NOT the gateway directly.
      // The BFF sets the platform_session HttpOnly cookie and returns the session envelope.
      const res = await fetch('/api/auth/login', {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify(body),
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        setError(err.message ?? 'Invalid credentials');
        return;
      }

      // Cookie is already set by the BFF response (HttpOnly — browser JS cannot read it).
      // Navigate to dashboard — SessionProvider will call /api/auth/me and pick up the session.
      router.push('/dashboard');
    } catch {
      setError('Network error. Please try again.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="bg-white rounded-xl shadow-sm border border-gray-200 p-6 space-y-4">
      {isDev && (
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Tenant Code <span className="text-gray-400 font-normal">(dev only)</span>
          </label>
          <input
            type="text"
            value={tenantCode}
            onChange={e => setTenantCode(e.target.value)}
            placeholder="e.g. lawfirm-alpha"
            className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
          />
        </div>
      )}

      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">Email</label>
        <input
          type="email"
          required
          value={email}
          onChange={e => setEmail(e.target.value)}
          autoComplete="email"
          className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
        />
      </div>

      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">Password</label>
        <input
          type="password"
          required
          value={password}
          onChange={e => setPassword(e.target.value)}
          autoComplete="current-password"
          className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
        />
      </div>

      {error && (
        <p className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-md px-3 py-2">
          {error}
        </p>
      )}

      <button
        type="submit"
        disabled={loading}
        className="w-full bg-primary text-white rounded-md px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-60 transition-opacity"
      >
        {loading ? 'Signing in…' : 'Sign in'}
      </button>
    </form>
  );
}
