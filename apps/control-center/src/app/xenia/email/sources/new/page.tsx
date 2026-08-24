import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import { getEmailProviders, type EmailProviderDefinition } from '@/lib/xenia-email-api';

export const dynamic = 'force-dynamic';

export default async function NewEmailSourcePage() {
  await requirePlatformAdmin();

  const jar = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';

  let providers: EmailProviderDefinition[] = [];

  try {
    const result = await getEmailProviders(token);
    providers = result.providers;
  } catch {
    // Non-fatal — render static fallback provider list
    providers = [];
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <a
            href="/xenia/email/sources"
            className="text-xs text-gray-400 hover:text-gray-600"
          >
            ← Email Sources
          </a>
          <h2 className="text-xl font-semibold text-gray-900 mt-1">New Email Source</h2>
          <p className="text-sm text-gray-500 mt-0.5">
            Create a tenant-scoped mailbox connection. Credentials are stored by reference only.
          </p>
        </div>
      </div>

      {/* Provider selection */}
      {providers.length > 0 && (
        <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
          <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
            <h3 className="text-sm font-semibold text-gray-700">Available Providers</h3>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3 p-4">
            {providers.map((p) => (
              <div
                key={p.providerKey}
                className="rounded-lg border border-gray-200 p-3 hover:border-indigo-300 hover:bg-indigo-50/50 transition-colors cursor-pointer"
              >
                <div className="flex items-center justify-between">
                  <p className="text-sm font-medium text-gray-900">{p.displayName}</p>
                  <span className="text-xs text-gray-400 bg-gray-100 rounded px-1.5 py-0.5">
                    {p.category}
                  </span>
                </div>
                {p.helpText && (
                  <p className="text-xs text-gray-500 mt-1">{p.helpText}</p>
                )}
                <div className="flex flex-wrap gap-1 mt-2">
                  {p.supportedAuthTypes.map((a) => (
                    <span
                      key={a}
                      className="text-xs bg-blue-50 text-blue-700 rounded px-1.5 py-0.5"
                    >
                      {a}
                    </span>
                  ))}
                </div>
                <div className="flex items-center gap-3 mt-2 text-xs text-gray-400">
                  {p.requiresTls && (
                    <span className="text-green-600">TLS required</span>
                  )}
                  {p.defaultIncomingHost && (
                    <span className="font-mono">{p.defaultIncomingHost}{p.defaultPort ? `:${p.defaultPort}` : ''}</span>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Create form */}
      <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
        <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
          <h3 className="text-sm font-semibold text-gray-700">Source Configuration</h3>
        </div>

        <form action="/api/xenia/email/sources" method="POST" className="px-4 py-4 space-y-4">
          {/* Basics */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label htmlFor="displayName" className="block text-xs font-medium text-gray-700 mb-1">
                Display Name <span className="text-red-500">*</span>
              </label>
              <input
                id="displayName"
                name="displayName"
                type="text"
                required
                maxLength={200}
                placeholder="e.g. Legal Intake Inbox"
                className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              />
            </div>
            <div>
              <label htmlFor="emailAddress" className="block text-xs font-medium text-gray-700 mb-1">
                Email Address <span className="text-red-500">*</span>
              </label>
              <input
                id="emailAddress"
                name="emailAddress"
                type="email"
                required
                maxLength={320}
                placeholder="inbox@yourdomain.com"
                className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              />
            </div>
          </div>

          <div>
            <label htmlFor="description" className="block text-xs font-medium text-gray-700 mb-1">
              Description
            </label>
            <textarea
              id="description"
              name="description"
              maxLength={1000}
              rows={2}
              placeholder="Optional description"
              className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
            />
          </div>

          {/* Provider & Auth */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label htmlFor="providerType" className="block text-xs font-medium text-gray-700 mb-1">
                Provider Type <span className="text-red-500">*</span>
              </label>
              <select
                id="providerType"
                name="providerType"
                required
                className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              >
                <option value="">Select provider…</option>
                <option value="Microsoft365">Microsoft 365</option>
                <option value="GoogleWorkspace">Google Workspace</option>
                <option value="Imap">IMAP</option>
                <option value="Pop3">POP3</option>
                <option value="ExchangeImap">Exchange (IMAP)</option>
              </select>
            </div>
            <div>
              <label htmlFor="authType" className="block text-xs font-medium text-gray-700 mb-1">
                Auth Type <span className="text-red-500">*</span>
              </label>
              <select
                id="authType"
                name="authType"
                required
                className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              >
                <option value="">Select auth type…</option>
                <option value="OAuth2">OAuth 2.0</option>
                <option value="UsernamePassword">Username + Password</option>
                <option value="AppPassword">App Password</option>
                <option value="ClientCredentials">Client Credentials</option>
                <option value="SecretReference">Secret Reference</option>
              </select>
            </div>
          </div>

          {/* Connection details */}
          <div className="border-t border-gray-100 pt-4">
            <h4 className="text-xs font-semibold text-gray-700 uppercase tracking-wide mb-3">
              Connection Details
              <span className="ml-1 text-gray-400 normal-case font-normal">(for IMAP/POP3/Exchange)</span>
            </h4>
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <div className="sm:col-span-2">
                <label htmlFor="incomingHost" className="block text-xs font-medium text-gray-700 mb-1">
                  Incoming Host
                </label>
                <input
                  id="incomingHost"
                  name="incomingHost"
                  type="text"
                  maxLength={255}
                  placeholder="mail.example.com"
                  className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                />
              </div>
              <div>
                <label htmlFor="incomingPort" className="block text-xs font-medium text-gray-700 mb-1">
                  Port
                </label>
                <input
                  id="incomingPort"
                  name="incomingPort"
                  type="number"
                  min={1}
                  max={65535}
                  placeholder="993"
                  className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                />
              </div>
            </div>
            <div className="flex items-center gap-2 mt-3">
              <input
                id="useTls"
                name="useTls"
                type="checkbox"
                defaultChecked
                value="true"
                className="h-4 w-4 rounded border-gray-300 text-indigo-600"
              />
              <label htmlFor="useTls" className="text-sm font-medium text-gray-700">
                Require TLS
              </label>
            </div>
            <div className="mt-3">
              <label htmlFor="mailboxFolder" className="block text-xs font-medium text-gray-700 mb-1">
                Mailbox Folder
              </label>
              <input
                id="mailboxFolder"
                name="mailboxFolder"
                type="text"
                maxLength={255}
                placeholder="INBOX"
                className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              />
            </div>
          </div>

          {/* Credentials */}
          <div className="border-t border-gray-100 pt-4">
            <h4 className="text-xs font-semibold text-gray-700 uppercase tracking-wide mb-2">
              Credential Reference
            </h4>
            <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 mb-3">
              <p className="text-xs text-amber-800 font-medium">No plaintext credentials</p>
              <p className="text-xs text-amber-700 mt-0.5">
                Never paste passwords, tokens, or API keys here. Provide only opaque reference IDs
                registered with the platform secret service.
              </p>
            </div>
            <div className="space-y-3">
              <div>
                <label htmlFor="secretReferenceId" className="block text-xs font-medium text-gray-700 mb-1">
                  Secret Reference ID
                </label>
                <input
                  id="secretReferenceId"
                  name="secretReferenceId"
                  type="text"
                  maxLength={500}
                  placeholder="ref:vault:prod/email/inbox-secret"
                  className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                />
              </div>
              <div>
                <label htmlFor="oauthConnectionRef" className="block text-xs font-medium text-gray-700 mb-1">
                  OAuth Connection Reference
                </label>
                <input
                  id="oauthConnectionRef"
                  name="oauthConnectionRef"
                  type="text"
                  maxLength={500}
                  placeholder="oauth:m365:tenant-abc"
                  className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                />
              </div>
            </div>
          </div>

          <div className="border-t border-gray-100 pt-4 flex items-center justify-between">
            <div className="flex items-center gap-2">
              <input
                id="enabled"
                name="enabled"
                type="checkbox"
                value="true"
                className="h-4 w-4 rounded border-gray-300 text-indigo-600"
              />
              <label htmlFor="enabled" className="text-sm font-medium text-gray-700">
                Enable source immediately
              </label>
            </div>
            <div className="flex gap-3">
              <a
                href="/xenia/email/sources"
                className="inline-flex items-center rounded-md border border-gray-200 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
              >
                Cancel
              </a>
              <button
                type="submit"
                className="inline-flex items-center rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700"
              >
                Create Source
              </button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}
