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
  /** Opt-in currency mode: strips leading zeros and caps the decimal part to this many digits, e.g. 2 for cents. Omit to keep the default unrestricted masking. */
  maxDecimals?: number;
}

// Strips everything but digits and a single decimal point, e.g. "1,234.5.6" -> "1234.56"
// When maxDecimals is set, also strips leading zeros ("0005" -> "5") and caps the decimal part.
function toRawValue(input: string, maxDecimals?: number): string {
  const cleaned = input.replace(/[^\d.]/g, "");
  const [wholeRaw, ...rest] = cleaned.split(".");
  const whole = maxDecimals !== undefined ? wholeRaw.replace(/^0+(?=\d)/, "") : wholeRaw;
  if (rest.length === 0) {
    return whole;
  }
  const decimal =
    maxDecimals !== undefined ? rest.join("").slice(0, maxDecimals) : rest.join("");
  return `${whole}.${decimal}`;
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
  maxDecimals,
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
          onChange={(e) => onValueChange(toRawValue(e.target.value, maxDecimals))}
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
