'use client';

import { createContext, useContext, useState, useEffect, useMemo, useRef } from 'react';
import { usePathname } from 'next/navigation';
import { inferProductFromPath } from '@/lib/nav';

interface ProductContextValue {
  selectedProductId: string | null;
  setSelectedProductId: (id: string | null) => void;
}

const ProductContext = createContext<ProductContextValue>({
  selectedProductId: null,
  setSelectedProductId: () => {},
});

const SELECTED_PRODUCT_STORAGE_KEY = 'selectedProductId';

function readStoredProductId(): string | null {
  if (typeof window === 'undefined') return null;
  try {
    return window.localStorage.getItem(SELECTED_PRODUCT_STORAGE_KEY);
  } catch {
    return null;
  }
}

export function ProductProvider({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const hydrated = useRef(false);

  // Matches the server-rendered value to avoid hydration mismatches; the
  // localStorage fallback is applied after mount in the effect below.
  const [selectedProductId, setSelectedProductId] = useState<string | null>(
    () => inferProductFromPath(pathname ?? ''),
  );

  useEffect(() => {
    const isHomepage = pathname === '/dashboard' || pathname === '/';
    if (!selectedProductId && !isHomepage) {
      const stored = readStoredProductId();
      if (stored) setSelectedProductId(stored);
    }
    hydrated.current = true;
    // Only run once, on mount, to restore the last selected product.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (pathname === '/dashboard' || pathname === '/') {
      setSelectedProductId(null);
      return;
    }
    const inferred = inferProductFromPath(pathname ?? '');
    if (inferred) setSelectedProductId(inferred);
  }, [pathname]);

  useEffect(() => {
    if (!hydrated.current || typeof window === 'undefined') return;
    try {
      if (selectedProductId) {
        window.localStorage.setItem(SELECTED_PRODUCT_STORAGE_KEY, selectedProductId);
      } else {
        window.localStorage.removeItem(SELECTED_PRODUCT_STORAGE_KEY);
      }
    } catch {
      // ignore storage errors (e.g. privacy mode)
    }
  }, [selectedProductId]);

  const value = useMemo(
    () => ({ selectedProductId, setSelectedProductId }),
    [selectedProductId],
  );

  return (
    <ProductContext.Provider value={value}>
      {children}
    </ProductContext.Provider>
  );
}

export function useProduct(): ProductContextValue {
  return useContext(ProductContext);
}
