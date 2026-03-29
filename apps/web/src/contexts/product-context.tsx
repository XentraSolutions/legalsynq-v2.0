'use client';

import { createContext, useContext, useState, useEffect } from 'react';
import { usePathname } from 'next/navigation';
import { inferProductFromPath } from '@/lib/nav';

interface ProductContextValue {
  /** Currently selected product id (set by app switcher or inferred from path). */
  selectedProductId: string | null;
  /** Programmatically select a product (called by the app switcher). */
  setSelectedProductId: (id: string | null) => void;
}

const ProductContext = createContext<ProductContextValue>({
  selectedProductId: null,
  setSelectedProductId: () => {},
});

export function ProductProvider({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();

  // Initialise from the current path so a hard-nav/refresh lands correctly
  const [selectedProductId, setSelectedProductId] = useState<string | null>(
    () => inferProductFromPath(pathname),
  );

  // Keep the selection in sync when the user navigates via browser back/forward
  useEffect(() => {
    const inferred = inferProductFromPath(pathname);
    if (inferred) setSelectedProductId(inferred);
  }, [pathname]);

  return (
    <ProductContext.Provider value={{ selectedProductId, setSelectedProductId }}>
      {children}
    </ProductContext.Provider>
  );
}

export function useProduct(): ProductContextValue {
  return useContext(ProductContext);
}
