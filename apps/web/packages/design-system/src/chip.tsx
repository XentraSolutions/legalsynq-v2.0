"use client";

import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "./utils";

const chipVariants = cva(
  "inline-flex items-center justify-center gap-1 rounded-full font-medium whitespace-nowrap",
  {
    variants: {
      color: {
        default: "",
        success: "",
        warning: "",
        danger: "",
        info: "",
        purple: "",
        teal: "",
        gray: "",
        red: "",
        yellow: "",
        green: "",
        blue: "",
        indigo: "",
        pink: "",
        "brand-orange": "",
        "brand-slate": "",
      },
      variant: {
        primary: "",
        secondary: "",
        tertiary: "",
        soft: "",
      },
      size: {
        lg: "h-7 px-3 text-sm gap-1.5 [&_svg]:size-3.5",
        md: "h-6 px-2.5 text-xs gap-1 [&_svg]:size-3",
        sm: "h-5 px-2 text-[11px] gap-1 [&_svg]:size-3",
        icon: "size-5 p-0 [&_svg]:size-3",
      },
    },
    compoundVariants: [
      // primary (solid, filled background)
      {
        variant: "primary",
        color: "default",
        class: "bg-surface-invert text-text-invert",
      },
      {
        variant: "primary",
        color: "success",
        class: "bg-surface-success text-black",
      },
      {
        variant: "primary",
        color: "warning",
        class: "bg-surface-warning text-black",
      },
      { variant: "primary", color: "danger", class: "bg-surface-error text-white" },
      { variant: "primary", color: "info", class: "bg-blue-500 text-white" },
      {
        variant: "primary",
        color: "purple",
        class: "bg-purple-500 text-white",
      },
      { variant: "primary", color: "teal", class: "bg-teal-500 text-white" },
      { variant: "primary", color: "gray", class: "bg-gray-500 text-white" },
      { variant: "primary", color: "red", class: "bg-red-500 text-white" },
      { variant: "primary", color: "yellow", class: "bg-yellow-500 text-black" },
      { variant: "primary", color: "green", class: "bg-green-500 text-black" },
      { variant: "primary", color: "blue", class: "bg-blue-500 text-white" },
      { variant: "primary", color: "indigo", class: "bg-indigo-500 text-white" },
      { variant: "primary", color: "pink", class: "bg-pink-500 text-white" },
      {
        variant: "primary",
        color: "brand-orange",
        class: "bg-surface-brand-orange text-white",
      },
      {
        variant: "primary",
        color: "brand-slate",
        class: "bg-surface-brand-slate text-white",
      },

      // secondary (flat neutral background, colored icon/text)
      {
        variant: "secondary",
        color: "default",
        class: "bg-surface-tertiary text-text-primary",
      },
      {
        variant: "secondary",
        color: "success",
        class: "bg-surface-tertiary text-text-success-dark",
      },
      {
        variant: "secondary",
        color: "warning",
        class: "bg-surface-tertiary text-text-warning-dark",
      },
      {
        variant: "secondary",
        color: "danger",
        class: "bg-surface-tertiary text-text-error-dark",
      },
      { variant: "secondary", color: "info", class: "bg-surface-tertiary text-blue-700" },
      {
        variant: "secondary",
        color: "purple",
        class: "bg-surface-tertiary text-purple-700",
      },
      {
        variant: "secondary",
        color: "teal",
        class: "bg-surface-tertiary text-teal-700",
      },
      { variant: "secondary", color: "gray", class: "bg-surface-tertiary text-gray-700" },
      { variant: "secondary", color: "red", class: "bg-surface-tertiary text-red-700" },
      {
        variant: "secondary",
        color: "yellow",
        class: "bg-surface-tertiary text-yellow-700",
      },
      { variant: "secondary", color: "green", class: "bg-surface-tertiary text-green-700" },
      { variant: "secondary", color: "blue", class: "bg-surface-tertiary text-blue-700" },
      {
        variant: "secondary",
        color: "indigo",
        class: "bg-surface-tertiary text-indigo-700",
      },
      { variant: "secondary", color: "pink", class: "bg-surface-tertiary text-pink-700" },
      {
        variant: "secondary",
        color: "brand-orange",
        class: "bg-surface-tertiary text-text-brand-orange-dark",
      },
      {
        variant: "secondary",
        color: "brand-slate",
        class: "bg-surface-tertiary text-text-brand-slate-dark",
      },

      // tertiary (no background, colored text/icon only)
      {
        variant: "tertiary",
        color: "default",
        class: "bg-transparent text-text-primary",
      },
      {
        variant: "tertiary",
        color: "success",
        class: "bg-transparent text-text-success-dark",
      },
      {
        variant: "tertiary",
        color: "warning",
        class: "bg-transparent text-text-warning-dark",
      },
      {
        variant: "tertiary",
        color: "danger",
        class: "bg-transparent text-text-error-dark",
      },
      { variant: "tertiary", color: "info", class: "bg-transparent text-blue-700" },
      {
        variant: "tertiary",
        color: "purple",
        class: "bg-transparent text-purple-700",
      },
      {
        variant: "tertiary",
        color: "teal",
        class: "bg-transparent text-teal-700",
      },
      { variant: "tertiary", color: "gray", class: "bg-transparent text-gray-700" },
      { variant: "tertiary", color: "red", class: "bg-transparent text-red-700" },
      { variant: "tertiary", color: "yellow", class: "bg-transparent text-yellow-700" },
      { variant: "tertiary", color: "green", class: "bg-transparent text-green-700" },
      { variant: "tertiary", color: "blue", class: "bg-transparent text-blue-700" },
      { variant: "tertiary", color: "indigo", class: "bg-transparent text-indigo-700" },
      { variant: "tertiary", color: "pink", class: "bg-transparent text-pink-700" },
      {
        variant: "tertiary",
        color: "brand-orange",
        class: "bg-transparent text-text-brand-orange-dark",
      },
      {
        variant: "tertiary",
        color: "brand-slate",
        class: "bg-transparent text-text-brand-slate-dark",
      },

      // soft (tinted background matching the color)
      {
        variant: "soft",
        color: "default",
        class: "bg-gray-500/15 text-text-primary",
      },
      {
        variant: "soft",
        color: "success",
        class: "bg-surface-success/15 text-text-success-dark",
      },
      {
        variant: "soft",
        color: "warning",
        class: "bg-surface-warning/15 text-text-warning-dark",
      },
      {
        variant: "soft",
        color: "danger",
        class: "bg-surface-error/15 text-text-error-dark",
      },
      {
        variant: "soft",
        color: "info",
        class: "bg-blue-500/15 text-blue-700",
      },
      {
        variant: "soft",
        color: "purple",
        class: "bg-purple-500/15 text-purple-700",
      },
      {
        variant: "soft",
        color: "teal",
        class: "bg-teal-500/15 text-teal-700",
      },
      { variant: "soft", color: "gray", class: "bg-gray-500/15 text-gray-700" },
      { variant: "soft", color: "red", class: "bg-red-500/15 text-red-700" },
      { variant: "soft", color: "yellow", class: "bg-yellow-500/15 text-yellow-700" },
      { variant: "soft", color: "green", class: "bg-green-500/15 text-green-700" },
      { variant: "soft", color: "blue", class: "bg-blue-500/15 text-blue-700" },
      { variant: "soft", color: "indigo", class: "bg-indigo-500/15 text-indigo-700" },
      { variant: "soft", color: "pink", class: "bg-pink-500/15 text-pink-700" },
      {
        variant: "soft",
        color: "brand-orange",
        class: "bg-surface-brand-orange/15 text-text-brand-orange-dark",
      },
      {
        variant: "soft",
        color: "brand-slate",
        class: "bg-surface-brand-slate/15 text-text-brand-slate-dark",
      },
    ],
    defaultVariants: {
      color: "default",
      variant: "primary",
      size: "md",
    },
  },
);

export interface ChipProps
  extends Omit<React.HTMLAttributes<HTMLSpanElement>, "color">,
    VariantProps<typeof chipVariants> {
  /** Icon rendered before the label. */
  leftIcon?: React.ReactNode;
  /** Icon rendered after the label. */
  rightIcon?: React.ReactNode;
  /** Renders only an icon (leftIcon, falling back to rightIcon), dropping the label. */
  iconOnly?: boolean;
}

function Chip({
  className,
  color,
  variant,
  size,
  leftIcon,
  rightIcon,
  iconOnly,
  children,
  ...props
}: ChipProps) {
  return (
    <span
      className={cn(
        chipVariants({ color, variant, size: iconOnly ? "icon" : size }),
        className,
      )}
      {...props}
    >
      {iconOnly ? (
        leftIcon ?? rightIcon
      ) : (
        <>
          {leftIcon}
          {children}
          {rightIcon}
        </>
      )}
    </span>
  );
}

export { Chip, chipVariants };
