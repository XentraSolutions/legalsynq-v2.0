import { LoginForm } from './login-form';

interface LoginPageProps {
  searchParams: { reason?: string };
}

export default function LoginPage({ searchParams }: LoginPageProps) {
  const reason = searchParams.reason;

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50 px-4">
      <div className="w-full max-w-sm space-y-6">
        <div className="text-center space-y-1">
          <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-md bg-indigo-50 border border-indigo-200 mb-3">
            <span className="text-xs font-semibold text-indigo-700 tracking-wide uppercase">
              Control Center
            </span>
          </div>
          <h1 className="text-2xl font-bold text-gray-900">Sign in</h1>
          <p className="text-sm text-gray-500">Platform administration access only</p>
        </div>

        {reason === 'unauthorized' && (
          <div className="bg-amber-50 border border-amber-200 rounded-lg px-4 py-3 text-sm text-amber-800">
            <strong>Access denied.</strong> This portal is restricted to LegalSynq platform administrators.
            Tenant users should sign in through their organisation&apos;s portal instead.
          </div>
        )}

        <LoginForm />
      </div>
    </div>
  );
}
