import { requireOrg } from '@/lib/auth-guards';
import { buildNavGroups } from '@/lib/nav';
import { redirect } from 'next/navigation';
import { AppShell } from '@/components/shell/app-shell';

/**
 * Dashboard — org-type-aware landing page.
 * Redirects the user to the first available product route for their org/roles.
 * If no product roles exist, shows a blank state.
 */
export default async function DashboardPage() {
  const session = await requireOrg();
  const groups  = buildNavGroups(session);

  // Redirect to the first available product route
  const firstProductGroup = groups.find(g => g.id !== 'admin');
  if (firstProductGroup?.items[0]?.href) {
    redirect(firstProductGroup.items[0].href);
  }

  // No product groups — show empty state inside the shell
  return (
    <AppShell>
      <div className="flex items-center justify-center h-full">
        <div className="text-center space-y-2">
          <h1 className="text-lg font-semibold text-gray-900">No Products Available</h1>
          <p className="text-sm text-gray-500">
            Your organization has no active product subscriptions.
            Contact your administrator.
          </p>
        </div>
      </div>
    </AppShell>
  );
}
