import { requireProductAccess, FrontendProductCode } from '@/lib/auth-guards';

export const dynamic = 'force-dynamic';

export default async function XeniaLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  await requireProductAccess(FrontendProductCode.Xenia);
  return <>{children}</>;
}
