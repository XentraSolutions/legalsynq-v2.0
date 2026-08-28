import * as React from "react";
import { cn } from "./utils";

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {}

const Input = React.forwardRef<HTMLInputElement, InputProps>(
  ({ className, type, ...props }, ref) => {
    return (
      <input
        type={type}
        className={cn(
          "h-9 w-full rounded-lg border border-border-primary bg-surface-primary px-3 py-1 text-sm text-text-secondary placeholder:text-text-disabled focus:outline-none focus:ring-2 focus:ring-border-brand-orange/20 focus:border-border-brand-orange transition-colors disabled:cursor-not-allowed disabled:opacity-50",
          className,
        )}
        ref={ref}
        {...props}
      />
    );
  },
);
Input.displayName = "Input";

export { Input };
