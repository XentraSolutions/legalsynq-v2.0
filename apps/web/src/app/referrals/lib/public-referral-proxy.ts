import { headers } from 'next/headers';

function normalizePath(path: string): string {
  return path.startsWith('/') ? path : `/${path}`;
}

async function getRequestOrigin(): Promise<string> {
  const hdrs = await headers();
  const host = hdrs.get('x-forwarded-host') ?? hdrs.get('host');
  const proto = hdrs.get('x-forwarded-proto') ?? (process.env.NODE_ENV === 'development' ? 'http' : 'https');

  if (!host) {
    throw new Error('Request host header is missing.');
  }

  return `${proto}://${host}`;
}

export async function buildPublicCareConnectUrl(path: string): Promise<string> {
  const origin = await getRequestOrigin();
  return new URL(`/api/public/careconnect${normalizePath(path)}`, origin).toString();
}

export async function fetchPublicCareConnect(path: string, init?: RequestInit): Promise<Response> {
  const url = await buildPublicCareConnectUrl(path);
  return fetch(url, {
    cache: 'no-store',
    ...init,
  });
}
