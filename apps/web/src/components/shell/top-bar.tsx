'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { Heart, Banknote, FileStack, Settings, LogOut, ExternalLink } from 'lucide-react';
import { useSession } from '@/hooks/use-session';
import { useTenantBranding } from '@/hooks/use-tenant-branding';
import { useProduct } from '@/contexts/product-context';
import { orgTypeLabel } from '@/lib/nav';
import type { NavGroup } from '@/types';
import type { LucideIcon } from 'lucide-react';

const PRODUCT_ICONS: Record<string, LucideIcon> = {
  careconnect: Heart,
  fund: Banknote,
  lien: FileStack,
  admin: Settings,
};

export function TopBar() {
  const { session, clearSession } = useSession();
  const branding = useTenantBranding();
  const { availableGroups, activeProductId } = useProduct();
  const router = useRouter();

  async function handleSignOut() {
    await fetch('/api/auth/logout', { method: 'POST' });
    clearSession();
    window.location.href = '/login';
  }

  const productGroups = availableGroups.filter(g => g.id !== 'admin');
  const adminGroup    = availableGroups.find(g => g.id === 'admin');

  return (
    <header className="h-12 shrink-0 bg-slate-900 flex items-center px-4 gap-0 z-20">

      {/* ── Branding ──────────────────────────────────────────────────────────── */}
      <div className="flex items-center gap-2 shrink-0 min-w-[160px]">
        {branding.logoUrl ? (
          <img src={branding.logoUrl} alt={branding.displayName} className="h-6 w-auto" />
        ) : (
          <span className="text-sm font-bold text-white tracking-tight">
            {branding.displayName || 'LegalSynq'}
          </span>
        )}
      </div>

      {/* ── Vertical divider ──────────────────────────────────────────────────── */}
      <div className="w-px h-5 bg-slate-700 mx-3 shrink-0" />

      {/* ── Org context ───────────────────────────────────────────────────────── */}
      {session?.hasOrg && (
        <>
          <div className="flex flex-col leading-tight shrink-0 mr-4">
            <span className="text-[10px] font-medium text-slate-400 uppercase tracking-wider">
              {orgTypeLabel(session.orgType)}
            </span>
            <span className="text-xs font-semibold text-slate-200 truncate max-w-[140px]">
              {session.orgName}
            </span>
          </div>
          <div className="w-px h-5 bg-slate-700 mr-3 shrink-0" />
        </>
      )}

      {/* ── Product tabs ──────────────────────────────────────────────────────── */}
      <nav className="flex items-center gap-0.5 flex-1">
        {productGroups.map(group => (
          <ProductTab
            key={group.id}
            group={group}
            isActive={activeProductId === group.id}
          />
        ))}

        {/* Admin tab — shown separately for admins */}
        {adminGroup && (
          <ProductTab
            group={adminGroup}
            isActive={activeProductId === 'admin'}
          />
        )}
      </nav>

      {/* ── Control Center (platform admin only) ──────────────────────────────── */}
      {session?.isPlatformAdmin && (
        <button
          onClick={() => {
            const { protocol, hostname } = window.location;
            window.location.href = `${protocol}//${hostname}:5004`;
          }}
          title="Switch to Control Center"
          className="flex items-center gap-1.5 px-2.5 py-1 rounded-md text-xs font-medium
                     border border-slate-600 text-slate-300 hover:text-white hover:border-slate-400
                     transition-colors shrink-0 mr-3"
        >
          <ExternalLink size={12} />
          <span>Control Center</span>
        </button>
      )}

      {/* ── User menu ─────────────────────────────────────────────────────────── */}
      {session && (
        <div className="flex items-center gap-3 shrink-0 pl-3 border-l border-slate-700">
          <span className="text-xs text-slate-400 max-w-[160px] truncate">{session.email}</span>
          <button
            onClick={handleSignOut}
            title="Sign out"
            className="flex items-center gap-1 text-xs text-slate-400 hover:text-white transition-colors"
          >
            <LogOut size={13} />
            <span>Sign out</span>
          </button>
        </div>
      )}
    </header>
  );
}

// ── Product tab ──────────────────────────────────────────────────────────────

interface ProductTabProps {
  group: NavGroup;
  isActive: boolean;
}

function ProductTab({ group, isActive }: ProductTabProps) {
  const Icon = PRODUCT_ICONS[group.id];
  const defaultHref = group.items[0]?.href ?? '#';

  return (
    <Link
      href={defaultHref}
      className={[
        'flex items-center gap-1.5 px-3 py-1.5 rounded-md text-sm font-medium transition-colors',
        isActive
          ? 'bg-blue-600 text-white'
          : 'text-slate-300 hover:text-white hover:bg-white/10',
      ].join(' ')}
    >
      {Icon && <Icon size={14} strokeWidth={2} />}
      <span>{group.label}</span>
    </Link>
  );
}
