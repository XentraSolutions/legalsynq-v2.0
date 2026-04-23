'use client';

import { useRouter, useSearchParams } from 'next/navigation';
import { Suspense } from 'react';
import Link from 'next/link';
import { getNavGroupModels, getSectionBySlug } from '@/lib/nav-utils';
import type { NavGroupModel } from '@/lib/nav-utils';
import type { NavItem } from '@/types';

// ── Badge styles (mirrors cc-sidebar) ───────────────────────────────────────
const BADGE_STYLES: Record<string, string> = {
  'LIVE':        'bg-emerald-100 text-emerald-700',
  'IN PROGRESS': 'bg-amber-100   text-amber-700',
  'MOCKUP':      'bg-gray-100    text-gray-500',
  'NEW':         'bg-blue-100    text-blue-700',
};

function NavBadge({ badge }: { badge: NonNullable<NavItem['badge']> }) {
  return (
    <span className={`shrink-0 text-[9px] font-semibold px-1.5 py-0.5 rounded-full leading-none ${BADGE_STYLES[badge] ?? BADGE_STYLES['MOCKUP']}`}>
      {badge}
    </span>
  );
}

// ── Group card ───────────────────────────────────────────────────────────────
function GroupCard({
  group,
  isSelected,
  onSelect,
}: {
  group:      NavGroupModel;
  isSelected: boolean;
  onSelect:   () => void;
}) {
  return (
    <button
      onClick={onSelect}
      className={[
        'group text-left w-full rounded-xl border px-4 py-4 transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-400',
        isSelected
          ? 'border-orange-300 bg-orange-50 shadow-sm'
          : 'border-gray-200 bg-white hover:border-gray-300 hover:shadow-sm',
      ].join(' ')}
      aria-pressed={isSelected}
      aria-label={`${group.heading} — ${group.itemCount} tools`}
    >
      <div className="flex items-start gap-3">
        <div
          className={[
            'shrink-0 w-8 h-8 rounded-lg flex items-center justify-center transition-colors',
            isSelected ? 'bg-orange-100' : 'bg-gray-100 group-hover:bg-gray-200',
          ].join(' ')}
        >
          <i
            className={`${group.icon} text-[16px] leading-none`}
            style={{ color: isSelected ? '#f97316' : undefined }}
          />
        </div>
        <div className="min-w-0 flex-1">
          <p
            className={[
              'text-[11px] font-bold uppercase tracking-wider truncate',
              isSelected ? 'text-orange-600' : 'text-gray-500 group-hover:text-gray-700',
            ].join(' ')}
          >
            {group.heading}
          </p>
          <p className="text-[12px] text-gray-400 mt-0.5">
            {group.itemCount} {group.itemCount === 1 ? 'tool' : 'tools'}
            {group.liveCount > 0 && (
              <span className="ml-1.5 text-emerald-600">· {group.liveCount} live</span>
            )}
          </p>
        </div>
      </div>
    </button>
  );
}

// ── Group detail panel ───────────────────────────────────────────────────────
function GroupDetailPanel({ group }: { group: NavGroupModel }) {
  return (
    <div className="mt-5 rounded-xl border border-gray-200 bg-white overflow-hidden">
      {/* Panel header */}
      <div className="flex items-center gap-3 px-5 py-4 border-b border-gray-100 bg-gray-50">
        <div className="w-8 h-8 rounded-lg bg-orange-100 flex items-center justify-center shrink-0">
          <i className={`${group.icon} text-[16px] text-orange-500 leading-none`} />
        </div>
        <div>
          <h2 className="text-sm font-semibold text-gray-900">{group.heading}</h2>
          <p className="text-xs text-gray-400">{group.itemCount} tools in this category</p>
        </div>
      </div>

      {/* Item grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-px bg-gray-100">
        {group.items.map(item => (
          <Link
            key={item.href}
            href={item.href}
            className="group flex items-center gap-3 bg-white px-5 py-4 hover:bg-gray-50 transition-colors focus-visible:outline-none focus-visible:ring-inset focus-visible:ring-2 focus-visible:ring-orange-400"
          >
            <div className="shrink-0 w-8 h-8 rounded-lg bg-gray-100 group-hover:bg-orange-100 flex items-center justify-center transition-colors">
              {item.icon
                ? <i className={`${item.icon} text-[15px] text-gray-500 group-hover:text-orange-500 leading-none transition-colors`} />
                : <span className="w-2 h-2 rounded-full bg-gray-400" />
              }
            </div>
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-1.5 min-w-0">
                <span className="text-sm font-medium text-gray-800 group-hover:text-gray-900 truncate">
                  {item.label}
                </span>
                {item.badge && <NavBadge badge={item.badge} />}
              </div>
              <p className="text-[11px] text-gray-400 truncate mt-0.5">{item.href}</p>
            </div>
            <i className="ri-arrow-right-s-line text-gray-300 group-hover:text-gray-500 shrink-0 transition-colors" />
          </Link>
        ))}
      </div>
    </div>
  );
}

// ── Inner component (requires Suspense) ─────────────────────────────────────
function NavHubInner() {
  const router       = useRouter();
  const searchParams = useSearchParams();
  const selectedSlug = searchParams.get('group');
  const groups       = getNavGroupModels();
  const selectedSection = selectedSlug ? getSectionBySlug(selectedSlug) : undefined;
  const selectedGroup   = selectedSlug
    ? groups.find(g => g.slug === selectedSlug)
    : undefined;

  function handleSelect(slug: string) {
    if (slug === selectedSlug) {
      router.push('/');
    } else {
      router.push(`/?group=${slug}`);
    }
  }

  return (
    <div>
      {/* Section heading */}
      <div className="flex items-center justify-between mb-3">
        <h2 className="text-xs font-semibold uppercase tracking-widest text-gray-400">
          Navigation
        </h2>
        {selectedGroup && (
          <button
            onClick={() => router.push('/')}
            className="text-xs text-gray-400 hover:text-gray-600 flex items-center gap-1 transition-colors"
          >
            <i className="ri-close-line text-[13px]" />
            Clear
          </button>
        )}
      </div>

      {/* Group cards grid */}
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3">
        {groups.map(g => (
          <GroupCard
            key={g.slug}
            group={g}
            isSelected={g.slug === selectedSlug}
            onSelect={() => handleSelect(g.slug)}
          />
        ))}
      </div>

      {/* Detail panel or empty state */}
      {selectedGroup ? (
        <GroupDetailPanel group={selectedGroup} />
      ) : (
        <div className="mt-5 rounded-xl border border-dashed border-gray-200 bg-white py-10 flex flex-col items-center justify-center text-center">
          <div className="w-10 h-10 rounded-full bg-gray-100 flex items-center justify-center mb-3">
            <i className="ri-layout-grid-line text-[18px] text-gray-400" />
          </div>
          <p className="text-sm font-medium text-gray-500">Select a category above</p>
          <p className="text-xs text-gray-400 mt-1">Browse tools by group</p>
        </div>
      )}
    </div>
  );
}

// ── Public export (with Suspense boundary) ───────────────────────────────────
export function NavigationGroupGrid() {
  return (
    <Suspense
      fallback={
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3 animate-pulse">
          {Array.from({ length: 12 }).map((_, i) => (
            <div key={i} className="h-20 rounded-xl bg-gray-100 border border-gray-200" />
          ))}
        </div>
      }
    >
      <NavHubInner />
    </Suspense>
  );
}
