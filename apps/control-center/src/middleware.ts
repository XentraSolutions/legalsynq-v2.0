import { NextResponse, type NextRequest } from 'next/server';

/**
 * Control Center middleware — route protection.
 *
 * All routes except /login and Next.js internals require the platform_session cookie.
 * The cookie is a gate only — actual role check (isPlatformAdmin) is done in
 * requirePlatformAdmin() inside each Server Component / layout.
 *
 * This middleware is intentionally lightweight. It does NOT decode the JWT or
 * make role decisions — those belong to the server-side auth guards.
 */

const PUBLIC_PATHS = [
  '/login',
  '/_next',
  '/favicon.ico',
  '/api/auth/login',
  '/api/auth/logout',
];

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  if (PUBLIC_PATHS.some(p => pathname.startsWith(p))) {
    return NextResponse.next();
  }

  const sessionCookie = request.cookies.get('platform_session');
  if (!sessionCookie) {
    const loginUrl = new URL('/login', request.url);
    loginUrl.searchParams.set('reason', 'unauthenticated');
    return NextResponse.redirect(loginUrl);
  }

  return NextResponse.next();
}

export const config = {
  matcher: ['/((?!_next/static|_next/image|favicon.ico).*)'],
};
