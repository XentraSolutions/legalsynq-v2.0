'use client';

import { LienProviders } from '@/components/lien/lien-providers';

export default function LienLayout({ children }: { children: React.ReactNode }) {
  return <LienProviders>{children}</LienProviders>;
}
