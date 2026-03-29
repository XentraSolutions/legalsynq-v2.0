'use client';

import { useState, useEffect, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';

/**
 * Login form — calls the BFF route POST /api/auth/login.
 * On success, the BFF sets the platform_session HttpOnly cookie
 * and the router navigates to /tenants.
 */
export function LoginForm() {
  const router = useRouter();

  // Defer the dev-mode check to after mount so the first client render
  // matches the server render (both start as false). Without this, if
  // NEXT_PUBLIC_ENV is set in the server environment but not inlined into
  // the client bundle, isDev is true server-side and false client-side,
  // which causes a React hydration mismatch on the Tenant Code field.
  const [mounted, setMounted] = useState(false);
  useEffect(() => setMounted(true), []);
  const isDev = mounted && process.env.NEXT_PUBLIC_ENV === 'development';

  const [email,      setEmail]      = useState('');
  const [password,   setPassword]   = useState('');
  const [tenantCode, setTenantCode] = useState(process.env.NEXT_PUBLIC_TENANT_CODE ?? 'LEGALSYNQ');
  const [error,      setError]      = useState<string | null>(null);
  const [loading,    setLoading]    = useState(false);

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
        setError(err.message ?? 'Invalid credentials');
        return;
      }

      router.push('/tenants');
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
            className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
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
          className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
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
          className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
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
        className="w-full bg-indigo-600 text-white rounded-md px-4 py-2 text-sm font-medium hover:bg-indigo-700 disabled:opacity-60 transition-colors"
      >
        {loading ? 'Signing in…' : 'Sign in'}
      </button>
    </form>
  );
}
