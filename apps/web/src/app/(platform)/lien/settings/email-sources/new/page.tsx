'use client';

import { useState, useEffect, useRef } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { PageHeader } from '@/components/lien/page-header';
import { useLienStore } from '@/stores/lien-store';
import type { EmailProviderDefinition } from '@/lib/xenia-email-api';

export const dynamic = 'force-dynamic';

const AUTH_TYPES = ['UsernamePassword', 'OAuth2'];

export default function NewEmailSourcePage() {
  const router   = useRouter();
  const addToast = useLienStore((s) => s.addToast);

  const [providers, setProviders]           = useState<EmailProviderDefinition[]>([]);
  const [selectedProvider, setSelectedProvider] = useState<EmailProviderDefinition | null>(null);
  const [submitting, setSubmitting]         = useState(false);
  const formRef = useRef<HTMLFormElement>(null);

  useEffect(() => {
    fetch('/api/xenia/email/providers')
      .then((r) => r.json())
      .then((d) => setProviders(d.providers ?? []))
      .catch(() => {});
  }, []);

  function handleProviderPick(p: EmailProviderDefinition) {
    setSelectedProvider(p);
    if (formRef.current) {
      const f = formRef.current;
      (f.elements.namedItem('providerType') as HTMLInputElement).value = p.providerKey;
      if (p.defaultIncomingHost) {
        (f.elements.namedItem('incomingHost') as HTMLInputElement).value = p.defaultIncomingHost;
      }
      if (p.defaultPort) {
        (f.elements.namedItem('incomingPort') as HTMLInputElement).value = String(p.defaultPort);
      }
      (f.elements.namedItem('useTls') as HTMLInputElement).checked = p.requiresTls;
      if (p.supportedAuthTypes.length === 1) {
        (f.elements.namedItem('authType') as HTMLSelectElement).value = p.supportedAuthTypes[0];
      }
    }
  }

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setSubmitting(true);
    const fd = new FormData(e.currentTarget);

    const payload = {
      displayName:    fd.get('displayName') as string,
      description:    (fd.get('description') as string) || undefined,
      providerType:   fd.get('providerType') as string,
      authType:       fd.get('authType') as string,
      emailAddress:   fd.get('emailAddress') as string,
      username:       (fd.get('username') as string) || undefined,
      incomingHost:   (fd.get('incomingHost') as string) || undefined,
      incomingPort:   fd.get('incomingPort') ? Number(fd.get('incomingPort')) : undefined,
      useTls:         fd.get('useTls') === 'on',
      mailboxFolder:  (fd.get('mailboxFolder') as string) || undefined,
      secretReferenceId: (fd.get('secretReferenceId') as string) || undefined,
      oauthConnectionRef: (fd.get('oauthConnectionRef') as string) || undefined,
      enabled:        fd.get('enabled') === 'on',
    };

    try {
      const res = await fetch('/api/xenia/email/sources', {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify(payload),
      });
      if (!res.ok) {
        const d = await res.json().catch(() => ({}));
        throw new Error(d.error ?? `HTTP ${res.status}`);
      }
      addToast({ type: 'success', title: 'Email source created' });
      router.push('/lien/settings/email-sources');
    } catch (err) {
      addToast({ type: 'error', title: 'Failed to create source', description: err instanceof Error ? err.message : '' });
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Add Email Source"
        subtitle="Connect a mailbox so Xenia can pull incoming email into the platform. Credentials are stored by reference only — never in plain text."
      />

      {providers.length > 0 && (
        <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
          <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
            <p className="text-sm font-semibold text-gray-700">Choose a Provider</p>
            <p className="text-xs text-gray-500 mt-0.5">Selecting a provider pre-fills the server settings below.</p>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3 p-4">
            {providers.map((p) => (
              <button
                key={p.providerKey}
                type="button"
                onClick={() => handleProviderPick(p)}
                className={`rounded-lg border p-3 text-left transition-colors ${
                  selectedProvider?.providerKey === p.providerKey
                    ? 'border-indigo-400 bg-indigo-50'
                    : 'border-gray-200 hover:border-indigo-300 hover:bg-indigo-50/40'
                }`}
              >
                <div className="flex items-center justify-between">
                  <p className="text-sm font-medium text-gray-900">{p.displayName}</p>
                  <span className="text-xs text-gray-400 bg-gray-100 rounded px-1.5 py-0.5">{p.category}</span>
                </div>
                {p.helpText && <p className="text-xs text-gray-500 mt-1">{p.helpText}</p>}
                <div className="flex flex-wrap gap-1 mt-2">
                  {p.supportedAuthTypes.map((a) => (
                    <span key={a} className="text-xs bg-blue-50 text-blue-700 rounded px-1.5 py-0.5">{a}</span>
                  ))}
                </div>
                {(p.requiresTls || p.defaultIncomingHost) && (
                  <div className="flex items-center gap-3 mt-2 text-xs text-gray-400">
                    {p.requiresTls && <span className="text-green-600">TLS required</span>}
                    {p.defaultIncomingHost && (
                      <span className="font-mono">{p.defaultIncomingHost}{p.defaultPort ? `:${p.defaultPort}` : ''}</span>
                    )}
                  </div>
                )}
              </button>
            ))}
          </div>
        </div>
      )}

      <form ref={formRef} onSubmit={handleSubmit} className="space-y-4">
        <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
          <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
            <p className="text-sm font-semibold text-gray-700">Basic Information</p>
          </div>
          <div className="px-4 py-4 grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label htmlFor="displayName" className="block text-xs font-medium text-gray-700 mb-1">
                Display Name <span className="text-red-500">*</span>
              </label>
              <input
                id="displayName" name="displayName" type="text" required maxLength={200}
                placeholder="e.g. Liens Inbox"
                className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              />
            </div>
            <div>
              <label htmlFor="emailAddress" className="block text-xs font-medium text-gray-700 mb-1">
                Email Address <span className="text-red-500">*</span>
              </label>
              <input
                id="emailAddress" name="emailAddress" type="email" required maxLength={320}
                placeholder="inbox@liens-company.com"
                className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              />
            </div>
            <div className="sm:col-span-2">
              <label htmlFor="description" className="block text-xs font-medium text-gray-700 mb-1">Description</label>
              <input
                id="description" name="description" type="text" maxLength={500}
                placeholder="Optional description"
                className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              />
            </div>
          </div>
        </div>

        <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
          <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
            <p className="text-sm font-semibold text-gray-700">Connection Settings</p>
          </div>
          <div className="px-4 py-4 grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label htmlFor="providerType" className="block text-xs font-medium text-gray-700 mb-1">
                Provider Type <span className="text-red-500">*</span>
              </label>
              <input
                id="providerType" name="providerType" type="text" required maxLength={100}
                placeholder="e.g. Imap, MicrosoftGraph, GoogleWorkspace"
                className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              />
            </div>
            <div>
              <label htmlFor="authType" className="block text-xs font-medium text-gray-700 mb-1">
                Auth Type <span className="text-red-500">*</span>
              </label>
              <select
                id="authType" name="authType" required
                className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              >
                {AUTH_TYPES.map((a) => <option key={a} value={a}>{a}</option>)}
              </select>
            </div>
            <div>
              <label htmlFor="incomingHost" className="block text-xs font-medium text-gray-700 mb-1">Incoming Host</label>
              <input
                id="incomingHost" name="incomingHost" type="text" maxLength={253}
                placeholder="mail.example.com"
                className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              />
            </div>
            <div>
              <label htmlFor="incomingPort" className="block text-xs font-medium text-gray-700 mb-1">Port</label>
              <input
                id="incomingPort" name="incomingPort" type="number" min={1} max={65535}
                placeholder="993"
                className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              />
            </div>
            <div>
              <label htmlFor="username" className="block text-xs font-medium text-gray-700 mb-1">Username</label>
              <input
                id="username" name="username" type="text" maxLength={320}
                placeholder="Same as email address if not specified separately"
                className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              />
            </div>
            <div>
              <label htmlFor="mailboxFolder" className="block text-xs font-medium text-gray-700 mb-1">Mailbox Folder</label>
              <input
                id="mailboxFolder" name="mailboxFolder" type="text" maxLength={200}
                placeholder="INBOX"
                className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              />
            </div>
            <div className="flex items-center gap-2 sm:col-span-2 mt-1">
              <input
                id="useTls" name="useTls" type="checkbox" defaultChecked
                className="h-4 w-4 rounded border-gray-300 text-indigo-600"
              />
              <label htmlFor="useTls" className="text-sm text-gray-700 font-medium">Use TLS</label>
            </div>
          </div>
        </div>

        <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
          <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
            <p className="text-sm font-semibold text-gray-700">Credentials</p>
            <p className="text-xs text-gray-500 mt-0.5">
              Provide either a secret reference (recommended) or an OAuth connection reference. Passwords are never stored directly.
            </p>
          </div>
          <div className="px-4 py-4 grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label htmlFor="secretReferenceId" className="block text-xs font-medium text-gray-700 mb-1">
                Secret Reference ID
              </label>
              <input
                id="secretReferenceId" name="secretReferenceId" type="text" maxLength={500}
                placeholder="secret:vault:abc123"
                className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              />
              <p className="mt-1 text-xs text-gray-400">Points to a stored credential in the secrets vault.</p>
            </div>
            <div>
              <label htmlFor="oauthConnectionRef" className="block text-xs font-medium text-gray-700 mb-1">
                OAuth Connection Reference
              </label>
              <input
                id="oauthConnectionRef" name="oauthConnectionRef" type="text" maxLength={500}
                placeholder="oauth:m365:tenant-abc"
                className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              />
              <p className="mt-1 text-xs text-gray-400">For Microsoft 365 / Google Workspace OAuth flows.</p>
            </div>
          </div>
        </div>

        <div className="flex items-center justify-between rounded-lg border border-gray-200 bg-white px-4 py-3">
          <div className="flex items-center gap-2">
            <input
              id="enabled" name="enabled" type="checkbox" defaultChecked
              className="h-4 w-4 rounded border-gray-300 text-indigo-600"
            />
            <label htmlFor="enabled" className="text-sm font-medium text-gray-700">
              Enable source immediately
            </label>
          </div>
          <div className="flex items-center gap-3">
            <Link
              href="/lien/settings/email-sources"
              className="inline-flex items-center rounded-lg border border-gray-200 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
            >
              Cancel
            </Link>
            <button
              type="submit"
              disabled={submitting}
              className="inline-flex items-center gap-1.5 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-60"
            >
              {submitting ? 'Creating…' : 'Create Source'}
            </button>
          </div>
        </div>
      </form>
    </div>
  );
}
