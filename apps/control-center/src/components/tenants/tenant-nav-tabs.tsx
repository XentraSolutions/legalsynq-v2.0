'use client';

/**
 * TenantNavTabs — active-aware sub-navigation for tenant detail pages.
 *
 * Uses usePathname() so the layout (a server component) doesn't need to know
 * the current URL segment. The active tab is derived purely from the pathname.
 */

import Link        from 'next/link';
import { usePathname } from 'next/navigation';

interface TenantNavTabsProps {
  overviewHref:      string;
  usersHref:         string;
  notificationsHref: string;
}

export function TenantNavTabs({
  overviewHref,
  usersHref,
  notificationsHref,
}: TenantNavTabsProps) {
  const pathname = usePathname();

  const isNotifications = pathname.includes('/notifications');
  const isUsers         = !isNotifications && pathname.includes('/users');
  const isOverview      = !isUsers && !isNotifications;

  return (
    <div className="flex items-center gap-0 border-b border-gray-200">
      <Tab href={overviewHref}      label="Overview"       active={isOverview} />
      <Tab href={usersHref}         label="Users"          active={isUsers} />
      <Tab href={notificationsHref} label="Notifications"  active={isNotifications} />
    </div>
  );
}

function Tab({ href, label, active }: { href: string; label: string; active: boolean }) {
  return (
    <Link
      href={href}
      className={[
        'px-4 py-2 text-sm font-medium border-b-2 -mb-px transition-colors',
        active
          ? 'border-indigo-600 text-indigo-700'
          : 'border-transparent text-gray-600 hover:text-gray-900 hover:border-gray-300',
      ].join(' ')}
    >
      {label}
    </Link>
  );
}
