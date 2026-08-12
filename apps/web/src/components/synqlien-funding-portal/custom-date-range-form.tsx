"use client";

import { useRef, useState, type ChangeEvent, type FormEvent, type MouseEvent } from "react";

interface CustomDateRangeFormProps {
  from?: string;
  to?: string;
  defaultDate: string;
}

export function CustomDateRangeForm({
  from,
  to,
  defaultDate,
}: CustomDateRangeFormProps) {
  const [startDate, setStartDate] = useState(from ?? defaultDate);
  const [endDate, setEndDate] = useState(to ?? defaultDate);
  const startInputRef = useRef<HTMLInputElement>(null);
  const endInputRef = useRef<HTMLInputElement>(null);

  function handleStartChange(event: ChangeEvent<HTMLInputElement> | FormEvent<HTMLInputElement>) {
    const nextStart = event.currentTarget.value;
    setStartDate(nextStart);

    if (nextStart && endDate && endDate < nextStart) {
      setEndDate(nextStart);
    }
  }

  function handleEndChange(event: ChangeEvent<HTMLInputElement> | FormEvent<HTMLInputElement>) {
    const nextEnd = event.currentTarget.value;
    setEndDate(startDate && nextEnd && nextEnd < startDate ? startDate : nextEnd);
  }

  function clampEndToStart() {
    const nextStart = startInputRef.current?.value ?? startDate;
    const nextEnd = endInputRef.current?.value ?? endDate;
    if (!nextStart || !nextEnd) return true;
    if (nextEnd >= nextStart) return false;

    if (endInputRef.current) {
      endInputRef.current.value = nextStart;
    }
    setStartDate(nextStart);
    setEndDate(nextStart);
    return true;
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    if (clampEndToStart()) {
      event.preventDefault();
    }
  }

  function handleApplyClick(event: MouseEvent<HTMLButtonElement>) {
    if (clampEndToStart()) {
      event.preventDefault();
    }
  }

  return (
    <form
      action="/funding/dashboard"
      onSubmit={handleSubmit}
      noValidate
      className="mt-3 grid gap-2 rounded-[8px] bg-[#f5f5f5] p-1 sm:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_96px]"
    >
      <input type="hidden" name="range" value="custom" />
      <label className="flex h-9 min-w-0 items-center gap-2 rounded-[7px] border border-[#e5e5e5] bg-white px-3 shadow-[0_1px_1px_rgba(0,0,0,0.06)] transition-colors focus-within:border-[#ee7132] focus-within:ring-2 focus-within:ring-[#fdf1eb]">
        <span className="shrink-0 text-[12px] font-medium leading-[1.6] text-[#737373]">Start</span>
        <input
          type="date"
          name="from"
          aria-label="Start date"
          ref={startInputRef}
          value={startDate}
          onChange={handleStartChange}
          onInput={handleStartChange}
          onBlur={handleStartChange}
          required
          className="h-full min-w-0 flex-1 border-0 bg-transparent p-0 text-right text-[12px] font-medium leading-[1.6] text-[#0a0a0a] outline-none [color-scheme:light]"
        />
      </label>
      <label className="flex h-9 min-w-0 items-center gap-2 rounded-[7px] border border-[#e5e5e5] bg-white px-3 shadow-[0_1px_1px_rgba(0,0,0,0.06)] transition-colors focus-within:border-[#ee7132] focus-within:ring-2 focus-within:ring-[#fdf1eb]">
        <span className="shrink-0 text-[12px] font-medium leading-[1.6] text-[#737373]">End</span>
        <input
          type="date"
          name="to"
          aria-label="End date"
          ref={endInputRef}
          value={endDate}
          min={startDate}
          onChange={handleEndChange}
          onInput={handleEndChange}
          onBlur={handleEndChange}
          onInvalid={handleEndChange}
          required
          className="h-full min-w-0 flex-1 border-0 bg-transparent p-0 text-right text-[12px] font-medium leading-[1.6] text-[#0a0a0a] outline-none [color-scheme:light]"
        />
      </label>
      <button
        type="submit"
        onMouseDown={handleApplyClick}
        onClick={handleApplyClick}
        className="inline-flex h-9 items-center justify-center rounded-[7px] bg-[#ee7132] px-4 text-[14px] font-medium leading-[1.6] text-white shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors hover:bg-[#d86228] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132]"
      >
        Apply
      </button>
    </form>
  );
}
