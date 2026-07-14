import { cookies } from 'next/headers';
import { type NextRequest, NextResponse } from 'next/server';
import { disableEmailSource } from '@/lib/xenia-email-api';

type Params = { params: Promise<{ id: string }> };

export async function PUT(_req: NextRequest, { params }: Params) {
  const { id } = await params;
  try {
    const jar = await cookies();
    const tok = jar.get('platform_session')?.value ?? '';
    await disableEmailSource(tok, id);
    return new NextResponse(null, { status: 204 });
  } catch (err) {
    const msg = err instanceof Error ? err.message : 'Failed to disable source';
    return NextResponse.json({ error: msg }, { status: 502 });
  }
}
