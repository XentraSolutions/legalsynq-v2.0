"use client";

import * as React from "react";
import * as PopoverPrimitive from "@radix-ui/react-popover";
import { Check, ChevronDown, Loader2, Plus } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Minimum shape an option must satisfy. Pass a wider type via the
 * `TOption` generic (e.g. `{ value: string; label: string; email: string }`)
 * to get it back typed in `onChange` and `renderOption`.
 */
export interface BaseSelectOption {
  value: string;
  label: string;
  disabled?: boolean;
}

/** Row shown when an option is highlighted/selected, passed to `renderOption`. */
export interface BaseSelectOptionState {
  selected: boolean;
  active: boolean;
  search: string;
}

export interface BaseSelectCreateAction {
  /** Label shown in the trigger row, e.g. "Add Law Firm". */
  label: string;
  /** Opens the caller's own create form/modal. BaseSelect does not render it. */
  onSelect: () => void;
  icon?: React.ReactNode;
}

export interface BaseSelectProps<TOption extends BaseSelectOption = BaseSelectOption> {
  /** Currently selected option's `value`, or null/undefined for none. */
  value?: string | null;
  /** Fires with the selected option's value and the full option object. */
  onChange: (value: string, option: TOption) => void;
  /** Full option list. In "infinite" mode this should be the accumulated pages so far. */
  options: TOption[];

  /** "eager" renders every option up front; "infinite" fetches more as the list is scrolled. Default "eager". */
  loadingMode?: "eager" | "infinite";
  /** True while the first page of options is loading. */
  isLoading?: boolean;
  /** True while a subsequent page is loading (infinite mode only). */
  isFetchingMore?: boolean;
  /** Whether another page is available (infinite mode only). */
  hasNextPage?: boolean;
  /** Called when the scroll sentinel comes into view (infinite mode only). */
  onLoadMore?: () => void;

  /** Controlled search text. Falls back to internal state when omitted. */
  search?: string;
  /** Fires on every keystroke — wire this to a debounced server search. */
  onSearchChange?: (search: string) => void;
  /** Filter `options` locally by label as well. Default true. Set false when `options` is already server-filtered. */
  filterLocally?: boolean;
  /** Highlight the matched substring of each option's label. Default true. Ignored when `renderOption` is set. */
  highlightMatch?: boolean;

  /**
   * Fully customize each option row (e.g. show a secondary line like an
   * email or address under the label). BaseSelect still overlays the
   * selected-check indicator; `renderOption` only controls the row's
   * own content.
   */
  renderOption?: (option: TOption, state: BaseSelectOptionState) => React.ReactNode;

  /** Renders a trailing "+ Add …" row that hands off to the caller's own modal/form. */
  createAction?: BaseSelectCreateAction;

  placeholder?: string;
  searchPlaceholder?: string;
  emptyText?: string;
  loadingText?: string;
  disabled?: boolean;
  error?: boolean;
  className?: string;
  contentClassName?: string;
}

/** Wraps the first case-insensitive match of `query` in `label` with a `<mark>`. */
function highlightLabel(label: string, query: string): React.ReactNode {
  const trimmed = query.trim();
  if (!trimmed) return label;

  const index = label.toLowerCase().indexOf(trimmed.toLowerCase());
  if (index === -1) return label;

  return (
    <>
      {label.slice(0, index)}
      <mark className="bg-primary/15 text-primary rounded-[2px]">
        {label.slice(index, index + trimmed.length)}
      </mark>
      {label.slice(index + trimmed.length)}
    </>
  );
}

/**
 * Pure, presentational select/combobox built on Radix Popover.
 *
 * BaseSelect owns only UI state (open, search text, keyboard focus) — it
 * never fetches data itself. Callers supply `options` plus, where relevant,
 * the loading/pagination flags and callbacks. This keeps it reusable across
 * both fully-loaded lists ("eager") and scroll-paginated ones ("infinite"),
 * and across purely client-side filtering or server-driven search
 * (`onSearchChange`).
 *
 * Features:
 * - Eager or infinite-scroll loading via `loadingMode`.
 * - Client-side filtering and/or server-side search via `onSearchChange`.
 * - Autocomplete match highlighting via `highlightMatch`.
 * - Custom row rendering via `renderOption`, for options that need more
 *   than a single label (e.g. a secondary line).
 * - An optional "+ Add …" row (`createAction`) that hands off to the
 *   caller's own create form/modal — BaseSelect never renders the modal
 *   itself, it only closes the popover and calls `onSelect`.
 * - Arrow-key/Enter/Escape navigation and `role="listbox"`/`"option"` for
 *   basic accessibility.
 *
 * @example Eager, with an inline "add new" action
 * ```tsx
 * const [showCreate, setShowCreate] = useState(false);
 * <BaseSelect
 *   value={form.facilityId}
 *   onChange={(v, option) => setForm({ ...form, facilityId: v, facility: option.label })}
 *   options={facilityList}
 *   placeholder="Select facility..."
 *   createAction={{ label: "Add New Medical Facility", onSelect: () => setShowCreate(true) }}
 * />
 * {showCreate && (
 *   <CreateMedicalFacility
 *     open={showCreate}
 *     onClose={() => setShowCreate(false)}
 *     onCreated={(created) => {
 *       setForm({ ...form, facilityId: created.id });
 *       reloadFacilities();
 *       setShowCreate(false);
 *     }}
 *   />
 * )}
 * ```
 *
 * @example Server search + infinite scroll
 * ```tsx
 * const [search, setSearch] = useState("");
 * const query = useInfiniteContacts({ search });
 * <BaseSelect
 *   value={contactId}
 *   onChange={setContactId}
 *   options={query.data?.pages.flatMap((p) => p.items) ?? []}
 *   loadingMode="infinite"
 *   isLoading={query.isLoading}
 *   isFetchingMore={query.isFetchingNextPage}
 *   hasNextPage={query.hasNextPage}
 *   onLoadMore={query.fetchNextPage}
 *   onSearchChange={setSearch}
 *   filterLocally={false}
 * />
 * ```
 */
export function BaseSelect<TOption extends BaseSelectOption = BaseSelectOption>({
  value,
  onChange,
  options,
  loadingMode = "eager",
  isLoading = false,
  isFetchingMore = false,
  hasNextPage = false,
  onLoadMore,
  search: controlledSearch,
  onSearchChange,
  filterLocally = true,
  highlightMatch = true,
  renderOption,
  createAction,
  placeholder = "Select…",
  searchPlaceholder = "Search...",
  emptyText = "No options found.",
  loadingText = "Loading...",
  disabled,
  error,
  className,
  contentClassName,
}: BaseSelectProps<TOption>) {
  const [open, setOpen] = React.useState(false);
  const [internalSearch, setInternalSearch] = React.useState("");
  const [activeIndex, setActiveIndex] = React.useState(0);

  const search = controlledSearch ?? internalSearch;
  const listRef = React.useRef<HTMLDivElement>(null);
  const sentinelRef = React.useRef<HTMLDivElement>(null);

  const selected = options.find((o) => o.value === value);

  const filteredOptions = React.useMemo(() => {
    if (!filterLocally) return options;
    const query = search.trim().toLowerCase();
    if (!query) return options;
    return options.filter((o) => o.label.toLowerCase().includes(query));
  }, [options, search, filterLocally]);

  React.useEffect(() => {
    setActiveIndex(0);
  }, [filteredOptions]);

  const handleSearchChange = (next: string) => {
    if (controlledSearch === undefined) setInternalSearch(next);
    onSearchChange?.(next);
  };

  const handleOpenChange = (next: boolean) => {
    setOpen(next);
    if (!next) {
      handleSearchChange("");
      setActiveIndex(0);
    }
  };

  const selectOption = (option: TOption) => {
    if (option.disabled) return;
    onChange(option.value, option);
    handleOpenChange(false);
  };

  React.useEffect(() => {
    if (loadingMode !== "infinite" || !open || !hasNextPage || !onLoadMore) return;
    const target = sentinelRef.current;
    const root = listRef.current;
    if (!target || !root) return;

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0]?.isIntersecting && !isFetchingMore) onLoadMore();
      },
      { root, threshold: 0.1 },
    );
    observer.observe(target);
    return () => observer.disconnect();
  }, [loadingMode, open, hasNextPage, onLoadMore, isFetchingMore]);

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setActiveIndex((i) => Math.min(i + 1, filteredOptions.length - 1));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setActiveIndex((i) => Math.max(i - 1, 0));
    } else if (e.key === "Enter") {
      e.preventDefault();
      const option = filteredOptions[activeIndex];
      if (option) selectOption(option);
    } else if (e.key === "Escape") {
      handleOpenChange(false);
    }
  };

  const showLoading = isLoading && filteredOptions.length === 0;

  return (
    <PopoverPrimitive.Root open={open} onOpenChange={handleOpenChange}>
      <PopoverPrimitive.Trigger asChild>
        <button
          type="button"
          disabled={disabled}
          className={cn(
            "h-9 flex w-full items-center justify-between whitespace-nowrap rounded-lg border border-gray-200 bg-white px-3 py-1 text-sm text-gray-700 ring-offset-background focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary disabled:cursor-not-allowed disabled:opacity-50",
            error && "border-red-300",
            className,
          )}
        >
          <span className={cn("truncate", !selected && "text-gray-400")}>
            {selected ? selected.label : placeholder}
          </span>
          <ChevronDown className="h-4 w-4 shrink-0 opacity-50" />
        </button>
      </PopoverPrimitive.Trigger>
      <PopoverPrimitive.Portal>
        <PopoverPrimitive.Content
          align="start"
          sideOffset={4}
          className={cn(
            "z-50 w-[var(--radix-popover-trigger-width)] rounded-lg border border-gray-200 bg-white text-gray-700 shadow-md data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95",
            contentClassName,
          )}
        >
          <div className="p-2">
            <input
              autoFocus
              value={search}
              onChange={(e) => handleSearchChange(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder={searchPlaceholder}
              role="combobox"
              aria-expanded={open}
              className="w-full border border-gray-300 rounded px-2 py-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
            />
          </div>

          <div ref={listRef} role="listbox" className="max-h-64 overflow-auto p-1">
            {showLoading ? (
              <div className="flex items-center gap-2 p-3 text-sm text-gray-500">
                <Loader2 className="h-4 w-4 animate-spin" />
                {loadingText}
              </div>
            ) : filteredOptions.length > 0 ? (
              <>
                {filteredOptions.map((option, index) => (
                  <button
                    key={option.value}
                    type="button"
                    role="option"
                    aria-selected={option.value === value}
                    disabled={option.disabled}
                    onClick={() => selectOption(option)}
                    onMouseEnter={() => setActiveIndex(index)}
                    className={cn(
                      "relative flex w-full cursor-default select-none items-center rounded-md py-1.5 pl-2 pr-8 text-sm text-left outline-none",
                      index === activeIndex && "bg-gray-50",
                      option.disabled && "cursor-not-allowed opacity-50",
                    )}
                  >
                    {renderOption ? (
                      renderOption(option, {
                        selected: option.value === value,
                        active: index === activeIndex,
                        search,
                      })
                    ) : (
                      <span className="truncate">
                        {highlightMatch ? highlightLabel(option.label, search) : option.label}
                      </span>
                    )}
                    {option.value === value && (
                      <span className="absolute right-2 flex h-3.5 w-3.5 items-center justify-center">
                        <Check className="h-4 w-4 text-primary" />
                      </span>
                    )}
                  </button>
                ))}
                {loadingMode === "infinite" && hasNextPage && (
                  <div ref={sentinelRef} className="flex items-center justify-center py-2">
                    {isFetchingMore && (
                      <Loader2 className="h-4 w-4 animate-spin text-gray-400" />
                    )}
                  </div>
                )}
              </>
            ) : (
              <div className="p-3 text-sm text-gray-500">{emptyText}</div>
            )}
          </div>

          {createAction && (
            <button
              type="button"
              onClick={() => {
                handleOpenChange(false);
                createAction.onSelect();
              }}
              className="flex w-full items-center gap-1.5 text-left px-3 py-2 text-sm font-semibold text-primary border-t border-gray-100 hover:bg-gray-50"
            >
              {createAction.icon ?? <Plus className="h-3.5 w-3.5" />}
              {createAction.label}
            </button>
          )}
        </PopoverPrimitive.Content>
      </PopoverPrimitive.Portal>
    </PopoverPrimitive.Root>
  );
}
