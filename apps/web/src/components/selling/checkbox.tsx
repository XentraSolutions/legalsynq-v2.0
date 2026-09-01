"use client";

import * as React from "react";
import * as CheckboxPrimitive from "@radix-ui/react-checkbox";
import { Check, Minus } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Selling-scoped copy of the design system "Checkbox" component (Color
 * Base Gray, State Default), used while the rest of the app migrates off
 * the shared ui/checkbox.tsx. All selling components should import
 * Checkbox from here.
 *
 * Colors are fixed hex values from the spec rather than Tailwind's
 * `gray`/`neutral` scale, matching the same deviation documented in
 * ./button.tsx:
 * - Unchecked border: #E5E5E5 (--Border-border-primary).
 * - Checked background: #EE7132 (--Surface-surface-brand-orange).
 *
 * Radius is a literal 6px rather than Tailwind's `rounded-xs` utility —
 * Tailwind v4's default `--radius-xs` is 0.125rem (2px), which doesn't
 * match this design system's own radius-xs token (6px).
 *
 * "Show Text Box" from the spec is the caller's responsibility — this
 * component renders the control only, no built-in label.
 */
const SIZE_CLASS = {
  small: "h-4 w-4",
  medium: "h-5 w-5",
} as const;

const INDICATOR_SIZE_CLASS = {
  small: "h-3 w-3",
  medium: "h-3.5 w-3.5",
} as const;

export interface CheckboxProps
  extends React.ComponentPropsWithoutRef<typeof CheckboxPrimitive.Root> {
  /** 16px (small) or 20px (medium). Defaults to small. */
  size?: keyof typeof SIZE_CLASS;
}

const Checkbox = React.forwardRef<
  React.ElementRef<typeof CheckboxPrimitive.Root>,
  CheckboxProps
>(({ className, size = "small", ...props }, ref) => (
  <CheckboxPrimitive.Root
    ref={ref}
    className={cn(
      "peer shrink-0 rounded-[6px] border-[1.5px] border-[#E5E5E5] bg-white transition-colors",
      SIZE_CLASS[size],
      "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#EE7132]/30",
      "disabled:cursor-not-allowed disabled:opacity-50",
      "data-[state=checked]:bg-[#EE7132] data-[state=checked]:border-[#EE7132]",
      "data-[state=indeterminate]:bg-[#EE7132] data-[state=indeterminate]:border-[#EE7132]",
      className,
    )}
    {...props}
  >
    <CheckboxPrimitive.Indicator className="flex items-center justify-center text-white">
      {props.checked === "indeterminate" ? (
        <Minus className={INDICATOR_SIZE_CLASS[size]} strokeWidth={2.5} />
      ) : (
        <Check className={INDICATOR_SIZE_CLASS[size]} strokeWidth={2.5} />
      )}
    </CheckboxPrimitive.Indicator>
  </CheckboxPrimitive.Root>
));
Checkbox.displayName = "Checkbox";

export { Checkbox };
