import dynamic from 'next/dynamic';

const LoginForm = dynamic(
  () => import('./login-form').then(m => ({ default: m.LoginForm })),
  { ssr: false },
);

const CCLink = dynamic(
  () => import('./cc-link').then(m => ({ default: m.CCLink })),
  { ssr: false },
);

export default function LoginPage() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50 px-4">
      <div className="w-full max-w-sm space-y-6">
        <div className="text-center">
          <h1 className="text-2xl font-bold text-gray-900">Sign in</h1>
          <p className="mt-1 text-sm text-gray-500">
            Access the LegalSynq Platform
          </p>
        </div>
        <LoginForm />
        <CCLink />
      </div>
    </div>
  );
}
