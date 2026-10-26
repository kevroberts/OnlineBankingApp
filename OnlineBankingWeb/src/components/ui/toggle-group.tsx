"use client";

import { ToggleGroup as ToggleGroupPrimitive } from "radix-ui";
import type { ComponentProps } from "react";
import { cn } from "@/lib/utils";

/** Segmented control. */
export function ToggleGroup({
  className,
  ...props
}: ComponentProps<typeof ToggleGroupPrimitive.Root>) {
  return (
    <ToggleGroupPrimitive.Root
      className={cn(
        "inline-flex w-fit items-center rounded-md border bg-card p-0.5 shadow-xs",
        className,
      )}
      {...props}
    />
  );
}

export function ToggleGroupItem({
  className,
  ...props
}: ComponentProps<typeof ToggleGroupPrimitive.Item>) {
  return (
    <ToggleGroupPrimitive.Item
      className={cn(
        "inline-flex h-8 items-center justify-center rounded-sm px-3 text-sm font-medium text-muted-foreground outline-none",
        "hover:text-foreground focus-visible:ring-[3px] focus-visible:ring-ring/50",
        "data-[state=on]:bg-secondary data-[state=on]:text-secondary-foreground data-[state=on]:shadow-xs",
        className,
      )}
      {...props}
    />
  );
}
