"use client";

import {
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";

export type MultiSelectOption = {
  label: string;
  value: string;
};

type MultiSelectProps = {
  options: MultiSelectOption[];
  value: string[];
  onChange: (value: string[]) => void;

  label?: string;
  placeholder?: string;
  searchPlaceholder?: string;
  disabled?: boolean;
};

export default function MultiSelect({
  options,
  value,
  onChange,
  label,
  placeholder = "Select options",
  searchPlaceholder = "Search...",
  disabled = false,
}: MultiSelectProps) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");

  const containerRef = useRef<HTMLDivElement>(null);

  // Close on outside click
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (
        containerRef.current &&
        !containerRef.current.contains(
          event.target as Node
        )
      ) {
        setOpen(false);
      }
    }

    document.addEventListener(
      "mousedown",
      handleClickOutside
    );

    return () => {
      document.removeEventListener(
        "mousedown",
        handleClickOutside
      );
    };
  }, []);

  const filteredOptions = useMemo(() => {
    return options.filter((option) =>
      option.label
        .toLowerCase()
        .includes(search.toLowerCase())
    );
  }, [options, search]);

  const toggleOption = (optionValue: string) => {
    if (value.includes(optionValue)) {
      onChange(
        value.filter((item) => item !== optionValue)
      );
    } else {
      onChange([...value, optionValue]);
    }
  };

  const selectedOptions = options.filter((option) =>
    value.includes(option.value)
  );

  return (
    <div
      ref={containerRef}
      className="relative w-full"
    >
      {label && (
        <label className="mb-2 block text-sm font-medium">
          {label}
        </label>
      )}

      {/* Select Button */}
      <button
        type="button"
        disabled={disabled}
        onClick={() => setOpen((prev) => !prev)}
        className="flex min-h-[44px] w-full flex-wrap items-center gap-2 rounded-xl border border-gray-300 bg-white px-3 py-2 text-left"
      >
        {selectedOptions.length > 0 ? (
          selectedOptions.map((item) => (
            <span
              key={item.value}
              className="rounded-full bg-gray-100 px-2 py-1 text-sm"
            >
              {item.label}
            </span>
          ))
        ) : (
          <span className="text-sm text-gray-400">
            {placeholder}
          </span>
        )}
      </button>

      {/* Dropdown */}
      {open && (
        <div className="absolute z-50 mt-2 w-full rounded-xl border border-gray-200 bg-white shadow-lg">
          {/* Search */}
          <div className="border-b p-2">
            <input
              type="text"
              placeholder={searchPlaceholder}
              value={search}
              onChange={(e) =>
                setSearch(e.target.value)
              }
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-black"
            />
          </div>

          {/* Options */}
          <div className="max-h-64 overflow-y-auto">
            {filteredOptions.length > 0 ? (
              filteredOptions.map((option) => {
                const checked = value.includes(
                  option.value
                );

                return (
                  <label
                    key={option.value}
                    className="flex cursor-pointer items-center gap-3 px-4 py-3 hover:bg-gray-50"
                  >
                    <input
                      type="checkbox"
                      checked={checked}
                      onChange={() =>
                        toggleOption(option.value)
                      }
                    />

                    <span className="text-sm">
                      {option.label}
                    </span>
                  </label>
                );
              })
            ) : (
              <div className="p-4 text-sm text-gray-500">
                No results found
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}