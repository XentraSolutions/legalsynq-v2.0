"use client";

import * as React from "react";
import * as PopoverPrimitive from "@radix-ui/react-popover";
import { useMask } from "@react-input/mask";
import { DayPicker, Matcher } from "react-day-picker";
import { ChevronLeft, ChevronRight, X } from "lucide-react";
import { cn } from "@/lib/utils";

import "react-day-picker/style.css";

interface DatePickerProps {
  value?: string;
  onChange?: (value: string) => void;
  placeholder?: string;
  className?: string;
  disabled?: boolean;
  maxDate?: Date | null;
  minDate?: Date | null;
  disableFutureDates?: boolean;
  clearable?: boolean;
}

function parseDate(value?: string): Date | undefined {
  if (!value) return undefined;
  const d = new Date(value + "T00:00:00");
  return isNaN(d.getTime()) ? undefined : d;
}

function formatDate(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

function displayDate(date: Date): string {
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  const y = date.getFullYear();
  return `${m}/${d}/${y}`;
}

const MONTH_NAMES = [
  "January",
  "February",
  "March",
  "April",
  "May",
  "June",
  "July",
  "August",
  "September",
  "October",
  "November",
  "December",
];

function yearRange(centerYear: number, maxYear?: number, span = 100): number[] {
  const end = maxYear ?? centerYear + 10;
  const start = Math.min(centerYear, end) - span;
  const years: number[] = [];
  for (let y = end; y >= start; y--) years.push(y);
  return years;
}

function parseTypedDate(masked: string): Date | undefined {
  const digits = masked.replace(/\D/g, "");
  if (digits.length !== 8) return undefined;
  const mm = Number(digits.slice(0, 2));
  const dd = Number(digits.slice(2, 4));
  const yyyy = Number(digits.slice(4, 8));
  if (mm < 1 || mm > 12 || dd < 1) return undefined;
  return new Date(yyyy, mm - 1, dd);
}

export function DatePicker({
  value,
  onChange,
  placeholder = "Pick a date",
  className,
  disabled,
  maxDate,
  minDate,
  disableFutureDates,
  clearable = true,
}: DatePickerProps) {
  const effectiveMaxDate =
    maxDate !== undefined
      ? maxDate
      : disableFutureDates
        ? new Date()
        : undefined;
  const effectiveMinDate = minDate ?? undefined;
  const [open, setOpen] = React.useState(false);
  const selected = parseDate(value);
  const [month, setMonth] = React.useState<Date>(selected ?? new Date());
  const [inputText, setInputText] = React.useState(
    selected ? displayDate(selected) : "",
  );
  const isFocused = React.useRef(false);
  const anchorRef = React.useRef<HTMLDivElement>(null);
  const inputRef = React.useRef<HTMLInputElement | null>(null);

  const maskRef = useMask({
    mask: "__/__/____",
    replacement: { _: /\d/ },
  });

  const setInputRefs = React.useCallback(
    (node: HTMLInputElement | null) => {
      inputRef.current = node;
      maskRef.current = node as HTMLInputElement;
    },
    [maskRef],
  );

  React.useEffect(() => {
    if (isFocused.current) return;
    setInputText(selected ? displayDate(selected) : "");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value]);

  React.useEffect(() => {
    setMonth((m) => {
      if (
        effectiveMaxDate &&
        (m.getFullYear() > effectiveMaxDate.getFullYear() ||
          (m.getFullYear() === effectiveMaxDate.getFullYear() &&
            m.getMonth() > effectiveMaxDate.getMonth()))
      ) {
        return new Date(
          effectiveMaxDate.getFullYear(),
          effectiveMaxDate.getMonth(),
          1,
        );
      }
      if (
        effectiveMinDate &&
        (m.getFullYear() < effectiveMinDate.getFullYear() ||
          (m.getFullYear() === effectiveMinDate.getFullYear() &&
            m.getMonth() < effectiveMinDate.getMonth()))
      ) {
        return new Date(
          effectiveMinDate.getFullYear(),
          effectiveMinDate.getMonth(),
          1,
        );
      }
      return m;
    });
  }, [effectiveMaxDate, effectiveMinDate]);

  function isWithinBounds(date: Date) {
    if (effectiveMaxDate && date > effectiveMaxDate) return false;
    if (effectiveMinDate && date < effectiveMinDate) return false;
    return true;
  }

  function handleSelect(date: Date | undefined) {
    if (date) {
      onChange?.(formatDate(date));
      setInputText(displayDate(date));
      setOpen(false);
    }
  }

  function handleInputChange(e: React.ChangeEvent<HTMLInputElement>) {
    const text = e.target.value;
    setInputText(text);
    const parsed = parseTypedDate(text);
    if (parsed && isWithinBounds(parsed)) {
      onChange?.(formatDate(parsed));
      setMonth(parsed);
    }
  }

  function handleClear(e: React.MouseEvent) {
    e.stopPropagation();
    onChange?.("");
    setInputText("");
    inputRef.current?.focus();
  }

  function handleInputFocus() {
    isFocused.current = true;
    setOpen(true);
  }

  function handleInputBlur() {
    isFocused.current = false;
    const parsed = parseTypedDate(inputText);
    if (parsed && isWithinBounds(parsed)) {
      setInputText(displayDate(parsed));
    } else {
      setInputText(selected ? displayDate(selected) : "");
    }
  }

  function prevMonth() {
    setMonth((m) => new Date(m.getFullYear(), m.getMonth() - 1, 1));
  }

  function nextMonth() {
    setMonth((m) => {
      const next = new Date(m.getFullYear(), m.getMonth() + 1, 1);
      if (effectiveMaxDate && next > effectiveMaxDate) return m;
      return next;
    });
  }

  function handleMonthSelect(monthIndex: number) {
    setMonth((m) => {
      const maxMonth =
        effectiveMaxDate && m.getFullYear() === effectiveMaxDate.getFullYear()
          ? effectiveMaxDate.getMonth()
          : 11;
      return new Date(m.getFullYear(), Math.min(monthIndex, maxMonth), 1);
    });
  }

  function handleYearSelect(year: number) {
    setMonth((m) => {
      const maxMonth =
        effectiveMaxDate && year === effectiveMaxDate.getFullYear()
          ? effectiveMaxDate.getMonth()
          : 11;
      return new Date(year, Math.min(m.getMonth(), maxMonth), 1);
    });
  }

  const years = React.useMemo(
    () =>
      yearRange(
        (selected ?? new Date()).getFullYear(),
        effectiveMaxDate?.getFullYear(),
      ),
    [selected, effectiveMaxDate],
  );

  const disabledDays: Matcher[] | undefined = React.useMemo(() => {
    const matchers: Matcher[] = [];
    if (effectiveMaxDate) matchers.push({ after: effectiveMaxDate });
    if (effectiveMinDate) matchers.push({ before: effectiveMinDate });
    return matchers.length ? matchers : undefined;
  }, [effectiveMaxDate, effectiveMinDate]);
  return (
    <PopoverPrimitive.Root open={open} onOpenChange={setOpen}>
      <PopoverPrimitive.Anchor asChild>
        <div
          ref={anchorRef}
          className={cn(
            "flex h-9 w-full items-center gap-2 rounded-lg border border-gray-200 bg-white px-3 py-1 text-sm transition-colors focus-within:ring-2 focus-within:ring-primary/20 focus-within:border-primary",
            disabled && "cursor-not-allowed opacity-50",
            className,
          )}
        >
          <button
            type="button"
            disabled={disabled}
            tabIndex={-1}
            onClick={() => inputRef.current?.focus()}
            className="shrink-0"
          >
            <i className="ri-calendar-line text-gray-400 text-base" />
          </button>
          <input
            ref={setInputRefs}
            type="text"
            inputMode="numeric"
            disabled={disabled}
            placeholder={placeholder}
            value={inputText}
            onChange={handleInputChange}
            onFocus={handleInputFocus}
            onBlur={handleInputBlur}
            className="w-full bg-transparent text-sm text-gray-900 placeholder:text-gray-400 focus:outline-none disabled:cursor-not-allowed"
          />
          {clearable && !disabled && inputText && (
            <button
              type="button"
              tabIndex={-1}
              onClick={handleClear}
              className="shrink-0 text-gray-400 hover:text-gray-600"
            >
              <X className="h-3.5 w-3.5" />
            </button>
          )}
        </div>
      </PopoverPrimitive.Anchor>

      <PopoverPrimitive.Portal>
        <PopoverPrimitive.Content
          align="start"
          sideOffset={4}
          onOpenAutoFocus={(e) => e.preventDefault()}
          onInteractOutside={(e) => {
            if (
              anchorRef.current &&
              e.target instanceof Node &&
              anchorRef.current.contains(e.target)
            ) {
              e.preventDefault();
            }
          }}
          className="z-50 rounded-xl border border-gray-200 bg-white p-3 shadow-lg"
        >
          <div className="flex items-center justify-between gap-1 px-1 mb-2">
            <button
              type="button"
              onClick={prevMonth}
              className="h-7 w-7 shrink-0 rounded-md hover:bg-gray-100 flex items-center justify-center text-gray-600"
            >
              <ChevronLeft className="h-4 w-4" />
            </button>
            <div className="flex items-center gap-1">
              <select
                value={month.getMonth()}
                onChange={(e) => handleMonthSelect(Number(e.target.value))}
                className="text-sm font-medium text-gray-800 bg-transparent rounded-md px-1 py-0.5 hover:bg-gray-100 focus:outline-none focus:ring-2 focus:ring-primary/20"
              >
                {MONTH_NAMES.map((name, idx) => (
                  <option key={name} value={idx}>
                    {name}
                  </option>
                ))}
              </select>
              <select
                value={month.getFullYear()}
                onChange={(e) => handleYearSelect(Number(e.target.value))}
                className="text-sm font-medium text-gray-800 bg-transparent rounded-md px-1 py-0.5 hover:bg-gray-100 focus:outline-none focus:ring-2 focus:ring-primary/20"
              >
                {years.map((y) => (
                  <option key={y} value={y}>
                    {y}
                  </option>
                ))}
              </select>
            </div>
            <button
              type="button"
              onClick={nextMonth}
              disabled={
                !!effectiveMaxDate &&
                month.getFullYear() === effectiveMaxDate.getFullYear() &&
                month.getMonth() === effectiveMaxDate.getMonth()
              }
              className="h-7 w-7 shrink-0 rounded-md hover:bg-gray-100 flex items-center justify-center text-gray-600 disabled:opacity-30 disabled:cursor-not-allowed disabled:hover:bg-transparent"
            >
              <ChevronRight className="h-4 w-4" />
            </button>
          </div>
          <DayPicker
            mode="single"
            selected={selected}
            onSelect={handleSelect}
            month={month}
            onMonthChange={setMonth}
            hideNavigation
            disabled={disabledDays}
            classNames={{
              root: "text-sm",
              months: "flex flex-col",
              month_caption: "hidden",
              month_grid: "w-full border-collapse",
              weekdays: "flex",
              weekday: "w-9 text-xs font-medium text-gray-400 text-center py-1",
              weeks: "flex flex-col",
              week: "flex",
              day: "w-9 text-center",
              day_button:
                "h-9 w-9 rounded-md text-sm hover:bg-gray-100 transition-colors focus:outline-none",
              selected:
                "[&>button]:bg-primary [&>button]:text-white [&>button]:hover:bg-primary",
              today: "[&>button]:font-semibold [&>button]:text-primary",
              outside: "[&>button]:text-gray-300",
              disabled:
                "[&>button]:text-gray-300 [&>button]:cursor-not-allowed",
            }}
          />
        </PopoverPrimitive.Content>
      </PopoverPrimitive.Portal>
    </PopoverPrimitive.Root>
  );
}
