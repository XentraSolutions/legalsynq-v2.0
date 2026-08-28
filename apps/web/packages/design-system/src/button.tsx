"use client";

import * as React from "react";
import { Slot } from "@radix-ui/react-slot";
import { Loader2 } from "lucide-react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "./utils";

/**
 * Matches the "Buttons" component set in the LegalSynq V3 design system
 * (Figma file iweekbakJ6fgPsi3QJC63q, node 142:1412): Primary / Secondary /
 * Tertiary / Ghost / Destructive plus Icon Rounded / Icon Square, each with
 * Default / Hover / Disabled / Loading states.
 *
 * Text/icon color is the `text-primary` token (gray-950), not the spec's
 * literal #0a0a0a (~1 shade off, imperceptible) — this app's grays are all
 * Tailwind `gray`, never `neutral`.
 */
const buttonVariants = cva(
  "inline-flex h-[38px] items-center justify-center gap-2.5 whitespace-nowrap rounded-[10px] text-sm font-medium shadow-[0_1px_2px_0_rgba(0,0,0,0.1)] transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-border-brand-orange/30 disabled:pointer-events-none disabled:opacity-50",
  {
    variants: {
      variant: {
        primary:
          "px-4 py-2 bg-button-primary-bg-default text-button-primary-text hover:bg-button-primary-bg-hover",
        secondary:
          "px-4 py-2 bg-surface-primary text-text-primary border border-button-border hover:bg-button-secondary-bg-hover",
        tertiary:
          "px-4 py-2 bg-button-tertiary-bg-default text-text-primary border border-button-border hover:bg-button-tertiary-bg-hover",
        ghost:
          "px-4 py-2 bg-transparent text-text-primary shadow-none hover:bg-button-ghost-bg-hover",
        destructive:
          "px-4 py-2 bg-button-destructive-bg-default text-button-primary-text hover:bg-button-destructive-bg-hover",
        "icon-rounded":
          "h-9 w-9 p-0 rounded-full bg-surface-primary text-text-primary border border-button-border hover:bg-button-secondary-bg-hover",
        "icon-square":
          "h-9 w-9 p-0 bg-surface-primary text-text-primary border border-button-border hover:bg-button-secondary-bg-hover",
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
  /**
   * Rendered before the label. Ignored on icon-only variants — pass the
   * icon as `children` instead. Whenever `leftIcon` or `rightIcon` is set,
   * it's shown off from the label by a full-height vertical rule, matching
   * the design system's "button + icon" split composition (e.g. "Add
   * Company", "Export"). Divider color follows the variant: white/25 on
   * solid variants (primary/destructive), the `button-border` token
   * otherwise. Ignored while loading.
   */
  leftIcon?: React.ReactNode;
  /** Rendered after the label; dropped while loading. See `leftIcon` for the divider behavior. */
  rightIcon?: React.ReactNode;
  /**
   * Renders the button's styles/behavior onto its child element instead of
   * a `<button>` (via Radix `Slot`) — for a read-only trigger that needs to
   * actually be a link or another primitive's element, e.g.
   * `<Button asChild variant="ghost"><Link href="/cases/1">View</Link></Button>`
   * or wrapping a Radix `DropdownMenuTrigger`. The child must be a single
   * element and is responsible for its own href/navigation; `loading`,
   * `leftIcon`, and `rightIcon` are ignored in this mode — pass icons as
   * part of the child's own content instead.
   */
  asChild?: boolean;
}

const DIVIDER_COLOR_CLASS: Record<string, string> = {
  primary: "border-white/25",
  destructive: "border-white/25",
  secondary: "border-button-border",
  tertiary: "border-button-border",
  ghost: "border-button-border",
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
      asChild,
      children,
      ...props
    },
    ref,
  ) => {
    const isIconOnly = variant === "icon-rounded" || variant === "icon-square";
    const showDivider = !isIconOnly && !loading && (leftIcon || rightIcon);
    const dividerColorClass = DIVIDER_COLOR_CLASS[variant ?? "primary"];

    if (asChild) {
      return (
        <Slot
          ref={ref}
          className={cn(buttonVariants({ variant }), className)}
          {...props}
        >
          {children}
        </Slot>
      );
    }

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
              <span className={cn("flex h-full items-center px-3 border-r", dividerColorClass)}>
                {leftIcon}
              </span>
            )}
            <span className="px-4">{children}</span>
            {rightIcon && (
              <span className={cn("flex h-full items-center px-3 border-l", dividerColorClass)}>
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
