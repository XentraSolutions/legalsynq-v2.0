"use client";

import * as React from "react";
import {
  ArrowDownWideNarrow,
  ChevronDown,
  CloudBackup,
  CloudUpload,
  Divide,
  Loader,
  Plus,
  RefreshCw,
  Settings2,
  SquarePen,
  Trash2,
  Upload,
} from "lucide-react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

/**
 * Closed set of icons selling Buttons may use — keeps the icon import
 * surface small and prevents ad-hoc custom-styled icons from being passed
 * in. Add new entries here as new icons are actually needed.
 */
const BUTTON_ICONS = {
  arrowDownWideNarrow: ArrowDownWideNarrow,
  chevronDown: ChevronDown,
  cloudBackup: CloudBackup,
  cloudUpload: CloudUpload,
  divide: Divide,
  plus: Plus,
  refreshCw: RefreshCw,
  settings2: Settings2,
  squarePen: SquarePen,
  trash2: Trash2,
  upload: Upload,
} as const;

export type ButtonIconName = keyof typeof BUTTON_ICONS;

/**
 * Selling-scoped copy of the design system "Buttons" component set (Figma
 * file iweekbakJ6fgPsi3QJC63q, node 142:1412), used while the rest of the
 * app migrates off the shared ui/button.tsx. All selling components should
 * import Button from here.
 *
 * Size and color are variant-only — there is no className escape hatch for
 * either, so every selling Button renders one of the design system's
 * defined looks.
 *
 * One intentional deviation from the literal spec, to stay consistent with
 * the rest of this app rather than the exact exported values:
 * - Text/icon color is gray-900, not the spec's literal #0a0a0a (~1 shade
 *   off, imperceptible) — this app's grays are all Tailwind `gray`, never
 *   `neutral`.
 *
 * Colors are fixed hex values from the spec rather than Tailwind's
 * `gray`/`neutral` scale or tenant-dynamic `--color-primary`, since none of
 * the spec's grays (#E5E5E5, #F5F5F5) or the primary orange line up with
 * either Tailwind scale or a single tenant's brand color:
 * - Primary: bg #EE7132, border/divider #F4A076, hover bg #D9672E.
 * - Secondary/tertiary/ghost border and fill: #E5E5E5 / #F5F5F5. Ghost has
 *   no shadow at rest — only on hover, unlike every other variant.
 * - Destructive: bg #DC2626, hover bg #B91C1C.
 *
 * Primary's disabled state is a fixed hex rather than `disabled:opacity-50`
 * — each channel is #EE7132/#F4A076 blended 50% with white (the page
 * background), so the flattened color matches what 50% opacity would
 * render as, without opacity affecting the shadow too. Other variants still
 * use `disabled:opacity-50`.
 */
const buttonVariants = cva(
  "inline-flex h-[38px] items-center justify-center gap-2.5 whitespace-nowrap rounded-[10px] px-4 py-2 text-sm font-medium shadow-[0_1px_2px_0_rgba(0,0,0,0.1)] transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30 disabled:pointer-events-none",
  {
    variants: {
      variant: {
        primary:
          "bg-[#EE7132] text-white border border-[#F4A076] hover:bg-[#D9672E] disabled:bg-[#F7B899] disabled:text-white disabled:border-[#FAD0BB]",
        secondary:
          "bg-white text-gray-900 border border-[#E5E5E5] hover:bg-[#F5F5F5] disabled:opacity-50",
        tertiary:
          "bg-[#F5F5F5] text-gray-900 border border-[#E5E5E5] hover:bg-[#E5E5E5] disabled:opacity-50",
        ghost:
          "bg-transparent text-gray-900 shadow-none hover:bg-[#F5F5F5] hover:shadow-[0_1px_2px_0_rgba(0,0,0,0.1)] disabled:opacity-50",
        destructive: "bg-[#DC2626] text-white hover:bg-[#B91C1C] disabled:opacity-50",
        "icon-rounded":
          "h-9 w-9 p-0 rounded-full bg-white text-gray-900 border border-[#E5E5E5] hover:bg-[#F5F5F5] disabled:opacity-50",
        "icon-square":
          "h-9 w-9 p-0 bg-white text-gray-900 border border-[#E5E5E5] hover:bg-[#F5F5F5] disabled:opacity-50",
        "icon-square-destructive":
          "h-9 w-9 p-0 bg-white text-red-600 border border-gray-200 hover:bg-red-50 hover:border-red-200 disabled:opacity-50",
      },
    },
    defaultVariants: {
      variant: "primary",
    },
  },
);

const ICON_ONLY_VARIANTS = new Set([
  "icon-rounded",
  "icon-square",
  "icon-square-destructive",
]);

export interface ButtonProps
  extends
    Omit<React.ButtonHTMLAttributes<HTMLButtonElement>, "children">,
    VariantProps<typeof buttonVariants> {
  /** Shows a spinner in place of the icon (left icon, or the icon-only content) and disables the button — matches the design system's Loading state, which also drops the right icon. */
  loading?: boolean;
  /** Rendered before the label, from the fixed icon set. Ignored on icon-only variants — use `icon` instead. */
  leftIcon?: ButtonIconName;
  /** Rendered after the label, from the fixed icon set; dropped while loading. Ignored on icon-only variants. */
  rightIcon?: ButtonIconName;
  /** The icon rendered by icon-only variants (`icon-rounded` / `icon-square` / `icon-square-destructive`). Ignored otherwise. */
  icon?: ButtonIconName;
  /** Label content. Ignored on icon-only variants. */
  children?: React.ReactNode;
}

const DIVIDER_COLOR_CLASS: Record<string, string> = {
  primary: "border-[#F4A076]",
  destructive: "border-white/25",
  secondary: "border-[#E5E5E5]",
  tertiary: "border-[#E5E5E5]",
};

const ICON_CLASS = "h-4 w-4 shrink-0";

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  (
    {
      className,
      variant,
      loading,
      disabled,
      leftIcon,
      rightIcon,
      icon,
      children,
      ...props
    },
    ref,
  ) => {
    const isIconOnly = ICON_ONLY_VARIANTS.has(variant ?? "primary");
    const showDivider =
      !isIconOnly && variant !== "ghost" && !loading && !!(leftIcon || rightIcon);
    const dividerColorClass = DIVIDER_COLOR_CLASS[variant ?? "primary"];
    // Spinner takes over whichever icon slot is in use — left if there's a
    // leftIcon (or neither), right only when rightIcon is the sole icon.
    const loadingOnRight = loading && !leftIcon && !!rightIcon;

    const renderIcon = (name?: ButtonIconName) => {
      if (!name) {
        return null;
      }
      const Icon = BUTTON_ICONS[name];
      return <Icon className={ICON_CLASS} />;
    };

    const spinner = <Loader className={cn("animate-spin", ICON_CLASS)} />;

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
          loading ? spinner : renderIcon(icon)
        ) : showDivider ? (
          <>
            {leftIcon && (
              <span
                className={cn(
                  "flex h-full items-center px-3 border-r",
                  dividerColorClass,
                )}
              >
                {renderIcon(leftIcon)}
              </span>
            )}
            <span className="px-4">{children}</span>
            {rightIcon && (
              <span
                className={cn(
                  "flex h-full items-center px-3 border-l",
                  dividerColorClass,
                )}
              >
                {renderIcon(rightIcon)}
              </span>
            )}
          </>
        ) : (
          <>
            {loading && !loadingOnRight ? spinner : renderIcon(leftIcon)}
            {children}
            {loading ? (loadingOnRight ? spinner : null) : renderIcon(rightIcon)}
          </>
        )}
      </button>
    );
  },
);
Button.displayName = "Button";

export { Button, buttonVariants };
