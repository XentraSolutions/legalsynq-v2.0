import { LoginForm } from './login-form';

export default function LoginPage() {
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
        <LoginForm />
      </div>
    </div>
  );
}
