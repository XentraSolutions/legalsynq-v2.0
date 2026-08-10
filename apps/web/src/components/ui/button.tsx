"use client";

import * as React from "react";
import { Loader2 } from "lucide-react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

/**
 * Matches the "Buttons" component set in the LegalSynq V3 design system
 * (Figma file iweekbakJ6fgPsi3QJC63q, node 142:1412): Primary / Secondary /
 * Tertiary / Ghost / Destructive plus Icon Rounded / Icon Square, each with
 * Default / Hover / Disabled / Loading states.
 *
 * Two intentional deviations from the literal spec, both to stay consistent
 * with the rest of this app rather than the exact exported values:
 * - Text/icon color is gray-900, not the spec's literal #0a0a0a (~1 shade
 *   off, imperceptible) — this app's grays are all Tailwind `gray`, never
 *   `neutral`.
 * - Primary's hover is `bg-primary/90` (already the app-wide convention),
 *   not the spec's literal darker hex — the spec's hex was just whatever
 *   tenant brand color was active at export time; `--color-primary` is
 *   tenant-dynamic (see globals.css), so a fixed hex would only be right
 *   for one tenant.
 */
const buttonVariants = cva(
  "inline-flex items-center justify-center gap-2.5 whitespace-nowrap rounded-[10px] text-sm font-medium shadow-[0_1px_2px_0_rgba(0,0,0,0.1)] transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30 disabled:pointer-events-none disabled:opacity-50",
  {
    variants: {
      variant: {
        primary: "px-4 py-2 bg-primary text-white hover:bg-primary/90",
        secondary:
          "px-4 py-2 bg-white text-gray-900 border border-gray-200 hover:bg-gray-100",
        tertiary:
          "px-4 py-2 bg-gray-100 text-gray-900 border border-gray-200 hover:bg-gray-200",
        ghost:
          "px-4 py-2 bg-transparent text-gray-900 shadow-none hover:bg-gray-100",
        destructive: "px-4 py-2 bg-red-600 text-white hover:bg-red-700",
        "icon-rounded":
          "h-9 w-9 p-0 rounded-full bg-white text-gray-900 border border-gray-200 hover:bg-gray-100",
        "icon-square":
          "h-9 w-9 p-0 bg-white text-gray-900 border border-gray-200 hover:bg-gray-100",
      },
    },
    defaultVariants: {
      variant: "primary",
    },
  },
);

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  /** Shows a spinner in place of the left icon (or the icon-only content) and disables the button — matches the design system's Loading state, which also drops the right icon. */
  loading?: boolean;
  /** Rendered before the label. Ignored on icon-only variants — pass the icon as `children` instead. */
  leftIcon?: React.ReactNode;
  /** Rendered after the label; dropped while loading. */
  rightIcon?: React.ReactNode;
  /**
   * Sets `leftIcon`/`rightIcon` off from the label with a full-height
   * vertical rule, matching the design system's "button + icon" split
   * composition (e.g. "Add Company", "Export"). Divider color follows the
   * variant: white/25 on solid variants (primary/destructive), gray-200
   * otherwise. Ignored on icon-only variants or while loading.
   */
  iconDivider?: boolean;
}

const DIVIDER_COLOR_CLASS: Record<string, string> = {
  primary: "border-white/25",
  destructive: "border-white/25",
  secondary: "border-gray-200",
  tertiary: "border-gray-200",
  ghost: "border-gray-200",
};

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  (
    {
      className,
      variant,
      loading,
      disabled,
      leftIcon,
      rightIcon,
      iconDivider,
      children,
      ...props
    },
    ref,
  ) => {
    const isIconOnly = variant === "icon-rounded" || variant === "icon-square";
    const showDivider = iconDivider && !isIconOnly && !loading && (leftIcon || rightIcon);
    const dividerColorClass = DIVIDER_COLOR_CLASS[variant ?? "primary"];

    return (
      <button
        ref={ref}
        disabled={disabled || loading}
        className={cn(
          buttonVariants({ variant }),
          showDivider && "p-0 gap-0 overflow-hidden",
          className,
        )}
        {...props}
      >
        {isIconOnly ? (
          loading ? <Loader2 className="h-4 w-4 animate-spin" /> : children
        ) : showDivider ? (
          <>
            {leftIcon && (
              <span className={cn("flex items-center px-3 py-2 border-r", dividerColorClass)}>
                {leftIcon}
              </span>
            )}
            <span className="px-4 py-2">{children}</span>
            {rightIcon && (
              <span className={cn("flex items-center px-3 py-2 border-l", dividerColorClass)}>
                {rightIcon}
              </span>
            )}
          </>
        ) : (
          <>
            {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : leftIcon}
            {children}
            {!loading && rightIcon}
          </>
        )}
      </button>
    );
  },
);
Button.displayName = "Button";

export { Button, buttonVariants };
