export const metadata = {
  title: 'Email — Xenia | LegalSynq Control Center',
};

export default function XeniaEmailLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="space-y-0">
      <div className="border-b border-gray-100 bg-white/60 mb-6">
        <nav className="flex gap-5 px-0 py-2">
          <EmailNavLink href="/xenia/email" label="Dashboard" />
          <EmailNavLink href="/xenia/email/sources" label="Sources" />
          <EmailNavLink href="/xenia/email/providers" label="Providers" />
        </nav>
      </div>
      {children}
    </div>
  );
}

function EmailNavLink({ href, label }: { href: string; label: string }) {
  return (
    <a
      href={href}
      className="text-sm text-gray-600 hover:text-indigo-600 transition-colors pb-2 border-b-2 border-transparent hover:border-indigo-300"
    >
      {label}
    </a>
  );
}
