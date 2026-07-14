import { cookies } from 'next/headers';
import { type NextRequest, NextResponse } from 'next/server';
import { getEmailMessage } from '@/lib/xenia-email-api';

type Params = { params: Promise<{ id: string }> };

export async function GET(_req: NextRequest, { params }: Params) {
  const { id } = await params;
  try {
    const jar = await cookies();
    const tok = jar.get('platform_session')?.value ?? '';
    const message = await getEmailMessage(tok, id);
    return NextResponse.json(message);
  } catch (err) {
    const msg = err instanceof Error ? err.message : 'Failed to fetch message';
    return NextResponse.json({ error: msg }, { status: 502 });
  }
}
