'use client';

/**
 * Sign-out button — Client Component.
 * Calls POST /api/auth/logout (clears HttpOnly cookie) then redirects to /login.
 */
export function SignOutButton() {
  async function handleSignOut() {
    await fetch('/api/auth/logout', { method: 'POST' });
    window.location.href = '/login';
  }

  return (
    <button
      onClick={handleSignOut}
      className="text-sm text-gray-500 hover:text-gray-900 transition-colors"
    >
      Sign out
    </button>
  );
}
