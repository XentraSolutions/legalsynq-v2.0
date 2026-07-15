import { requirePlatformAdmin } from '@/lib/auth-guards';
import { CCShell } from '@/components/shell/cc-shell';
import { PRODUCT_CATALOG } from '@/lib/product-catalog';

const XENIA_PRODUCT =
  PRODUCT_CATALOG.find(product => product.code === 'Xenia') ?? {
    name: 'Xenia',
    iconSrc: '/product-icons/synqai.png',
    description: 'Tenant-aware AI assistant and agent platform',
  };

export const metadata = {
  title: 'Xenia — LegalSynq Control Center',
  description: XENIA_PRODUCT.description,
};

export default async function XeniaLayout({ children }: { children: React.ReactNode }) {
  const session = await requirePlatformAdmin();

  return (
    <CCShell userEmail={session.email}>
      <div className="min-h-full bg-gray-50">
        <div className="border-b border-gray-200 bg-white">
          <div className="max-w-5xl mx-auto px-6 py-4">
            <div className="flex items-center gap-3">
              <div className="flex h-10 w-10 items-center justify-center rounded-lg border border-amber-100 bg-amber-50">
                <img
                  src={XENIA_PRODUCT.iconSrc}
                  alt=""
                  aria-hidden
                  className="h-5 w-5 object-contain"
                />
              </div>
              <div>
                <h1 className="text-lg font-semibold text-gray-900">{XENIA_PRODUCT.name}</h1>
                <p className="text-xs text-gray-500">{XENIA_PRODUCT.description}</p>
              </div>
            </div>
            <nav className="flex gap-6 mt-4 border-t border-gray-100 pt-3">
              <NavLink href="/xenia/settings" label="Xenia Assistant Settings" />
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
