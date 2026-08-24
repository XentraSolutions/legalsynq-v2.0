"use client";

import * as React from "react";
import * as PopoverPrimitive from "@radix-ui/react-popover";
import { Check, ChevronDown, Plus, X } from "lucide-react";
import { cn } from "@/lib/utils";

export interface ComboboxOption {
  value: string;
  label: string;
}

export interface ComboboxCreateAction {
  /** Label shown in the trigger row, e.g. "Add New Role". */
  label: string;
  /** Opens the caller's own create form/modal. Combobox does not render it. */
  onSelect: () => void;
  icon?: React.ReactNode;
}

interface ComboboxProps {
  value?: string | null;
  onChange: (value: string) => void;
  options: ComboboxOption[];
  placeholder?: string;
  searchPlaceholder?: string;
  emptyText?: string;
  disabled?: boolean;
  error?: boolean;
  footer?: React.ReactNode;
  /** Renders a "+ Add …" row below the option list that hands off to the caller. */
  createAction?: ComboboxCreateAction;
  className?: string;
  /** Shows a clear (X) button when a value is selected. */
  clearable?: boolean;
  // Fired on every keystroke in the search input, in addition to the
  // built-in client-side filtering — lets a parent drive a server-side
  // search (e.g. debounced) while `options` is still filtered locally.
  onSearchChange?: (search: string) => void;
}

export function Combobox({
  value,
  onChange,
  options,
  placeholder = "Select…",
  searchPlaceholder = "Search...",
  emptyText = "No options found.",
  disabled,
  error,
  footer,
  createAction,
  className,
  clearable,
  onSearchChange,
}: ComboboxProps) {
  const [open, setOpen] = React.useState(false);
  const [search, setSearch] = React.useState("");

  const selected = options.find((o) => o.value === value);

  const filteredOptions = React.useMemo(() => {
    const query = search.trim().toLowerCase();
    if (!query) return options;
    return options.filter((o) => o.label.toLowerCase().includes(query));
  }, [options, search]);

  return (
    <PopoverPrimitive.Root
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (!next) {
          setSearch("");
          onSearchChange?.("");
        }
      }}
    >
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
          <span className="flex items-center gap-1 shrink-0">
            {clearable && selected && !disabled && (
              <X
                className="h-3.5 w-3.5 text-gray-400 hover:text-gray-600"
                onClick={(e) => {
                  e.stopPropagation();
                  onChange("");
                }}
              />
            )}
            <ChevronDown className="h-4 w-4 opacity-50" />
          </span>
        </button>
      </PopoverPrimitive.Trigger>
      <PopoverPrimitive.Portal>
        <PopoverPrimitive.Content
          align="start"
          sideOffset={4}
          className="z-50 w-[var(--radix-popover-trigger-width)] rounded-lg border border-gray-200 bg-white text-gray-700 shadow-md data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95"
        >
          <div className="p-2">
            <input
              autoFocus
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                onSearchChange?.(e.target.value);
              }}
              placeholder={searchPlaceholder}
              className="w-full border border-gray-300 rounded px-2 py-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
            />
          </div>
          <div className="max-h-64 overflow-auto p-1">
            {filteredOptions.length > 0 ? (
              filteredOptions.map((option) => (
                <button
                  key={option.value}
                  type="button"
                  onClick={() => {
                    onChange(option.value);
                    setOpen(false);
                    setSearch("");
                  }}
                  className="relative flex w-full cursor-default select-none items-center rounded-md py-1.5 pl-2 pr-8 text-sm text-left outline-none hover:bg-gray-50 focus:bg-gray-50"
                >
                  <span className="truncate">{option.label}</span>
                  {option.value === value && (
                    <span className="absolute right-2 flex h-3.5 w-3.5 items-center justify-center">
                      <Check className="h-4 w-4 text-primary" />
                    </span>
                  )}
                </button>
              ))
            ) : (
              <div className="p-3 text-sm text-gray-500">{emptyText}</div>
            )}
          </div>
          {createAction && (
            <button
              type="button"
              onClick={() => {
                setOpen(false);
                setSearch("");
                createAction.onSelect();
              }}
              className="flex w-full items-center gap-1.5 text-left px-3 py-2 text-sm font-semibold text-primary border-t border-gray-100 hover:bg-gray-50"
            >
              {createAction.icon ?? <Plus className="h-3.5 w-3.5" />}
              {createAction.label}
            </button>
          )}
          {footer}
        </PopoverPrimitive.Content>
      </PopoverPrimitive.Portal>
    </PopoverPrimitive.Root>
  );
}
