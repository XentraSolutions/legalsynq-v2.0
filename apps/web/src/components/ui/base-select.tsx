"use client";

import * as React from "react";
import * as PopoverPrimitive from "@radix-ui/react-popover";
import { Check, ChevronDown, Plus, X } from "lucide-react";
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

interface BaseSelectCommonProps<
  TOption extends BaseSelectOption = BaseSelectOption,
> {
  options: TOption[];

  loadingMode?: "eager" | "infinite";
  isLoading?: boolean;
  /** A new server-side search request is in flight — shows the same skeleton as `isLoading`, even while stale results from the previous search are still in `options` (e.g. via `keepPreviousData`). */
  isSearching?: boolean;
  isFetchingMore?: boolean;
  hasNextPage?: boolean;
  onLoadMore?: () => void;

  search?: string;
  onSearchChange?: (search: string) => void;
  filterLocally?: boolean;
  highlightMatch?: boolean;

  renderOption?: (
    option: TOption,
    state: BaseSelectOptionState,
  ) => React.ReactNode;

  createAction?: BaseSelectCreateAction;

  /** Render each row with a leading checkbox instead of the trailing check icon. */
  showCheckboxes?: boolean;
  /** Allow clearing the current selection from the trigger. */
  clearable?: boolean;
  /**
   * Render the search input + option list directly in the page flow
   * (always visible, no trigger/popover) — for filter panels where the
   * list should stay open, e.g. inside a filter modal.
   */
  inline?: boolean;

  placeholder?: string;
  searchPlaceholder?: string;
  emptyText?: string;

  disabled?: boolean;
  error?: boolean;
  className?: string;
  contentClassName?: string;
  onOpen?: () => void;
  /**
   * Replaces the trigger's default "selected label / placeholder + chevron"
   * content — for a trigger that should look like an action button (e.g. an
   * icon + label) instead of the default combobox chrome. The popover/select
   * behavior is unchanged; only the trigger's visible content is swapped.
   * Pass a render function to still show the current selection and a clear
   * control inside the custom layout.
   */
  triggerContent?:
    | React.ReactNode
    | ((ctx: {
        selectedLabel?: string;
        clearable: boolean;
        onClear: (event: React.MouseEvent | React.KeyboardEvent) => void;
      }) => React.ReactNode);
}

type SingleSelectProps<TOption extends BaseSelectOption> = {
  multiple?: false;
  value?: string | null;
  onChange: (value: string, option: TOption | null) => void;
};

type MultiSelectProps<TOption extends BaseSelectOption> = {
  multiple: true;
  value?: string[];
  onChange: (values: string[], options: TOption[]) => void;
};

export type BaseSelectProps<
  TOption extends BaseSelectOption = BaseSelectOption,
> = BaseSelectCommonProps<TOption> &
  (SingleSelectProps<TOption> | MultiSelectProps<TOption>);

/** Placeholder rows shown in place of real options while loading or searching. */
function OptionSkeletonRows({ count }: { count: number }) {
  return (
    <div aria-hidden="true">
      {Array.from({ length: count }).map((_, i) => (
        <div key={i} className="flex items-center py-1.5 pl-2 pr-8">
          <div
            className="h-4 rounded bg-gray-100 animate-pulse"
            style={{ width: `${60 + ((i * 17) % 30)}%` }}
          />
        </div>
      ))}
    </div>
  );
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
 * - Checkbox-style rows via `showCheckboxes`, and an always-visible
 *   `inline` mode (search + list in the page flow, no popover) for
 *   filter panels.
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
export function BaseSelect<TOption extends BaseSelectOption = BaseSelectOption>(
  props: BaseSelectProps<TOption>,
) {
  const {
    multiple,
    options,
    loadingMode = "eager",
    isLoading = false,
    isSearching = false,
    isFetchingMore = false,
    hasNextPage = false,
    onLoadMore,
    search: controlledSearch,
    onSearchChange,
    filterLocally = true,
    highlightMatch = true,
    renderOption,
    createAction,
    showCheckboxes = false,
    clearable = false,
    inline = false,
    placeholder = "Select…",
    searchPlaceholder = "Search...",
    emptyText = "No options found.",
    disabled,
    error,
    className,
    contentClassName,
    onOpen,
    triggerContent,
  } = props;
  const [open, setOpen] = React.useState(false);
  const [internalSearch, setInternalSearch] = React.useState("");
  const [activeIndex, setActiveIndex] = React.useState(0);

  const search = controlledSearch ?? internalSearch;
  const listRef = React.useRef<HTMLDivElement>(null);
  // A plain ref here wouldn't work: Radix's Popover.Content mounts its
  // children (including this sentinel) one render after `open` flips true,
  // and ref writes don't trigger re-renders — so the infinite-scroll effect
  // below would see `sentinelEl` as null forever and never attach its
  // IntersectionObserver. Using state for the ref makes that mount a
  // dependency the effect can react to.
  const [sentinelEl, setSentinelEl] = React.useState<HTMLDivElement | null>(
    null,
  );

  const selectedValues = React.useMemo(
    () =>
      new Set(
        multiple ? (props.value ?? []) : props.value ? [props.value] : [],
      ),
    [multiple, props.value],
  );

  const selected = React.useMemo(
    () =>
      multiple
        ? options.filter((o) => selectedValues.has(o.value))
        : options.find((o) => o.value === props.value),
    [multiple, props.value, options, selectedValues],
  );

  const selectedOptions = React.useMemo(
    () => options.filter((o) => selectedValues.has(o.value)),
    [options, selectedValues],
  );

  const selectedOption = React.useMemo(
    () => options.find((o) => o.value === props.value),
    [options, props.value],
  );

  const selectedLabel = props.multiple
    ? selectedOptions.map((o) => o.label).join(", ")
    : selectedOption?.label;

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
    if (controlledSearch === undefined) {
      setInternalSearch(next);
    }

    onSearchChange?.(next);
  };

  const handleOpenChange = (next: boolean) => {
    setOpen(next);
    onOpen?.();

    if (!next) {
      handleSearchChange("");
      setActiveIndex(0);
    }
  };

  const handleClear = (event: React.MouseEvent | React.KeyboardEvent) => {
    event.stopPropagation();

    if (props.multiple) {
      props.onChange([], []);
      return;
    }

    props.onChange("", selectedOption ?? null);
  };

  const isSelected = React.useCallback(
    (option: TOption) => selectedValues.has(option.value),
    [selectedValues],
  );

  const selectOption = (option: TOption) => {
    if (option.disabled) return;

    if (props.multiple) {
      const current = props.value ?? [];

      const nextValues = current.includes(option.value)
        ? current.filter((v) => v !== option.value)
        : [...current, option.value];

      const nextOptions = options.filter((o) => nextValues.includes(o.value));

      props.onChange(nextValues, nextOptions);
      return;
    }

    props.onChange(option.value, option);
    handleOpenChange(false);
  };

  const listVisible = inline || open;

  React.useEffect(() => {
    if (
      loadingMode !== "infinite" ||
      !listVisible ||
      !hasNextPage ||
      !onLoadMore
    )
      return;
    const root = listRef.current;
    if (!sentinelEl || !root) return;

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0]?.isIntersecting && !isFetchingMore) onLoadMore();
      },
      { root, threshold: 0.1 },
    );
    observer.observe(sentinelEl);
    return () => observer.disconnect();
  }, [
    loadingMode,
    listVisible,
    hasNextPage,
    onLoadMore,
    isFetchingMore,
    sentinelEl,
  ]);

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

  const showSkeleton =
    isSearching || (isLoading && filteredOptions.length === 0);

  const searchInput = (
    <input
      autoFocus={!inline}
      value={search}
      onChange={(e) => handleSearchChange(e.target.value)}
      onKeyDown={handleKeyDown}
      placeholder={searchPlaceholder}
      role="combobox"
      aria-expanded={listVisible}
      className="w-full border border-gray-300 rounded px-2 py-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
    />
  );

  const optionList = (
    <div
      ref={listRef}
      role="listbox"
      className={cn("overflow-auto p-1", inline ? "max-h-44" : "max-h-64")}
    >
      {showSkeleton ? (
        <OptionSkeletonRows count={3} />
      ) : filteredOptions.length > 0 ? (
        <>
          {filteredOptions.map((option, index) => (
            <button
              key={option.value}
              type="button"
              role="option"
              aria-selected={isSelected(option)}
              disabled={option.disabled}
              onClick={() => selectOption(option)}
              onMouseEnter={() => setActiveIndex(index)}
              className={cn(
                "relative flex w-full cursor-default select-none items-center rounded-md py-1.5 pl-2 text-sm text-left outline-none",
                showCheckboxes ? "pr-2" : "pr-8",
                index === activeIndex && "bg-gray-50",
                option.disabled && "cursor-not-allowed opacity-50",
              )}
            >
              {showCheckboxes && (
                <span
                  aria-hidden
                  className={cn(
                    "mr-2 flex h-4 w-4 shrink-0 items-center justify-center rounded border",
                    isSelected(option)
                      ? "bg-primary border-primary text-white"
                      : "bg-white border-gray-300",
                  )}
                >
                  {isSelected(option) && <Check className="h-3 w-3" />}
                </span>
              )}

              {renderOption ? (
                renderOption(option, {
                  selected: isSelected(option),
                  active: index === activeIndex,
                  search,
                })
              ) : (
                <span className="truncate whitespace-pre-line">
                  {highlightMatch
                    ? highlightLabel(option.label, search)
                    : option.label}
                </span>
              )}

              {!showCheckboxes && isSelected(option) && (
                <span className="absolute right-2 flex h-3.5 w-3.5 items-center justify-center">
                  <Check className="h-4 w-4 text-primary" />
                </span>
              )}
            </button>
          ))}
          {loadingMode === "infinite" && hasNextPage && (
            // Needs a non-zero height at all times, even while idle — an
            // empty (0-height) element always has an IntersectionObserver
            // ratio of 0, so with threshold: 0.1 it would never be reported
            // as intersecting and onLoadMore would never fire.
            <div ref={setSentinelEl} className="min-h-[1px]">
              {isFetchingMore && <OptionSkeletonRows count={1} />}
            </div>
          )}
        </>
      ) : (
        <div className="p-3 text-sm text-gray-500">{emptyText}</div>
      )}
    </div>
  );

  const createButton = createAction && (
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
  );

  if (inline) {
    return (
      <div className={className}>
        <div className="mb-1.5">{searchInput}</div>
        <div
          className={cn(
            "rounded-lg border border-gray-200 bg-white",
            error && "border-red-300",
            contentClassName,
          )}
        >
          {optionList}
          {createButton}
        </div>
      </div>
    );
  }

  return (
    <PopoverPrimitive.Root open={open} onOpenChange={handleOpenChange}>
      <PopoverPrimitive.Trigger asChild>
        <button
          type="button"
          disabled={disabled}
          className={cn(
            triggerContent
              ? "inline-flex items-center whitespace-nowrap rounded-lg text-sm ring-offset-background focus:outline-none focus:ring-2 focus:ring-primary/20 disabled:cursor-not-allowed disabled:opacity-50"
              : "h-9 flex w-full items-center justify-between whitespace-nowrap rounded-lg border border-gray-200 bg-white px-3 py-1 text-sm text-gray-700 ring-offset-background focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary disabled:cursor-not-allowed disabled:opacity-50",
            error && "border-red-300",
            className,
          )}
        >
          {typeof triggerContent === "function"
            ? triggerContent({ selectedLabel, clearable, onClear: handleClear })
            : (triggerContent ?? (
                <>
                  <span className={cn("truncate", !selectedLabel && "text-gray-400")}>
                    {selectedLabel || placeholder}
                  </span>
                  <span className="flex items-center gap-1 shrink-0">
                    {clearable && !disabled && ((props.multiple && selectedOptions.length > 0) || (!props.multiple && Boolean(selectedLabel))) && (
                      <X
                        aria-label="Clear selection"
                        className="h-3.5 w-3.5 text-gray-400 hover:text-gray-600"
                        onClick={handleClear}
                      />
                    )}
                    <ChevronDown className="h-4 w-4 opacity-50" />
                  </span>
                </>
              ))}
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
          <div className="p-2">{searchInput}</div>
          {optionList}
          {createButton}
        </PopoverPrimitive.Content>
      </PopoverPrimitive.Portal>
    </PopoverPrimitive.Root>
  );
}
