import { cookies } from 'next/headers';
import { type NextRequest, NextResponse } from 'next/server';
import { getEmailSource, updateEmailSource, deleteEmailSource } from '@/lib/xenia-email-api';

type Params = { params: Promise<{ id: string }> };

function token(jar: Awaited<ReturnType<typeof cookies>>) {
  return jar.get('platform_session')?.value ?? '';
}

export async function GET(_req: NextRequest, { params }: Params) {
  const { id } = await params;
  try {
    const jar = await cookies();
    const source = await getEmailSource(token(jar), id);
    return NextResponse.json(source);
  } catch (err) {
    const msg = err instanceof Error ? err.message : 'Failed to fetch email source';
    return NextResponse.json({ error: msg }, { status: 502 });
  }
}

export async function PUT(req: NextRequest, { params }: Params) {
  const { id } = await params;
  try {
    const jar = await cookies();
    const body = await req.json();
    const source = await updateEmailSource(token(jar), id, body);
    return NextResponse.json(source);
  } catch (err) {
    const msg = err instanceof Error ? err.message : 'Failed to update email source';
    return NextResponse.json({ error: msg }, { status: 502 });
  }
}

export async function DELETE(_req: NextRequest, { params }: Params) {
  const { id } = await params;
  try {
    const jar = await cookies();
    await deleteEmailSource(token(jar), id);
    return new NextResponse(null, { status: 204 });
  } catch (err) {
    const msg = err instanceof Error ? err.message : 'Failed to delete email source';
    return NextResponse.json({ error: msg }, { status: 502 });
  }
}
