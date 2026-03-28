'use client';

import { useState, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import { ApiError } from '@/lib/api-client';

/**
 * Login form — calls POST /api/identity/api/auth/login.
 * The gateway resolves tenantCode from the subdomain (Host header).
 * In local dev (NEXT_PUBLIC_ENV=development), a tenantCode field is shown.
 */
export function LoginForm() {
  const router   = useRouter();
  const isDev    = process.env.NEXT_PUBLIC_ENV === 'development';

  const [email,      setEmail]      = useState('');
  const [password,   setPassword]   = useState('');
  const [tenantCode, setTenantCode] = useState('');
  const [error,      setError]      = useState<string | null>(null);
  const [loading,    setLoading]    = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const body: Record<string, string> = { email, password };
      if (isDev && tenantCode) body.tenantCode = tenantCode;

      const res = await fetch('/api/identity/api/auth/login', {
        method:      'POST',
        credentials: 'include',
        headers:     { 'Content-Type': 'application/json' },
        body:        JSON.stringify(body),
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        setError(err.message ?? 'Invalid credentials');
        return;
      }

      // Session cookie is set by the Identity service response.
      // Navigate to dashboard — SessionProvider will pick up the new session.
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
