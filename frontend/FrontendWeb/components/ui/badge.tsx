import * as React from "react";
import { cn } from "@/lib/utils";

export interface BadgeProps extends React.HTMLAttributes<HTMLSpanElement> {
  variant?: string;
}

// components/ui/badge.tsx
const VARIANT_STYLES: Record<string, string> = {
  default:     "bg-secondary text-secondary-foreground border-border",
  primary:     "bg-primary text-primary-foreground border-transparent",
  success:     "bg-success/15 text-success border-success/40",
  info:        "bg-info/15 text-info border-info/40",
  warning:     "bg-warning/15 text-warning border-warning/40",
  destructive: "bg-destructive/15 text-destructive border-destructive/40",
};

export const Badge = React.forwardRef<HTMLSpanElement, BadgeProps>(
  ({ className, variant = "default", ...props }, ref) => {
    const styles = VARIANT_STYLES[variant] ?? VARIANT_STYLES.default;
    return (
      <span
        ref={ref}
        className={cn(
          "inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-semibold",
          styles,
          className
        )}
        {...props}
      />
    );
  }
);
Badge.displayName = "Badge";

export default Badge;
