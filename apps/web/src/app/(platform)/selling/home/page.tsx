import { redirect } from 'next/navigation';

export const dynamic = 'force-dynamic';

/**
 * LSV3-628: Home is not part of the Phase 1 migration scope and has been removed
 * from the sidebar. This route is kept as a redirect (rather than deleted) so old
 * links/bookmarks to /lien/home don't 404 or show a blank page.
 */
export default function LienHomePage() {
  redirect('/lien/dashboard');
}
