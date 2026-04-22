import { NextResponse, type NextRequest } from 'next/server';

/**
 * Global Next.js middleware — route protection.
 *
 * Rules:
 *  1. Public routes (/login, /portal, static assets) — always allowed through.
 *  2. Protected routes — require the platform_session cookie to exist.
 *     The existence of the cookie is a gate only; the actual token is validated
 *     server-side by /auth/me in getServerSession(). Middleware does NOT decode
 *     or trust the JWT payload for access decisions — that is the backend's job.
 *  3. Admin routes (/admin) — same cookie gate; real role check happens in the
 *     requireAdmin() auth guard inside the route/layout Server Component.
 *  4. Portal routes (/portal) — checked for portal_session cookie only.
 *     Portal-specific pages below /portal/* that need auth will handle it in their
 *     page server components.
 *  5. This middleware NEVER makes backend capability decisions.
 *
 * The middleware is intentionally lightweight. All detailed authorization is
 * server-side inside the route handlers and auth guard helpers.
 */

const PUBLIC_PATHS = [
  '/login',
  '/no-org',
  '/portal/login',
  '/_next',
  '/favicon.ico',
  '/.well-known',
  // Auth API endpoints must be reachable before a session cookie exists
  '/api/auth/login',
  '/api/auth/logout',
  '/api/auth/forgot-password',
  '/api/auth/reset-password',
  '/forgot-password',
  '/reset-password',
  '/accept-invite',
  '/api/auth/accept-invite',
  // Public branding / logo routes — no session required (used by login page)
  '/api/branding',
  '/api/identity/api/tenants/current/branding',
  // LSCC-005: Public referral token routes — no session required
  '/referrals/view',
  '/referrals/accept',
  // LSCC-008: Provider activation funnel — no session required
  '/referrals/activate',
  // CC2-INT-B07: Public tenant network directory — no session required
  '/network',
  '/careconnect/network',
  '/api/public/',
];

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // Allow public and Next.js internal routes
  if (PUBLIC_PATHS.some(p => pathname.startsWith(p))) {
    return NextResponse.next();
  }

  // Portal sub-routes beyond /portal/login require portal_session
  // /portal/login handled above as PUBLIC; all other /portal/* need cookie
  if (pathname.startsWith('/portal/')) {
    const portalCookie = request.cookies.get('portal_session');
    if (!portalCookie) {
      return NextResponse.redirect(new URL('/portal/login', request.url));
    }
    return NextResponse.next();
  }

  // All other routes require platform_session cookie
  const sessionCookie = request.cookies.get('platform_session');
  if (!sessionCookie) {
    const loginUrl = new URL('/login', request.url);
    loginUrl.searchParams.set('reason', 'unauthenticated');
    return NextResponse.redirect(loginUrl);
  }

  // Let the request through — server components / layouts will run full
  // getServerSession() and requireOrg() / requireAdmin() guards as needed.
  return NextResponse.next();
}

export const config = {
  matcher: [
    /*
     * Match all request paths EXCEPT:
     * - _next/static       (Next.js static assets)
     * - _next/image        (Next.js image optimization)
     * - favicon.ico
     * - Static file types  (images/fonts served from /public — must bypass auth
     *                       so the login page can load logos without a session)
     */
    '/((?!_next/static|_next/image|favicon.ico|.*\\.(?:png|jpg|jpeg|gif|svg|ico|webp|woff2?|ttf|otf)).*)',
  ],
};
