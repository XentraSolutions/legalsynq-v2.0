'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

const tabs = [
  { href: '/xenia/dashboard', label: 'Workspace', icon: 'ri-message-3-line' },
  { href: '/xenia/settings', label: 'Settings', icon: 'ri-settings-4-line' },
];

export function XeniaProductShell({
  eyebrow,
  title,
  description,
  children,
}: {
  eyebrow: string;
  title: string;
  description: string;
  children: React.ReactNode;
}) {
  const pathname = usePathname();

  return (
    <main className="min-h-screen bg-[radial-gradient(circle_at_top_left,_rgba(251,191,36,0.18),_transparent_26%),linear-gradient(180deg,_#fffaf0_0%,_#f8fafc_48%,_#eef2ff_100%)] px-5 py-6 lg:px-8">
      <div className="mx-auto max-w-7xl space-y-6">
        <section className="overflow-hidden rounded-[28px] border border-amber-200/70 bg-white/90 p-8 shadow-lg shadow-amber-100/30">
          <div className="max-w-4xl">
            <div className="inline-flex items-center gap-2 rounded-full border border-amber-200 bg-amber-50 px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.18em] text-amber-700">
              <i className="ri-robot-line" />
              {eyebrow}
            </div>
            <h1 className="mt-4 text-3xl font-semibold tracking-tight text-slate-950">{title}</h1>
            <p className="mt-3 text-sm leading-7 text-slate-600">{description}</p>
          </div>

          <nav className="mt-6 flex flex-wrap gap-3">
            {tabs.map((tab) => {
              const active = pathname === tab.href;
              return (
                <Link
                  key={tab.href}
                  href={tab.href}
                  className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold transition ${
                    active
                      ? 'bg-slate-950 text-white'
                      : 'border border-slate-200 bg-white text-slate-600 hover:border-amber-300 hover:text-amber-700'
                  }`}
                >
                  <i className={tab.icon} />
                  {tab.label}
                </Link>
              );
            })}
          </nav>
        </section>

        {children}
      </div>
    </main>
  );
}
