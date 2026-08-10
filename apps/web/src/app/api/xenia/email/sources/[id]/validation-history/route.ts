import { cookies } from 'next/headers';
import { type NextRequest, NextResponse } from 'next/server';
import { getValidationHistory } from '@/lib/xenia-email-api';

type Params = { params: Promise<{ id: string }> };

export async function GET(req: NextRequest, { params }: Params) {
  const { id } = await params;
  const limit = Number(req.nextUrl.searchParams.get('limit') ?? '10');
  try {
    const jar = await cookies();
    const tok = jar.get('platform_session')?.value ?? '';
    const result = await getValidationHistory(tok, id, limit);
    return NextResponse.json(result);
  } catch (err) {
    const msg = err instanceof Error ? err.message : 'Failed to fetch validation history';
    return NextResponse.json({ error: msg }, { status: 502 });
  }
}
