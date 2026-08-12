import React, { useId } from "react";
import { useMask } from "@react-input/mask";

export interface PhoneInputProps extends Omit<
  React.InputHTMLAttributes<HTMLInputElement>,
  "onChange"
> {
  label?: string;
  error?: string;
  value: string; // The parent passes the formatted string down
  onValueChange: (formattedValue: string) => void; // Pass the formatted string back up
}

export function PhoneInput({
  label,
  error,
  value,
  onValueChange,
  className = "",
  placeholder = "(000) 000-0000",
  ...props
}: PhoneInputProps) {
  const generatedId = useId();
  const inputId = props.id || generatedId;

  const phoneRef = useMask({
    mask: "(___) ___-____",
    replacement: { _: /\d/ },
    showMask: true,
  });

  return (
    <div className="flex flex-col gap-1.5 w-full max-w-sm">
      {label && (
        <label
          htmlFor={inputId}
          className="text-sm font-medium text-gray-700 dark:text-gray-200"
        >
          {label}
        </label>
      )}

      <input
        {...props}
        id={inputId}
        type="tel"
        ref={phoneRef}
        value={value}
        onChange={(e) => onValueChange(e.target.value)}
        placeholder={placeholder}
        aria-invalid={!!error}
        className={`flex h-10 w-full rounded-md border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent aria-[invalid=true]:border-red-500 aria-[invalid=true]:focus:ring-red-500 ${className}`}
      />

      {error && (
        <span className="text-xs font-medium text-red-500 mt-0.5">{error}</span>
      )}
    </div>
  );
}
