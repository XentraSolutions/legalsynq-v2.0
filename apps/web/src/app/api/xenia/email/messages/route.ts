import { cookies } from 'next/headers';
import { type NextRequest, NextResponse } from 'next/server';
import { getEmailMessages } from '@/lib/xenia-email-api';

export async function GET(req: NextRequest) {
  try {
    const jar = await cookies();
    const tok = jar.get('platform_session')?.value ?? '';
    const sp  = req.nextUrl.searchParams;
    const result = await getEmailMessages(tok, {
      sourceId:     sp.get('sourceId')     ?? undefined,
      fromAddress:  sp.get('fromAddress')  ?? undefined,
      subject:      sp.get('subject')      ?? undefined,
      importStatus: sp.get('importStatus') ?? undefined,
      pageSize:     sp.get('pageSize')     ? Number(sp.get('pageSize'))  : undefined,
      pageOffset:   sp.get('pageOffset')   ? Number(sp.get('pageOffset')): undefined,
    });
    return NextResponse.json(result);
  } catch (err) {
    const msg = err instanceof Error ? err.message : 'Failed to fetch messages';
    return NextResponse.json({ error: msg }, { status: 502 });
  }
}
