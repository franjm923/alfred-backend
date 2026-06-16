// components/ui/avatar.tsx
import * as React from "react";
import { cn } from "@/lib/utils";

export function Avatar(props: React.HTMLAttributes<HTMLDivElement>) {
  const { className, ...rest } = props;
  return (
    <div
      className={cn("relative inline-flex h-10 w-10 overflow-hidden rounded-full bg-neutral-800", className)}
      {...rest}
    />
  );
}

export function AvatarImage(props: React.ComponentProps<"img">) {
  const { className, ...rest } = props;
  return <img className={cn("h-full w-full object-cover", className)} alt={rest.alt ?? ""} {...rest} />;
}

export function AvatarFallback(props: React.HTMLAttributes<HTMLSpanElement>) {
  const { className, ...rest } = props;
  return (
    <span className={cn("flex h-full w-full items-center justify-center text-sm text-neutral-300", className)} {...rest} />
  );
}

// Permite usar tanto `import { Avatar } ...` como `import Avatar ...`
export default Avatar;
