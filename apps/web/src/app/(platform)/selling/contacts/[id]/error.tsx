'use client';

import Link from 'next/link';
import { Button, buttonVariants } from '@/components/ui/button';

// Selling's brand accent, matching the convention used on other selling pages.
const PRIMARY_BUTTON_CLASSNAME = 'bg-[#EE7132] hover:bg-[#EE7132]/90 text-white';

export default function SellingContactDetailError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <div className="p-10 text-center space-y-4">
      <i className="ri-error-warning-line text-4xl text-red-400" />
      <h2 className="text-lg font-semibold text-gray-800">Unable to load contact</h2>
      <p className="text-sm text-gray-500 max-w-md mx-auto">
        {error.message || 'An unexpected error occurred while loading the contact details.'}
      </p>
      {error.digest && (
        <p className="text-xs text-gray-400 font-mono">Error ID: {error.digest}</p>
      )}
      <div className="flex items-center justify-center gap-3 pt-2">
        <Button className={PRIMARY_BUTTON_CLASSNAME} onClick={reset}>Try Again</Button>
        <Link href="/selling/contacts" className={buttonVariants({ variant: 'secondary' })}>
          Back to Contacts
        </Link>
      </div>
    </div>
  );
}
