import { cookies } from 'next/headers';
import { type NextRequest, NextResponse } from 'next/server';
import { validateEmailSource } from '@/lib/xenia-email-api';

type Params = { params: Promise<{ id: string }> };

export async function POST(_req: NextRequest, { params }: Params) {
  const { id } = await params;
  try {
    const jar = await cookies();
    const tok = jar.get('platform_session')?.value ?? '';
    const result = await validateEmailSource(tok, id);
    return NextResponse.json(result);
  } catch (err) {
    const msg = err instanceof Error ? err.message : 'Validation failed';
    return NextResponse.json({ error: msg }, { status: 502 });
  }
}
