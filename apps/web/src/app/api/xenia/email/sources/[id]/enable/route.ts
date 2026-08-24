import { cookies } from 'next/headers';
import { type NextRequest, NextResponse } from 'next/server';
import { enableEmailSource } from '@/lib/xenia-email-api';

type Params = { params: Promise<{ id: string }> };

export async function PUT(_req: NextRequest, { params }: Params) {
  const { id } = await params;
  try {
    const jar = await cookies();
    const tok = jar.get('platform_session')?.value ?? '';
    await enableEmailSource(tok, id);
    return new NextResponse(null, { status: 204 });
  } catch (err) {
    const msg = err instanceof Error ? err.message : 'Failed to enable source';
    return NextResponse.json({ error: msg }, { status: 502 });
  }
}
