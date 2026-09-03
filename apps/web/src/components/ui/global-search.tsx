"use client";

import { useTanStackGlobalSearch } from "@/hooks/use-global-search";
import {
  Search,
  X,
  Loader2,
  CornerDownLeft,
  ArrowUp,
  ArrowDown,
  FileSearch,
  User,
  Stethoscope,
  Building2,
  Landmark,
  UserPlus,
  ClipboardList,
  Scale,
} from "lucide-react";
import { useRouter } from "next/navigation";
import React, { useEffect, useMemo, useRef, useState } from "react";
import { clsx } from "clsx";
import { isMacPlatform } from "@/lib/platform";

interface ResultDetail {
  title: string;
  description: string;
}

interface ResultItem {
  id: string;
  name: string;
  url: string;
  details?: ResultDetail[];
}

interface ResultGroup {
  title: string;
  items: ResultItem[];
}

// Maps a category title (from the search API) to a representative icon.
// Falls back to a generic search icon for any category not listed here.
const CATEGORY_ICONS: Record<string, React.ComponentType<{ className?: string }>> = {
  "Plaintiff Name": User,
  "Medical Providers": Stethoscope,
  "Medical Facilities": Building2,
  "Funding Companies": Landmark,
  Leads: UserPlus,
  Servicing: ClipboardList,
  "Law Firms": Scale,
};

export const GlobalSearch: React.FC<{ onClose?: () => void }> = ({
  onClose,
}) => {
  const router = useRouter();
  const isMac = isMacPlatform();
  const {
    inputValue,
    setInputValue,
    results,
    isLoading,
    error,
    clearSearch,
  } = useTanStackGlobalSearch(350, 2);

  const groups: ResultGroup[] = (results ?? []).filter(
    (g: ResultGroup) => g.items?.length > 0,
  );
  const flatItems = useMemo(
    () => groups.flatMap((g) => g.items.map((item) => ({ group: g.title, item }))),
    [groups],
  );

  const [activeIndex, setActiveIndex] = useState(0);
  useEffect(() => {
    setActiveIndex(0);
  }, [flatItems.length, inputValue]);

  const inputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLDivElement>(null);
  const activeItemRef = useRef<HTMLDivElement>(null);
  const modalRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    inputRef.current?.focus();
  }, []);

  useEffect(() => {
    activeItemRef.current?.scrollIntoView({ block: "nearest" });
  }, [activeIndex]);

  function handleClose() {
    onClose?.();
  }

  function navigateTo(url: string) {
    handleClose();
    router.push(url);
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === "Escape") {
      e.preventDefault();
      handleClose();
      return;
    }
    if (e.key === "ArrowDown") {
      e.preventDefault();
      if (flatItems.length > 0)
        setActiveIndex((i) => (i + 1) % flatItems.length);
      return;
    }
    if (e.key === "ArrowUp") {
      e.preventDefault();
      if (flatItems.length > 0)
        setActiveIndex((i) => (i - 1 + flatItems.length) % flatItems.length);
      return;
    }
    if (e.key === "Enter") {
      e.preventDefault();
      const selected = flatItems[activeIndex];
      if (selected) navigateTo(selected.item.url);
    }
  }

  // Keep focus trapped inside the modal — there's nothing behind it that
  // should be reachable by Tab while it's open.
  function handleTrapTab(e: React.KeyboardEvent<HTMLDivElement>) {
    if (e.key !== "Tab") return;
    const focusable = modalRef.current?.querySelectorAll<HTMLElement>(
      'input, button, [tabindex]:not([tabindex="-1"])',
    );
    if (!focusable || focusable.length === 0) return;
    const list = Array.from(focusable);
    const first = list[0];
    const last = list[list.length - 1];
    const current = document.activeElement as HTMLElement | null;

    if (e.shiftKey) {
      if (current === first || !list.includes(current as HTMLElement)) {
        e.preventDefault();
        last.focus();
      }
    } else {
      if (current === last || !list.includes(current as HTMLElement)) {
        e.preventDefault();
        first.focus();
      }
    }
  }

  const showEmptyState = inputValue.trim().length < 2;
  const showNoResults =
    !isLoading && !error && !showEmptyState && flatItems.length === 0;

  return (
    <div
      className="fixed inset-0 z-50 flex items-start justify-center bg-gray-900/40 backdrop-blur-sm px-4 pt-[12vh]"
      onMouseDown={(e) => {
        if (e.target === e.currentTarget) handleClose();
      }}
    >
      <div
        ref={modalRef}
        onKeyDown={handleTrapTab}
        className="w-full max-w-2xl rounded-2xl bg-white shadow-2xl border border-gray-200/80 overflow-hidden flex flex-col max-h-[70vh]"
      >
        {/* Input row */}
        <div className="flex items-center gap-3 px-4 h-14 border-b border-gray-100 shrink-0">
          <Search className="h-5 w-5 text-gray-400 shrink-0" />
          <input
            ref={inputRef}
            type="text"
            placeholder="Search plaintiffs, providers, facilities, law firms…"
            value={inputValue}
            onChange={(e) => setInputValue(e.target.value)}
            onKeyDown={handleKeyDown}
            className="flex-1 min-w-0 bg-transparent text-[15px] text-gray-900 placeholder:text-gray-400 focus:outline-none"
          />
          {isLoading && (
            <Loader2 className="h-4 w-4 text-gray-400 animate-spin shrink-0" />
          )}
          {inputValue && !isLoading && (
            <button
              type="button"
              onClick={clearSearch}
              title="Clear"
              className="flex items-center justify-center h-6 w-6 rounded-md text-gray-400 hover:bg-gray-100 hover:text-gray-600 transition-colors shrink-0"
            >
              <X className="h-3.5 w-3.5" />
            </button>
          )}
          <kbd className="hidden sm:inline-flex items-center h-5 px-1.5 rounded border border-gray-200 bg-gray-50 text-[10px] font-medium text-gray-400 shrink-0">
            ESC
          </kbd>
        </div>

        {/* Results */}
        <div ref={listRef} className="overflow-y-auto flex-1 min-h-0">
          {error && (
            <div className="flex flex-col items-center gap-2 py-14 text-center">
              <FileSearch className="h-8 w-8 text-red-300" />
              <p className="text-sm text-red-500">{error}</p>
            </div>
          )}

          {!error && showEmptyState && (
            <div className="flex flex-col items-center gap-2 py-14 text-center px-6">
              <Search className="h-8 w-8 text-gray-200" />
              <p className="text-sm text-gray-400">
                Keep typing to search across cases, providers, facilities and
                more
              </p>
            </div>
          )}

          {!error && showNoResults && (
            <div className="flex flex-col items-center gap-2 py-14 text-center px-6">
              <FileSearch className="h-8 w-8 text-gray-200" />
              <p className="text-sm text-gray-500">
                No results for <span className="font-medium">"{inputValue}"</span>
              </p>
            </div>
          )}

          {!error &&
            !showEmptyState &&
            groups.length > 0 &&
            (() => {
              let runningIndex = -1;
              return groups.map((group) => {
                const Icon = CATEGORY_ICONS[group.title] ?? FileSearch;
                return (
                  <div key={group.title} className="py-2 first:pt-3">
                    <div className="flex items-center gap-1.5 px-4 pb-1.5">
                      <Icon className="h-3.5 w-3.5 text-gray-400" />
                      <span className="text-[11px] font-semibold text-gray-400 uppercase tracking-wider">
                        {group.title}
                      </span>
                    </div>

                    {group.items.map((item) => {
                      runningIndex += 1;
                      const isActive = runningIndex === activeIndex;
                      const idx = runningIndex;
                      return (
                        <div
                          key={`${group.title}-${item.id}`}
                          ref={isActive ? activeItemRef : undefined}
                          onMouseEnter={() => setActiveIndex(idx)}
                          onClick={() => navigateTo(item.url)}
                          className={clsx(
                            "mx-2 px-3 py-2 rounded-lg cursor-pointer transition-colors",
                            isActive ? "bg-primary/10" : "hover:bg-gray-50",
                          )}
                        >
                          <div
                            className={clsx(
                              "text-sm font-medium",
                              isActive ? "text-primary" : "text-gray-900",
                            )}
                          >
                            {item.name}
                          </div>

                          {item.details && item.details.length > 0 && (
                            <div className="flex flex-wrap items-center gap-x-3 gap-y-0.5 mt-0.5">
                              {item.details.map((detail, detailIndex) => (
                                <div
                                  key={detailIndex}
                                  className="inline-flex items-center gap-1 text-xs text-gray-500"
                                >
                                  <span className="text-gray-400">
                                    {detail.title}:
                                  </span>
                                  <span>{detail.description}</span>
                                </div>
                              ))}
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                );
              });
            })()}
        </div>

        {/* Footer hint bar */}
        {!showEmptyState && flatItems.length > 0 && (
          <div className="flex items-center gap-4 px-4 h-9 border-t border-gray-100 shrink-0 text-[11px] text-gray-400">
            <span className="flex items-center gap-1">
              <ArrowUp className="h-3 w-3" />
              <ArrowDown className="h-3 w-3" />
              Navigate
            </span>
            <span className="flex items-center gap-1">
              <CornerDownLeft className="h-3 w-3" />
              Select
            </span>
            <span className="ml-auto flex items-center gap-1">
              <kbd className="flex items-center justify-center h-5 min-w-[18px] px-1 rounded border border-gray-200 bg-gray-50 text-[10px] font-medium text-gray-400">
                {isMac ? "⌘" : "Ctrl"}
              </kbd>
              <kbd className="flex items-center justify-center h-5 min-w-[18px] px-1 rounded border border-gray-200 bg-gray-50 text-[10px] font-medium text-gray-400">
                /
              </kbd>
              Shortcut
            </span>
          </div>
        )}
      </div>
    </div>
  );
};
