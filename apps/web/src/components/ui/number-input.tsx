import React, { useId } from "react";

export interface NumberInputProps
  extends Omit<
    React.InputHTMLAttributes<HTMLInputElement>,
    "onChange" | "value" | "type" | "prefix"
  > {
  label?: string;
  error?: string;
  value: string | number | null | undefined; // Unformatted numeric string, e.g. "1234.5"
  onValueChange: (rawValue: string) => void; // Emits the unformatted numeric string back up
  prefix?: React.ReactNode;
  suffix?: React.ReactNode;
}

// Strips everything but digits and a single decimal point, e.g. "1,234.5.6" -> "1234.56"
function toRawValue(input: string): string {
  const cleaned = input.replace(/[^\d.]/g, "");
  const [whole, ...rest] = cleaned.split(".");
  return rest.length > 0 ? `${whole}.${rest.join("")}` : whole;
}

// Groups the integer part with thousands separators as the user types, e.g. "1234.5" -> "1,234.5"
function toDisplayValue(raw: string): string {
  if (!raw) return "";
  const [whole, decimal] = raw.split(".");
  const groupedWhole = whole.replace(/\B(?=(\d{3})+(?!\d))/g, ",");
  return decimal !== undefined ? `${groupedWhole}.${decimal}` : groupedWhole;
}

export function NumberInput({
  label,
  error,
  value,
  onValueChange,
  className = "",
  prefix,
  suffix,
  ...props
}: NumberInputProps) {
  const generatedId = useId();
  const inputId = props.id || generatedId;

  const rawValue = value === null || value === undefined ? "" : String(value);
  const displayValue = toDisplayValue(rawValue);
  const hasAdornment = Boolean(prefix || suffix);

  return (
    <div className="flex flex-col gap-1.5 w-full">
      {label && (
        <label
          htmlFor={inputId}
          className="text-sm font-medium text-gray-700 dark:text-gray-200"
        >
          {label}
        </label>
      )}

      <div className={hasAdornment ? "relative" : ""}>
        {prefix && (
          <span className="absolute left-3 top-1/2 -translate-y-1/2 text-sm text-gray-400 pointer-events-none">
            {prefix}
          </span>
        )}

        <input
          {...props}
          id={inputId}
          type="text"
          inputMode="decimal"
          value={displayValue}
          onChange={(e) => onValueChange(toRawValue(e.target.value))}
          aria-invalid={!!error}
          className={`w-full h-9 rounded-lg border border-gray-200 bg-white ${prefix ? "pl-9" : "px-3"} ${suffix ? "pr-9" : ""} py-1 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-colors disabled:cursor-not-allowed disabled:opacity-50 aria-[invalid=true]:border-red-300 ${className}`}
        />

        {suffix && (
          <span className="absolute right-3 top-1/2 -translate-y-1/2 text-sm text-gray-400 pointer-events-none">
            {suffix}
          </span>
        )}
      </div>

      {error && (
        <span className="text-xs font-medium text-red-500 mt-0.5">
          {error}
        </span>
      )}
    </div>
  );
}
