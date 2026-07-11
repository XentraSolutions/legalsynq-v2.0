import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import { getEmailSource, type EmailSource } from '@/lib/xenia-email-api';
import { notFound } from 'next/navigation';

export const dynamic = 'force-dynamic';

export default async function EditEmailSourcePage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  await requirePlatformAdmin();

  const { id } = await params;
  const jar = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';

  let source: EmailSource | null = null;

  try {
    source = await getEmailSource(token, id);
  } catch {
    notFound();
  }

  if (!source) notFound();

  const isProtocol = ['Imap', 'Pop3', 'ExchangeImap'].includes(source.providerType);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <a
            href={`/xenia/email/sources/${id}`}
            className="text-xs text-gray-400 hover:text-gray-600"
          >
            ← {source.displayName}
          </a>
          <h2 className="text-xl font-semibold text-gray-900 mt-1">Edit Email Source</h2>
        </div>
      </div>

      <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
        <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
          <h3 className="text-sm font-semibold text-gray-700">Source Details</h3>
          <p className="text-xs text-gray-500 mt-0.5">
            Provider type and auth type cannot be changed after creation.
            To change them, delete this source and create a new one.
          </p>
        </div>

        {/* Read-only info */}
        <div className="px-4 py-3 space-y-1 border-b border-gray-100 bg-gray-50/50">
          <div className="flex gap-3 text-xs text-gray-500">
            <span>Provider: <strong className="text-gray-700">{source.providerType}</strong></span>
            <span>·</span>
            <span>Auth: <strong className="text-gray-700">{source.authType}</strong></span>
            <span>·</span>
            <span>Email: <strong className="text-gray-700">{source.emailAddress}</strong></span>
          </div>
        </div>

        <form
          action={`/api/xenia/email/sources/${id}`}
          method="POST"
          className="px-4 py-4 space-y-4"
        >
          <input type="hidden" name="_method" value="PUT" />
          <input type="hidden" name="expectedRowVersion" value={source.rowVersion} />

          <FormField
            label="Display Name"
            name="displayName"
            type="text"
            defaultValue={source.displayName}
            required
            maxLength={200}
            hint="Human-readable name for this source. Visible to tenant admins."
          />

          <FormField
            label="Description"
            name="description"
            type="textarea"
            defaultValue={source.description ?? ''}
            maxLength={1000}
            hint="Optional description. Not exposed externally."
          />

          {isProtocol && (
            <>
              <FormField
                label="Incoming Host"
                name="incomingHost"
                type="text"
                defaultValue={source.incomingHost ?? ''}
                maxLength={255}
                hint="IMAP/POP3/Exchange server hostname. Must be a publicly routable address — private ranges are blocked by SSRF policy."
              />
              <FormField
                label="Incoming Port"
                name="incomingPort"
                type="number"
                defaultValue={source.incomingPort?.toString() ?? ''}
                hint="TCP port. Must be in the tenant's allowed port list (default: 993, 995, 443)."
              />
              <CheckboxField
                label="Require TLS"
                name="useTls"
                defaultChecked={source.useTls}
                hint="When enabled, the connection must use TLS. Cannot be disabled when global RequireTls is set."
              />
              <FormField
                label="Mailbox Folder"
                name="mailboxFolder"
                type="text"
                defaultValue={source.mailboxFolder ?? ''}
                maxLength={255}
                hint="Target folder/label (e.g. INBOX). Leave blank for the default inbox."
              />
              <FormField
                label="Username"
                name="username"
                type="text"
                defaultValue={source.username ?? ''}
                maxLength={255}
                hint="Non-secret login username, if different from the email address."
              />
            </>
          )}

          <div className="border-t border-gray-100 pt-4">
            <h4 className="text-xs font-semibold text-gray-700 uppercase tracking-wide mb-3">
              Credential Reference
            </h4>
            <p className="text-xs text-amber-700 bg-amber-50 border border-amber-100 rounded px-3 py-2 mb-3">
              Never enter passwords, tokens, or API keys here.
              Provide only opaque secret reference IDs from the platform secret service.
            </p>
            <FormField
              label="Secret Reference ID"
              name="secretReferenceId"
              type="text"
              defaultValue=""
              maxLength={500}
              hint="Leave blank to keep the existing reference. Enter a new reference ID to replace it."
            />
            <FormField
              label="OAuth Connection Reference"
              name="oauthConnectionRef"
              type="text"
              defaultValue=""
              maxLength={500}
              hint="Leave blank to keep the existing OAuth connection. OAuth sources only."
            />
          </div>

          <div className="border-t border-gray-100 pt-4 flex justify-end gap-3">
            <a
              href={`/xenia/email/sources/${id}`}
              className="inline-flex items-center rounded-md border border-gray-200 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
            >
              Cancel
            </a>
            <button
              type="submit"
              className="inline-flex items-center rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700"
            >
              Save Changes
            </button>
          </div>
        </form>
      </div>

      <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
        <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
          <h3 className="text-sm font-semibold text-gray-700">Actions</h3>
        </div>
        <div className="px-4 py-3 space-y-2">
          <p className="text-xs text-gray-500">
            Use the API or tenant portal to enable/disable, run validation, or delete this source.
          </p>
          <div className="flex gap-2 mt-2">
            <a
              href={`/xenia/email/sources/${id}`}
              className="inline-flex items-center rounded-md border border-gray-200 bg-white px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50"
            >
              View Details
            </a>
          </div>
        </div>
      </div>
    </div>
  );
}

function FormField({
  label,
  name,
  type,
  defaultValue,
  required,
  maxLength,
  hint,
}: {
  label: string;
  name: string;
  type: 'text' | 'number' | 'textarea';
  defaultValue: string;
  required?: boolean;
  maxLength?: number;
  hint?: string;
}) {
  return (
    <div>
      <label htmlFor={name} className="block text-xs font-medium text-gray-700 mb-1">
        {label}
        {required && <span className="text-red-500 ml-0.5">*</span>}
      </label>
      {type === 'textarea' ? (
        <textarea
          id={name}
          name={name}
          defaultValue={defaultValue}
          maxLength={maxLength}
          rows={3}
          className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
        />
      ) : (
        <input
          id={name}
          name={name}
          type={type}
          defaultValue={defaultValue}
          required={required}
          maxLength={maxLength}
          className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
        />
      )}
      {hint && <p className="mt-1 text-xs text-gray-400">{hint}</p>}
    </div>
  );
}

function CheckboxField({
  label,
  name,
  defaultChecked,
  hint,
}: {
  label: string;
  name: string;
  defaultChecked: boolean;
  hint?: string;
}) {
  return (
    <div>
      <div className="flex items-center gap-2">
        <input
          id={name}
          name={name}
          type="checkbox"
          defaultChecked={defaultChecked}
          value="true"
          className="h-4 w-4 rounded border-gray-300 text-indigo-600"
        />
        <label htmlFor={name} className="text-sm font-medium text-gray-700">
          {label}
        </label>
      </div>
      {hint && <p className="mt-1 text-xs text-gray-400 ml-6">{hint}</p>}
    </div>
  );
}
