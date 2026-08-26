'use client';

import Link from 'next/link';
import { CircleAlert } from 'lucide-react';
import { Button, buttonVariants } from '@/components/selling/button';

export default function SellingContactDetailError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <div className="p-10 text-center space-y-4">
      <CircleAlert className="h-8 w-8 text-red-400" />
      <h2 className="text-lg font-semibold text-gray-800">Unable to load contact</h2>
      <p className="text-sm text-gray-500 max-w-md mx-auto">
        {error.message || 'An unexpected error occurred while loading the contact details.'}
      </p>
      {error.digest && (
        <p className="text-xs text-gray-400 font-mono">Error ID: {error.digest}</p>
      )}
      <div className="flex items-center justify-center gap-3 pt-2">
        <Button variant="primary" onClick={reset}>Try Again</Button>
        <Link href="/selling/contacts/companies" className={buttonVariants({ variant: 'secondary' })}>
          Back to Contacts
        </Link>
      </div>
    </div>
  );
}
