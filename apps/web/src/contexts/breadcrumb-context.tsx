"use client";

import {
  createContext,
  useContext,
  useEffect,
  useId,
  useState,
  type ReactNode,
} from "react";
import type { NavItem } from "@/types";

interface BreadcrumbEntry {
  id: string;
  trail: NavItem[];
}

interface BreadcrumbContextValue {
  current: BreadcrumbEntry | null;
  setTrail: (id: string, trail: NavItem[] | null) => void;
}

const BreadcrumbContext = createContext<BreadcrumbContextValue | null>(null);

export function BreadcrumbProvider({ children }: { children: ReactNode }) {
  const [current, setCurrent] = useState<BreadcrumbEntry | null>(null);

  const setTrail = (id: string, trail: NavItem[] | null) => {
    setCurrent((prev) => {
      if (trail) return { id, trail };
      // Only clear if we're still the active owner — otherwise a page that's
      // already unmounting could wipe out the next page's freshly-set trail.
      return prev?.id === id ? null : prev;
    });
  };

  return (
    <BreadcrumbContext.Provider value={{ current, setTrail }}>
      {children}
    </BreadcrumbContext.Provider>
  );
}

/** Read by the top-bar: an active page override, or null to use the nav-config trail. */
export function useBreadcrumbOverride(): NavItem[] | null {
  const ctx = useContext(BreadcrumbContext);
  return ctx?.current?.trail ?? null;
}

/**
 * Lets a page override the top-bar breadcrumb trail with something the
 * generic nav-config lookup can't derive on its own — e.g. a dynamic
 * entity's name on a detail page that has no matching nav item.
 * Pass `null` (e.g. while the entity is still loading) to fall back to the
 * nav-config trail instead of showing a stale or empty one.
 */
export function usePageBreadcrumbTrail(trail: NavItem[] | null): void {
  const ctx = useContext(BreadcrumbContext);
  const id = useId();
  const trailKey = trail ? JSON.stringify(trail) : null;

  useEffect(() => {
    if (!ctx) return;
    ctx.setTrail(id, trailKey ? JSON.parse(trailKey) : null);
    return () => ctx.setTrail(id, null);
    // `ctx` is deliberately omitted: the provider's context value is a new
    // object every render (its `current` field changes whenever any page
    // calls setTrail), so including it here would re-fire this effect on
    // every unrelated render and loop forever. `ctx.setTrail` only closes
    // over the stable `setState` function, so an older `ctx` reference
    // still updates the current state correctly.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id, trailKey]);
}
