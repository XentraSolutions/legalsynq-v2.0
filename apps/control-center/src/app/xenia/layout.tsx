import { requirePlatformAdmin } from '@/lib/auth-guards';
import { CCShell } from '@/components/shell/cc-shell';

export const metadata = {
  title: 'Xenia — LegalSynq Control Center',
  description: 'Xenia Automation Platform administration',
};

export default async function XeniaLayout({ children }: { children: React.ReactNode }) {
  const session = await requirePlatformAdmin();

  return (
    <CCShell userEmail={session.email}>
      <div className="min-h-full bg-gray-50">
        <div className="border-b border-gray-200 bg-white">
          <div className="max-w-5xl mx-auto px-6 py-4">
            <div className="flex items-center gap-3">
              <div className="h-8 w-8 rounded bg-indigo-600 flex items-center justify-center">
                <span className="text-white text-xs font-bold">X</span>
              </div>
              <div>
                <h1 className="text-lg font-semibold text-gray-900">Xenia</h1>
                <p className="text-xs text-gray-500">Automation Platform Administration</p>
              </div>
            </div>
            <nav className="flex gap-6 mt-4 border-t border-gray-100 pt-3">
              <NavLink href="/xenia" label="Dashboard" />
              <NavLink href="/xenia/modules" label="Modules" />
              <NavLink href="/xenia/adapters" label="Adapters" />
              <NavLink href="/xenia/email" label="Email" />
              <NavLink href="/xenia/settings" label="Settings" />
            </nav>
          </div>
        </div>
        <div className="max-w-5xl mx-auto px-6 py-8">
          {children}
        </div>
      </div>
    </CCShell>
  );
}

function NavLink({ href, label }: { href: string; label: string }) {
  return (
    <a
      href={href}
      className="text-sm font-medium text-gray-600 hover:text-indigo-600 transition-colors"
    >
      {label}
    </a>
  );
}
