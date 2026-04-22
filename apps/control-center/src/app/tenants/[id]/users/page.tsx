import { requirePlatformAdmin }    from '@/lib/auth-guards';
import { controlCenterServerApi }  from '@/lib/control-center-api';
import { UserManagementTabs }      from '@/components/users/user-management-tabs';

export const dynamic = 'force-dynamic';

interface Props {
  params:       Promise<{ id: string }>;
  searchParams: Promise<{ page?: string; search?: string }>;
}

/**
 * /tenants/[id]/users — Full User Management hub for a specific tenant.
 *
 * Renders three sub-tabs:
 *   • Users       — list, search, invite, activate/deactivate, row actions
 *   • Groups      — access groups (create, archive)
 *   • Permissions — role permission matrix + permission catalog
 *
 * The shared header, breadcrumb, and top-level nav tabs are in layout.tsx.
 * Access: PlatformAdmin only.
 */
export default async function TenantUsersPage({ params, searchParams }: Props) {
  await requirePlatformAdmin();

  const { id } = await params;
  const sp     = await searchParams;
  const page   = Math.max(1, parseInt(sp.page ?? '1') || 1);
  const search = sp.search ?? '';

  let usersData = null;
  let hasError  = false;

  try {
    usersData = await controlCenterServerApi.users.list({
      tenantId: id,
      page,
      pageSize: 20,
      search,
    });
  } catch {
    hasError = true;
  }

  return (
    <UserManagementTabs
      tenantId={id}
      users={usersData?.items ?? []}
      totalCount={usersData?.totalCount ?? 0}
      page={usersData?.page ?? page}
      pageSize={usersData?.pageSize ?? 20}
      search={search}
      hasError={hasError}
    />
  );
}
