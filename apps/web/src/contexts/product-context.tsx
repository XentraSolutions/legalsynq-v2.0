'use client';

import { createContext, useContext, useMemo } from 'react';
import { usePathname } from 'next/navigation';
import { useSession } from '@/hooks/use-session';
import { buildNavGroups } from '@/lib/nav';
import { inferProductIdFromPath } from '@/lib/product-config';
import type { NavGroup } from '@/types';

interface ProductContextValue {
  /** The product id inferred from the current pathname (e.g. 'careconnect', 'fund'). */
  activeProductId: string | null;
  /** All nav groups the user has access to (role-filtered). */
  availableGroups: NavGroup[];
  /** The single NavGroup corresponding to the active product, or null if none matched. */
  activeGroup: NavGroup | null;
}

const ProductContext = createContext<ProductContextValue>({
  activeProductId: null,
  availableGroups: [],
  activeGroup: null,
});

export function ProductProvider({ children }: { children: React.ReactNode }) {
  const { session } = useSession();
  const pathname = usePathname();

  const value = useMemo<ProductContextValue>(() => {
    const availableGroups = session ? buildNavGroups(session) : [];
    const activeProductId = inferProductIdFromPath(pathname);
    const activeGroup = availableGroups.find(g => g.id === activeProductId) ?? null;
    return { activeProductId, availableGroups, activeGroup };
  }, [session, pathname]);

  return (
    <ProductContext.Provider value={value}>
      {children}
    </ProductContext.Provider>
  );
}

export function useProduct(): ProductContextValue {
  return useContext(ProductContext);
}
