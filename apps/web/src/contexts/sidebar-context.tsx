"use client";

import { createContext, useContext, useState, useEffect } from "react";

const STORAGE_KEY = "ls_sidebar_collapsed";

interface SidebarContextValue {
  collapsed: boolean;
  mounted: boolean;
  toggle: () => void;
}

const SidebarContext = createContext<SidebarContextValue>({
  collapsed: false,
  mounted: false,
  toggle: () => {},
});

/**
 * Shared collapse state for the sidebar, lifted out so the top bar's
 * collapse toggle (left of the logo, aligned with the sidebar) and the
 * sidebar itself stay in sync.
 */
export function SidebarProvider({ children }: { children: React.ReactNode }) {
  const [collapsed, setCollapsed] = useState(false);
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === "true") setCollapsed(true);
    setMounted(true);
  }, []);

  function toggle() {
    setCollapsed((prev) => {
      const next = !prev;
      localStorage.setItem(STORAGE_KEY, String(next));
      return next;
    });
  }

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if ((e.ctrlKey || e.metaKey) && e.key === "[") {
        e.preventDefault();
        toggle();
      }
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  return (
    <SidebarContext.Provider value={{ collapsed, mounted, toggle }}>
      {children}
    </SidebarContext.Provider>
  );
}

export function useSidebar() {
  return useContext(SidebarContext);
}
