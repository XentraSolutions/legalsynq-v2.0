import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import { redirect } from 'next/navigation';

const XENIA_BASE = process.env.XENIA_API_BASE ?? 'http://127.0.0.1:5035';

export async function POST(
  _req: Request,
  { params }: { params: Promise<{ id: string }> },
) {
  await requirePlatformAdmin();
  const { id } = await params;
  const jar = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';

  try {
    await fetch(`${XENIA_BASE}/email/sources/${id}/sync`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`,
      },
      cache: 'no-store',
    });
  } catch {
    // best-effort
  }

  redirect(`/xenia/email/sources/${id}/sync`);
}
