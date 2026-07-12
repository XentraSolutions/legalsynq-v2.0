import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import { getEmailProviders, type EmailProviderDefinition } from '@/lib/xenia-email-api';

export const dynamic = 'force-dynamic';

export default async function EmailProvidersPage() {
  await requirePlatformAdmin();

  const jar = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';

  let providers: EmailProviderDefinition[] = [];
  let error = false;

  try {
    const result = await getEmailProviders(token);
    providers = result.providers;
  } catch {
    error = true;
  }

  const categoryColor = (cat: string) => {
    switch (cat) {
      case 'Cloud': return 'bg-blue-100 text-blue-800';
      case 'Protocol': return 'bg-purple-100 text-purple-800';
      case 'Enterprise': return 'bg-indigo-100 text-indigo-800';
      default: return 'bg-gray-100 text-gray-600';
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-xl font-semibold text-gray-900">Email Providers</h2>
          <p className="text-sm text-gray-500 mt-1">
            Supported email provider types and their capabilities.
          </p>
        </div>
        <a
          href="/xenia/email"
          className="inline-flex items-center gap-1 rounded-md border border-gray-200 bg-white px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50"
        >
          ← Email Dashboard
        </a>
      </div>

      {error ? (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Unable to reach the Xenia service. Ensure it is running on port 5035.
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4">
          {providers.map(p => (
            <div key={p.providerKey} className="rounded-lg border border-gray-200 bg-white p-5">
              <div className="flex items-start justify-between">
                <div>
                  <div className="flex items-center gap-2">
                    <h3 className="text-sm font-semibold text-gray-900">{p.displayName}</h3>
                    <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${categoryColor(p.category)}`}>
                      {p.category}
                    </span>
                    {p.requiresTls && (
                      <span className="inline-flex rounded-full px-2 py-0.5 text-xs font-medium bg-green-100 text-green-800">
                        TLS required
                      </span>
                    )}
                  </div>
                  <p className="text-xs text-gray-500 mt-1 font-mono">{p.providerKey}</p>
                </div>
                <div className="flex gap-2">
                  {p.supportsOAuth && (
                    <span className="inline-flex items-center gap-1 text-xs text-indigo-600 bg-indigo-50 rounded-full px-2 py-0.5">
                      OAuth2
                    </span>
                  )}
                  {p.validationAvailable && (
                    <span className="inline-flex items-center gap-1 text-xs text-green-600 bg-green-50 rounded-full px-2 py-0.5">
                      Validation
                    </span>
                  )}
                </div>
              </div>

              {p.helpText && (
                <p className="text-xs text-gray-600 mt-2 leading-relaxed">{p.helpText}</p>
              )}

              <div className="mt-3 flex flex-wrap gap-3 text-xs text-gray-500">
                {p.defaultIncomingHost && (
                  <span className="font-mono bg-gray-50 border border-gray-100 rounded px-1.5 py-0.5">
                    {p.defaultIncomingHost}:{p.defaultPort}
                  </span>
                )}
                <span className="text-gray-400">Auth types:</span>
                {p.supportedAuthTypes.map(a => (
                  <span key={a} className="bg-gray-100 rounded px-1.5 py-0.5 text-gray-600">{a}</span>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
