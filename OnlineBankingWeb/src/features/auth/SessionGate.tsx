"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { useGetCurrentCustomerQuery } from "@/api/bankApi";
import { isApiProblem } from "@/api/problem";
import { ErrorPanel } from "@/components/ErrorPanel";
import { Skeleton } from "@/components/ui/skeleton";
import { useAppSelector } from "@/store/hooks";
import { selectSessionEndedBy, selectSessionStatus } from "./sessionSlice";

/**
 * Loads the customer from GET /auth/me and renders protected screens only for a live session.
 * proxy.ts already turned away requests without a cookie; this catches a cookie the API rejects.
 */
export function SessionGate({ children }: { children: ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const search = useSearchParams();
  const status = useAppSelector(selectSessionStatus);
  const endedBy = useAppSelector(selectSessionEndedBy);
  const { error, refetch } = useGetCurrentCustomerQuery(undefined, {
    skip: status === "signedOut",
  });

  useEffect(() => {
    if (status !== "signedOut") return;
    if (endedBy === "user") {
      router.replace("/login");
    } else {
      const query = search.toString();
      const next = pathname + (query ? `?${query}` : "");
      router.replace(`/login?next=${encodeURIComponent(next)}`);
    }
  }, [status, endedBy, pathname, search, router]);

  if (status === "signedIn") return <>{children}</>;

  if (status === "unknown" && isApiProblem(error) && error.status !== 401) {
    return <ErrorPanel problem={error} onRetry={refetch} />;
  }

  return (
    <div className="flex flex-col gap-4" aria-busy="true" aria-label="Loading">
      <Skeleton className="h-8 w-48" />
      <div className="grid gap-4 md:grid-cols-2">
        <Skeleton className="h-36" />
        <Skeleton className="h-36" />
      </div>
    </div>
  );
}
