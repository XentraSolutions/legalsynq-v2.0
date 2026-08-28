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
      },
      variant: {
        solid: "",
        light: "",
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
      // solid
      {
        variant: "solid",
        color: "default",
        class: "bg-surface-invert text-text-invert",
      },
      {
        variant: "solid",
        color: "success",
        class: "bg-surface-success text-black",
      },
      {
        variant: "solid",
        color: "warning",
        class: "bg-surface-warning text-black",
      },
      { variant: "solid", color: "danger", class: "bg-surface-error text-white" },
      { variant: "solid", color: "info", class: "bg-blue-500 text-white" },
      {
        variant: "solid",
        color: "purple",
        class: "bg-purple-500 text-white",
      },
      { variant: "solid", color: "teal", class: "bg-teal-500 text-white" },

      // light (flat neutral background, colored icon/text)
      {
        variant: "light",
        color: "default",
        class: "bg-surface-tertiary text-text-primary",
      },
      {
        variant: "light",
        color: "success",
        class: "bg-surface-tertiary text-text-success-dark",
      },
      {
        variant: "light",
        color: "warning",
        class: "bg-surface-tertiary text-text-warning-dark",
      },
      {
        variant: "light",
        color: "danger",
        class: "bg-surface-tertiary text-text-error-dark",
      },
      { variant: "light", color: "info", class: "bg-surface-tertiary text-blue-700" },
      {
        variant: "light",
        color: "purple",
        class: "bg-surface-tertiary text-purple-700",
      },
      {
        variant: "light",
        color: "teal",
        class: "bg-surface-tertiary text-teal-700",
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
    ],
    defaultVariants: {
      color: "default",
      variant: "solid",
      size: "md",
    },
  },
);

export interface ChipProps
  extends Omit<React.HTMLAttributes<HTMLSpanElement>, "color">,
    VariantProps<typeof chipVariants> {
  /** Icon rendered before the label. */
  icon?: React.ReactNode;
  /** Renders only the icon, dropping the label. */
  iconOnly?: boolean;
}

function Chip({
  className,
  color,
  variant,
  size,
  icon,
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
      {icon}
      {!iconOnly && children}
    </span>
  );
}

export { Chip, chipVariants };
