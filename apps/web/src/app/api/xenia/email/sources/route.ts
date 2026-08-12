import { cookies } from 'next/headers';
import { type NextRequest, NextResponse } from 'next/server';
import { getEmailSources, createEmailSource } from '@/lib/xenia-email-api';

function token(jar: Awaited<ReturnType<typeof cookies>>) {
  return jar.get('platform_session')?.value ?? '';
}

export async function GET() {
  try {
    const jar = await cookies();
    const result = await getEmailSources(token(jar));
    return NextResponse.json(result);
  } catch (err) {
    const msg = err instanceof Error ? err.message : 'Failed to fetch email sources';
    return NextResponse.json({ error: msg }, { status: 502 });
  }
}

export async function POST(req: NextRequest) {
  try {
    const jar = await cookies();
    const body = await req.json();
    const source = await createEmailSource(token(jar), body);
    return NextResponse.json(source, { status: 201 });
  } catch (err) {
    const msg = err instanceof Error ? err.message : 'Failed to create email source';
    return NextResponse.json({ error: msg }, { status: 502 });
  }
}
