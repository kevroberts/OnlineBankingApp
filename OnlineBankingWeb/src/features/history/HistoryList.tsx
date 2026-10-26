"use client";

import { Info } from "lucide-react";
import { useEffect, useRef } from "react";
import { useListTransactionsInfiniteQuery } from "@/api/bankApi";
import { isApiProblem } from "@/api/problem";
import { ErrorPanel } from "@/components/ErrorPanel";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { problemMessage } from "@/lib/problemMessages";
import { HistoryListRow, HistoryTableRow } from "./HistoryRow";
import { useHistoryFilters } from "./useHistoryFilters";

const BALANCE_AFTER_HINT = "Your balance right after this transaction.";

export function HistoryList({ accountId, currency }: { accountId: string; currency: string }) {
  const { filters, active, clear } = useHistoryFilters();
  const {
    data,
    error,
    isLoading,
    isFetching,
    isFetchingNextPage,
    hasNextPage,
    fetchNextPage,
    refetch,
  } = useListTransactionsInfiniteQuery({
    accountId,
    from: filters.from ?? undefined,
    to: filters.to ?? undefined,
    type: filters.type ?? undefined,
    q: filters.q ?? undefined,
  });

  // "Load more" also loads when it scrolls into view.
  const sentinel = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const el = sentinel.current;
    if (!el || !hasNextPage || typeof IntersectionObserver === "undefined") return;
    const observer = new IntersectionObserver((entries) => {
      if (entries.some((e) => e.isIntersecting) && !isFetching) void fetchNextPage();
    });
    observer.observe(el);
    return () => observer.disconnect();
  }, [hasNextPage, isFetching, fetchNextPage]);

  if (isLoading) {
    return (
      <div className="flex flex-col gap-3" aria-busy="true" aria-label="Loading transactions">
        {Array.from({ length: 6 }, (_, i) => (
          <Skeleton key={i} className="h-12" />
        ))}
      </div>
    );
  }

  if (!data) {
    if (isApiProblem(error) && error.status === 400) {
      return (
        <Alert variant="destructive">
          <AlertDescription>
            {problemMessage(error)}
            <Button variant="link" className="h-auto p-0" onClick={() => void clear()}>
              Clear filters
            </Button>
          </AlertDescription>
        </Alert>
      );
    }
    return isApiProblem(error) ? <ErrorPanel problem={error} onRetry={refetch} /> : null;
  }

  const items = data.pages.flatMap((p) => p.items);
  const total = Number(data.pages[0]?.totalCount ?? 0);

  if (items.length === 0) {
    return active ? (
      <div className="flex flex-col items-start gap-2 rounded-lg border border-dashed p-6">
        <p className="font-medium">No transactions match these filters</p>
        <Button variant="outline" size="sm" onClick={() => void clear()}>
          Clear
        </Button>
      </div>
    ) : (
      <div className="rounded-lg border border-dashed p-6">
        <p className="font-medium">No transactions yet</p>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-sm text-muted-foreground" aria-live="polite">
        {total.toLocaleString("en-US")} {total === 1 ? "result" : "results"}
      </p>

      <table className="hidden w-full border-collapse sm:table">
        <caption className="sr-only">Transactions, newest first</caption>
        <thead>
          <tr className="border-b text-left text-xs text-muted-foreground">
            <th scope="col" className="py-2 pr-4 font-medium">
              Date
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              Description
            </th>
            <th scope="col" className="py-2 pr-4 text-right font-medium">
              Amount
            </th>
            <th scope="col" className="py-2 text-right font-medium">
              <Tooltip>
                <TooltipTrigger className="inline-flex items-center gap-1 underline decoration-dotted underline-offset-4">
                  Balance after
                  <Info aria-hidden className="size-3.5" />
                </TooltipTrigger>
                <TooltipContent>{BALANCE_AFTER_HINT}</TooltipContent>
              </Tooltip>
            </th>
          </tr>
        </thead>
        <tbody>
          {items.map((tx) => (
            <HistoryTableRow key={tx.id} tx={tx} currency={currency} />
          ))}
        </tbody>
      </table>

      <ul className="sm:hidden" aria-label="Transactions, newest first">
        {items.map((tx) => (
          <HistoryListRow key={tx.id} tx={tx} currency={currency} />
        ))}
      </ul>

      {hasNextPage && (
        <div ref={sentinel} className="flex justify-center pt-2">
          <Button
            variant="outline"
            onClick={() => void fetchNextPage()}
            disabled={isFetchingNextPage}
          >
            {isFetchingNextPage ? "Loading…" : "Load more"}
          </Button>
        </div>
      )}
    </div>
  );
}
