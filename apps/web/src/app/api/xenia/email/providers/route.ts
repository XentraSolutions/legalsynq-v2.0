import { cookies } from 'next/headers';
import { NextResponse } from 'next/server';
import { getEmailProviders } from '@/lib/xenia-email-api';

export async function GET() {
  try {
    const jar = await cookies();
    const tok = jar.get('platform_session')?.value ?? '';
    const result = await getEmailProviders(tok);
    return NextResponse.json(result);
  } catch (err) {
    const msg = err instanceof Error ? err.message : 'Failed to fetch providers';
    return NextResponse.json({ error: msg }, { status: 502 });
  }
}
