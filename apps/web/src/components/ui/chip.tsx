"use client";

import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

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
        class: "bg-zinc-900 text-zinc-50",
      },
      {
        variant: "solid",
        color: "success",
        class: "bg-green-500 text-black",
      },
      {
        variant: "solid",
        color: "warning",
        class: "bg-yellow-500 text-black",
      },
      { variant: "solid", color: "danger", class: "bg-red-500 text-white" },
      { variant: "solid", color: "info", class: "bg-blue-500 text-white" },

      // light (flat neutral background, colored icon/text)
      {
        variant: "light",
        color: "default",
        class: "bg-zinc-100 text-zinc-900",
      },
      {
        variant: "light",
        color: "success",
        class: "bg-zinc-100 text-green-700",
      },
      {
        variant: "light",
        color: "warning",
        class: "bg-zinc-100 text-yellow-700",
      },
      {
        variant: "light",
        color: "danger",
        class: "bg-zinc-100 text-red-700",
      },
      { variant: "light", color: "info", class: "bg-zinc-100 text-blue-700" },

      // soft (tinted background matching the color)
      {
        variant: "soft",
        color: "default",
        class: "bg-zinc-500/15 text-zinc-900",
      },
      {
        variant: "soft",
        color: "success",
        class: "bg-green-500/15 text-green-700",
      },
      {
        variant: "soft",
        color: "warning",
        class: "bg-yellow-500/15 text-yellow-700",
      },
      {
        variant: "soft",
        color: "danger",
        class: "bg-red-500/15 text-red-700",
      },
      {
        variant: "soft",
        color: "info",
        class: "bg-blue-500/15 text-blue-700",
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
