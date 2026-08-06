"use client";

import * as React from "react";
import * as PopoverPrimitive from "@radix-ui/react-popover";
import { DayPicker, type DateRange } from "react-day-picker";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { cn } from "@/lib/utils";

import "react-day-picker/style.css";

export interface DateRangeValue {
  from?: string;
  to?: string;
}

interface DateRangePickerProps {
  value?: DateRangeValue;
  onChange?: (value: DateRangeValue) => void;
  placeholder?: string;
  className?: string;
  disabled?: boolean;
  /** Renders a quick-select preset sidebar (Today/Yesterday/.../Last Year) alongside a two-month calendar. */
  presets?: boolean;
  disableFutureDates?: boolean;
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

function addDays(date: Date, days: number): Date {
  const d = new Date(date);
  d.setDate(d.getDate() + days);
  return d;
}

function startOfWeek(date: Date): Date {
  const d = new Date(date);
  const day = (d.getDay() + 6) % 7; // Monday = 0
  d.setDate(d.getDate() - day);
  d.setHours(0, 0, 0, 0);
  return d;
}

function sameDate(a?: Date, b?: Date): boolean {
  return (
    !!a &&
    !!b &&
    a.getFullYear() === b.getFullYear() &&
    a.getMonth() === b.getMonth() &&
    a.getDate() === b.getDate()
  );
}

/** Comparable month index (year*12+month) so "is this month at or past that one" can use a plain >= instead of a brittle equality check. */
function monthIndex(d: Date): number {
  return d.getFullYear() * 12 + d.getMonth();
}

interface DatePreset {
  key: string;
  label: string;
  range: () => DateRange;
}

function buildPresets(): DatePreset[] {
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return [
    { key: "today", label: "Today", range: () => ({ from: today, to: today }) },
    {
      key: "yesterday",
      label: "Yesterday",
      range: () => {
        const d = addDays(today, -1);
        return { from: d, to: d };
      },
    },
    {
      key: "lastWeek",
      label: "Last week",
      range: () => {
        const start = addDays(startOfWeek(today), -7);
        return { from: start, to: addDays(start, 6) };
      },
    },
    {
      key: "pastTwoWeeks",
      label: "Past Two Weeks",
      range: () => ({ from: addDays(today, -13), to: today }),
    },
    {
      key: "thisMonth",
      label: "This Month",
      range: () => ({
        from: new Date(today.getFullYear(), today.getMonth(), 1),
        to: today,
      }),
    },
    {
      key: "lastMonth",
      label: "Last Month",
      range: () => ({
        from: new Date(today.getFullYear(), today.getMonth() - 1, 1),
        to: new Date(today.getFullYear(), today.getMonth(), 0),
      }),
    },
    {
      key: "thisYear",
      label: "This Year",
      range: () => ({ from: new Date(today.getFullYear(), 0, 1), to: today }),
    },
    {
      key: "lastYear",
      label: "Last Year",
      range: () => ({
        from: new Date(today.getFullYear() - 1, 0, 1),
        to: new Date(today.getFullYear() - 1, 11, 31),
      }),
    },
  ];
}

export function DateRangePicker({
  value,
  onChange,
  placeholder = "Pick a date range",
  className,
  disabled,
  presets = false,
  disableFutureDates = false,
}: DateRangePickerProps) {
  const [open, setOpen] = React.useState(false);
  const [maxDate] = React.useState(() =>
    disableFutureDates ? new Date() : new Date("9999-12-31"),
  );
  const selected: DateRange | undefined = React.useMemo(() => {
    const from = parseDate(value?.from);
    const to = parseDate(value?.to);
    if (!from && !to) return undefined;
    return { from, to };
  }, [value?.from, value?.to]);
  const [month, setMonth] = React.useState<Date>(selected?.from ?? new Date());
  // react-day-picker's addToRange sets `to` equal to `from` on the very first
  // click of a fresh range, so `range.from && range.to` is true after just one
  // click. Track clicks per open so we only auto-close after the second one.
  const selectionStepRef = React.useRef(0);

  function handleOpenChange(nextOpen: boolean) {
    if (nextOpen) selectionStepRef.current = 0;
    setOpen(nextOpen);
  }

  function handleSelect(range: DateRange | undefined) {
    selectionStepRef.current += 1;
    onChange?.({
      from: range?.from ? formatDate(range.from) : undefined,
      to: range?.to ? formatDate(range.to) : undefined,
    });
    if (selectionStepRef.current >= 2) setOpen(false);
  }

  function prevMonth() {
    setMonth((m) => new Date(m.getFullYear(), m.getMonth() - 1, 1));
  }

  function nextMonth() {
    setMonth((m) => {
      const next = new Date(m.getFullYear(), m.getMonth() + 1, 1);
      if (next > maxDate) return m;
      return next;
    });
  }

  function handleMonthSelect(monthIndex: number) {
    setMonth((m) => {
      const maxMonth =
        m.getFullYear() === maxDate.getFullYear() ? maxDate.getMonth() : 11;
      return new Date(m.getFullYear(), Math.min(monthIndex, maxMonth), 1);
    });
  }

  function handleYearSelect(year: number) {
    setMonth((m) => {
      const maxMonth = year === maxDate.getFullYear() ? maxDate.getMonth() : 11;
      return new Date(year, Math.min(m.getMonth(), maxMonth), 1);
    });
  }

  const years = React.useMemo(
    () =>
      yearRange(
        (selected?.from ?? new Date()).getFullYear(),
        maxDate.getFullYear(),
      ),
    [selected?.from, maxDate],
  );

  const label = selected?.from
    ? selected.to
      ? `${displayDate(selected.from)} - ${displayDate(selected.to)}`
      : displayDate(selected.from)
    : placeholder;

  // Recomputed each time the popover opens (not just once at mount) so "today" can't
  // go stale if the page is left open across a day boundary.
  const datePresets = React.useMemo(
    () => (presets ? buildPresets() : []),
    [presets, open],
  );

  function handlePresetClick(preset: DatePreset) {
    const range = preset.range();
    onChange?.({
      from: range.from ? formatDate(range.from) : undefined,
      to: range.to ? formatDate(range.to) : undefined,
    });
    if (range.from) setMonth(range.from);
    setOpen(false);
  }

  function isPresetActive(preset: DatePreset): boolean {
    if (!selected?.from) return false;
    const range = preset.range();
    return (
      sameDate(selected.from, range.from) && sameDate(selected.to, range.to)
    );
  }

  const month2 = new Date(month.getFullYear(), month.getMonth() + 1, 1);
  const nextDisabled = presets
    ? monthIndex(month2) >= monthIndex(maxDate)
    : monthIndex(month) >= monthIndex(maxDate);

  return (
    <PopoverPrimitive.Root open={open} onOpenChange={handleOpenChange}>
      <PopoverPrimitive.Trigger asChild>
        <button
          type="button"
          disabled={disabled}
          className={cn(
            "h-9 w-full rounded-lg border border-gray-200 bg-white px-3 py-1 text-sm text-left transition-colors focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary disabled:cursor-not-allowed disabled:opacity-50",
            !selected?.from && "text-gray-400",
            className,
          )}
        >
          <span className="flex items-center gap-2">
            <i className="ri-calendar-line text-gray-400 text-base shrink-0" />
            {label}
          </span>
        </button>
      </PopoverPrimitive.Trigger>

      <PopoverPrimitive.Portal>
        <PopoverPrimitive.Content
          align="start"
          sideOffset={4}
          className={cn(
            "z-50 rounded-xl border border-gray-200 bg-white shadow-lg",
            presets ? "flex p-0" : "p-3",
          )}
        >
          {presets && (
            <div className="w-40 shrink-0 border-r border-gray-100 py-2 px-1 flex flex-col">
              {datePresets.map((preset) => (
                <button
                  key={preset.key}
                  type="button"
                  onClick={() => handlePresetClick(preset)}
                  className={cn(
                    "text-left text-sm px-3 py-2 rounded-md hover:bg-gray-50 transition-colors text-gray-700",
                    isPresetActive(preset) &&
                      "bg-primary/5 text-primary font-medium",
                  )}
                >
                  {preset.label}
                </button>
              ))}
            </div>
          )}
          <div className={presets ? "p-3" : undefined}>
            <div className="flex items-center justify-between gap-1 px-1 mb-2">
              <button
                type="button"
                onClick={prevMonth}
                className="h-7 w-7 shrink-0 rounded-md hover:bg-gray-100 flex items-center justify-center text-gray-600"
              >
                <ChevronLeft className="h-4 w-4" />
              </button>
              {presets ? (
                <div className="flex-1 grid grid-cols-2 text-center">
                  <span className="text-sm font-medium text-gray-800">
                    {MONTH_NAMES[month.getMonth()]} {month.getFullYear()}
                  </span>
                  <span className="text-sm font-medium text-gray-800">
                    {MONTH_NAMES[month2.getMonth()]} {month2.getFullYear()}
                  </span>
                </div>
              ) : (
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
              )}
              <button
                type="button"
                onClick={nextMonth}
                disabled={nextDisabled}
                className="h-7 w-7 shrink-0 rounded-md hover:bg-gray-100 flex items-center justify-center text-gray-600 disabled:opacity-30 disabled:cursor-not-allowed disabled:hover:bg-transparent"
              >
                <ChevronRight className="h-4 w-4" />
              </button>
            </div>
            <DayPicker
              mode="range"
              resetOnSelect
              numberOfMonths={presets ? 2 : 1}
              selected={selected}
              onSelect={handleSelect}
              month={month}
              onMonthChange={setMonth}
              disabled={{ after: maxDate }}
              hideNavigation
              classNames={{
                root: "text-sm",
                months: presets ? "flex flex-row gap-6" : "flex flex-col",
                month_caption: "hidden",
                month_grid: "w-full border-collapse",
                weekdays: "flex",
                weekday:
                  "w-9 text-xs font-medium text-gray-500 text-center py-1",
                weeks: "flex flex-col",
                week: "flex",
                day: "w-9 text-center",
                day_button:
                  "h-9 w-9 rounded-full text-sm text-gray-900 hover:bg-gray-100 transition-colors focus:outline-none",
                range_start:
                  "rounded-l-full bg-primary [&>button]:bg-primary [&>button]:text-white [&>button]:hover:bg-primary",
                range_end:
                  "rounded-r-full bg-primary [&>button]:bg-primary [&>button]:text-white [&>button]:hover:bg-primary",
                range_middle:
                  "bg-gray-100 [&>button]:bg-transparent [&>button]:!text-gray-900 [&>button]:hover:bg-transparent [&>button]:rounded-none",
                selected:
                  "[&>button]:bg-primary [&>button]:text-white [&>button]:hover:bg-primary",
                today: "[&>button]:font-semibold [&>button]:text-primary",
                outside: "[&>button]:text-gray-400",
                disabled:
                  "[&>button]:text-gray-400 [&>button]:cursor-not-allowed",
              }}
            />
          </div>
        </PopoverPrimitive.Content>
      </PopoverPrimitive.Portal>
    </PopoverPrimitive.Root>
  );
}
