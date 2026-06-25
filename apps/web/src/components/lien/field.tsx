import { useEffect, useRef, useState } from "react";

export default function Field({
  label,
  value,
  onChange,
  error,
  placeholder,
  type = "text",
  required,
  options,
  optionValueKey,
  optionLabelKey,
  multiple,
  isChecked,
  children,
}: {
  label: string;
  value: string | string[] | null;
  onChange: (v: string | string[] | boolean) => void;
  error?: string;
  placeholder?: string;
  type?: string;
  required?: boolean;
  options?: any[];
  optionValueKey?: string;
  optionLabelKey?: string;
  multiple?: boolean;
  isChecked?: boolean;
  children?: React.ReactNode;
}) {
  return (
    <div
      className={
        type == "checkbox" ? "flex items-center gap-3 cursor-pointer" : ""
      }
    >
      <label className="block text-sm font-medium text-gray-700 mb-1">
        {label}
        {required && <span className="text-red-500 ml-0.5">*</span>}
      </label>
      {type === "textarea" ? (
        <textarea
          value={value ?? ""}
          onChange={(e) => onChange(e.target.value)}
          placeholder={placeholder}
          className={`w-full border rounded-lg px-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary ${error ? "border-red-300" : "border-gray-200"}`}
          children={children}
        />
      ) : type === "select" ? (
        <SelectField
          label={label}
          value={value ?? ""}
          onChange={onChange}
          placeholder={placeholder}
          error={error}
          options={options}
          optionValueKey={optionValueKey}
          optionLabelKey={optionLabelKey}
          multiple={multiple}
          children={children}
        />
      ) : type === "checkbox" ? (
        <input
          type="checkbox"
          value={value ?? ""}
          checked={isChecked}
          onChange={(e) => onChange(e.target.checked)}
          placeholder={placeholder}
          className="w-4 h-4 rounded border-gray-300 text-primary focus:ring-primary mb-1"
          children={children}
        />
      ) : (
        <input
          type={type}
          value={value ?? ""}
          onChange={(e) => onChange(e.target.value)}
          placeholder={placeholder}
          className={`w-full border rounded-lg px-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary ${error ? "border-red-300" : "border-gray-200"}`}
        />
      )}
      {error && <p className="text-xs text-red-500 mt-1">{error}</p>}
    </div>
  );
}

// Helper select field used by Field when `type === 'select'`.
function SelectField({
  value,
  onChange,
  placeholder,
  error,
  options,
  optionValueKey = "value",
  optionLabelKey = "label",
  multiple,
  children,
}: {
  label: string;
  value: string | string[] | null;
  onChange: (v: string | string[]) => void;
  placeholder?: string;
  error?: string;
  options?: Array<{ key: string; value: string; label: string }>;
  optionValueKey?: string;
  optionLabelKey?: string;
  multiple?: boolean;
  children?: React.ReactNode;
}) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const opts: any[] = (options ?? (window as any).__FIELD_OPTIONS__) || [];
  const selectedValues = Array.isArray(value) ? value : value ? [value] : [];

  useEffect(() => {
    const onClickOutside = (event: MouseEvent) => {
      if (
        containerRef.current &&
        !containerRef.current.contains(event.target as Node)
      ) {
        setOpen(false);
      }
    };

    document.addEventListener("mousedown", onClickOutside);
    return () => document.removeEventListener("mousedown", onClickOutside);
  }, []);

  const getOptionLabel = (optionValue: string) => {
    const selected = opts.find(
      (o) => String(o[optionValueKey] ?? "") === optionValue,
    );
    return String(selected?.[optionLabelKey] ?? optionValue);
  };

  const displayLabel = () => {
    if (multiple) {
      if (selectedValues.length === 0) return placeholder ?? "Select…";
      if (selectedValues.length === 1) return getOptionLabel(selectedValues[0]);
      return `${selectedValues.length} selected`;
    }

    if (!selectedValues.length) return placeholder ?? "Select…";
    return getOptionLabel(selectedValues[0]);
  };

  const filteredOptions = opts?.filter((option) => {
    const label = String(option[optionLabelKey] ?? "").toLowerCase();
    const valueText = String(option[optionValueKey] ?? "").toLowerCase();
    const keyText = String(option.key ?? "").toLowerCase();
    const searchValue = search.toLowerCase();

    return (
      label.includes(searchValue) ||
      valueText.includes(searchValue) ||
      keyText.includes(searchValue)
    );
  });

  const toggleOption = (optionValue: string) => {
    if (!multiple) {
      onChange(optionValue);
      setOpen(false);
      return;
    }

    const next = selectedValues.includes(optionValue)
      ? selectedValues.filter((value) => value !== optionValue)
      : [...selectedValues, optionValue];
    onChange(next);
  };

  return (
    <div className="relative" ref={containerRef}>
      <button
        type="button"
        onClick={() => setOpen((current) => !current)}
        className={`w-full text-left border rounded-lg px-3 py-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary ${error ? "border-red-300" : "border-gray-200"}`}
      >
        <div className="flex items-center justify-between gap-2">
          <span
            className={`truncate ${selectedValues.length ? "text-gray-900" : "text-gray-400"}`}
          >
            {displayLabel()}
          </span>
          <span className="text-xs text-gray-500">{open ? "▲" : "▼"}</span>
        </div>
      </button>

      {open && (
        <div className="absolute z-50 mt-2 w-full rounded-lg border border-gray-200 bg-white shadow-lg">
          <div className="p-2">
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search..."
              className="w-full border border-gray-300 rounded px-2 py-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
            />
          </div>

          <div className="max-h-64 overflow-auto">
            {filteredOptions.length > 0 ? (
              filteredOptions.map((option) => {
                const optionValue = String(option[optionValueKey] ?? "");
                const optionLabel = String(
                  option[optionLabelKey] ?? optionValue,
                );
                const checked = selectedValues.includes(optionValue);

                return (
                  <label
                    key={String(option.key ?? optionValue)}
                    className="flex cursor-pointer items-center gap-2 px-3 py-2 text-sm text-gray-700 hover:bg-gray-50"
                  >
                    <input
                      type={multiple ? "checkbox" : "radio"}
                      checked={checked}
                      onChange={() => toggleOption(optionValue)}
                      className="h-4 w-4 rounded border-gray-300 text-primary focus:ring-primary"
                    />
                    <div className="min-w-0 truncate">
                      <div>{optionLabel}</div>
                    </div>
                  </label>
                );
              })
            ) : (
              <div className="p-3 text-sm text-gray-500">No options found.</div>
            )}
          </div>
          {children}
        </div>
      )}
    </div>
  );
}
