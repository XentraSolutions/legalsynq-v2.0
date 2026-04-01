'use client';

/**
 * InviteUserForm — client-side form for /tenant-users/invite.
 *
 * Props:
 *   resolvedTenantId   — if set, the tenant is pre-resolved (TenantAdmin
 *                        context or selected tenant). The field is hidden and
 *                        the value is submitted silently.
 *   resolvedTenantName — human-readable tenant name shown in the locked badge.
 */

import { useState, FormEvent } from 'react';
import Link                    from 'next/link';
import { useRouter }           from 'next/navigation';
import { Routes }              from '@/lib/routes';

interface Props {
  resolvedTenantId?:   string;
  resolvedTenantName?: string;
}

interface FormState {
  email:      string;
  firstName:  string;
  lastName:   string;
  tenantId:   string;
  memberRole: string;
}

export function InviteUserForm({ resolvedTenantId, resolvedTenantName }: Props) {
  const router = useRouter();

  const [form, setForm]       = useState<FormState>({
    email:      '',
    firstName:  '',
    lastName:   '',
    tenantId:   resolvedTenantId ?? '',
    memberRole: 'Member',
  });
  const [pending, setPending] = useState(false);
  const [error, setError]     = useState<string | null>(null);

  function handleChange(e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) {
    const { name, value } = e.target;
    setForm(prev => ({ ...prev, [name]: value }));
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setPending(true);

    try {
      const res = await fetch('/api/identity/admin/users/invite', {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify({
          email:      form.email.trim(),
          firstName:  form.firstName.trim(),
          lastName:   form.lastName.trim(),
          tenantId:   form.tenantId.trim(),
          memberRole: form.memberRole || undefined,
        }),
      });

      if (!res.ok) {
        const body = await res.json().catch(() => ({})) as { message?: string };
        throw new Error(body.message ?? 'Failed to send invitation.');
      }

      router.push(Routes.tenantUsers);
      router.refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'An unexpected error occurred.');
    } finally {
      setPending(false);
    }
  }

  const tenantIsLocked = Boolean(resolvedTenantId);

  return (
    <div className="min-h-screen bg-gray-50 flex items-start justify-center pt-20 px-4">
      <div className="w-full max-w-md">

        {/* Card */}
        <div className="bg-white border border-gray-200 rounded-xl shadow-sm overflow-hidden">

          {/* Header */}
          <div className="px-6 py-5 border-b border-gray-100">
            <div className="flex items-center justify-between">
              <div>
                <h1 className="text-lg font-semibold text-gray-900">Invite User</h1>
                <p className="text-sm text-gray-500 mt-0.5">
                  Send an invitation to a new platform user.
                </p>
              </div>
              <Link
                href={Routes.tenantUsers}
                className="text-sm text-gray-400 hover:text-gray-700 transition-colors"
              >
                Cancel
              </Link>
            </div>
          </div>

          {/* Form */}
          <form onSubmit={handleSubmit} className="px-6 py-5 space-y-4">

            {error && (
              <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
                {error}
              </div>
            )}

            <Field label="First Name" required>
              <input
                type="text"
                name="firstName"
                value={form.firstName}
                onChange={handleChange}
                required
                placeholder="Jane"
                className={inputClass}
              />
            </Field>

            <Field label="Last Name" required>
              <input
                type="text"
                name="lastName"
                value={form.lastName}
                onChange={handleChange}
                required
                placeholder="Smith"
                className={inputClass}
              />
            </Field>

            <Field label="Email Address" required>
              <input
                type="email"
                name="email"
                value={form.email}
                onChange={handleChange}
                required
                placeholder="jane@example.com"
                className={inputClass}
              />
            </Field>

            {/* Tenant — locked (auto-resolved) or manual UUID entry */}
            {tenantIsLocked ? (
              <Field label="Tenant">
                <div className="flex items-center gap-2 px-3 py-1.5 border border-gray-200 rounded-md bg-gray-50">
                  <span className="h-2 w-2 rounded-full bg-amber-400 flex-shrink-0" />
                  <span className="text-sm text-gray-700 font-medium truncate">
                    {resolvedTenantName ?? resolvedTenantId}
                  </span>
                  <span className="ml-auto text-[10px] font-semibold uppercase tracking-wide text-gray-400">
                    auto
                  </span>
                </div>
                <p className="mt-1 text-xs text-gray-400">
                  Tenant resolved from your active session context.
                </p>
                {/* Hidden field carries the value for submit */}
                <input type="hidden" name="tenantId" value={resolvedTenantId} />
              </Field>
            ) : (
              <Field label="Tenant ID" required>
                <input
                  type="text"
                  name="tenantId"
                  value={form.tenantId}
                  onChange={handleChange}
                  required
                  placeholder="Tenant UUID"
                  className={`${inputClass} font-mono text-xs`}
                />
                <p className="mt-1 text-xs text-gray-400">
                  The UUID of the tenant this user belongs to.
                </p>
              </Field>
            )}

            <Field label="Member Role">
              <select
                name="memberRole"
                value={form.memberRole}
                onChange={handleChange}
                className={inputClass}
              >
                <option value="Member">Member</option>
                <option value="Admin">Admin</option>
                <option value="Owner">Owner</option>
                <option value="Viewer">Viewer</option>
              </select>
            </Field>

            <div className="pt-2">
              <button
                type="submit"
                disabled={pending}
                className="w-full bg-indigo-600 text-white text-sm font-medium px-4 py-2.5 rounded-lg hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-1"
              >
                {pending ? (
                  <span className="flex items-center justify-center gap-2">
                    <span className="h-4 w-4 rounded-full border-2 border-white/40 border-t-white animate-spin" />
                    Sending invitation…
                  </span>
                ) : (
                  'Send Invitation'
                )}
              </button>
            </div>
          </form>
        </div>

        {/* Back link */}
        <p className="text-center mt-4">
          <Link href={Routes.tenantUsers} className="text-sm text-gray-400 hover:text-gray-700 underline transition-colors">
            ← Back to Tenant Users
          </Link>
        </p>
      </div>
    </div>
  );
}

const inputClass =
  'w-full text-sm border border-gray-200 rounded-md px-3 py-1.5 text-gray-900 placeholder-gray-400 bg-white focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400';

function Field({
  label,
  required,
  children,
}: {
  label:     string;
  required?: boolean;
  children:  React.ReactNode;
}) {
  return (
    <div>
      <label className="block text-xs font-medium text-gray-700 mb-1">
        {label}
        {required && <span className="text-red-500 ml-0.5">*</span>}
      </label>
      {children}
    </div>
  );
}
