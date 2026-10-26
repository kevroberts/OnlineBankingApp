"use client";

import Link from "next/link";
import { useListTransfersInfiniteQuery } from "@/api/bankApi";
import { isApiProblem } from "@/api/problem";
import { ErrorPanel } from "@/components/ErrorPanel";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { formatDateTime } from "@/lib/dates";
import { formatMoney } from "@/lib/money";

export function TransferList() {
  const { data, error, isLoading, hasNextPage, isFetchingNextPage, fetchNextPage, refetch } =
    useListTransfersInfiniteQuery();

  if (isLoading) {
    return (
      <div className="flex flex-col gap-3" aria-busy="true" aria-label="Loading transfers">
        {Array.from({ length: 5 }, (_, i) => (
          <Skeleton key={i} className="h-14" />
        ))}
      </div>
    );
  }
  if (!data) return isApiProblem(error) ? <ErrorPanel problem={error} onRetry={refetch} /> : null;

  const items = data.pages.flatMap((p) => p.items);
  if (items.length === 0) {
    return (
      <div className="flex flex-col items-start gap-3 rounded-lg border border-dashed p-6">
        <p className="font-medium">No transfers yet</p>
        <Button asChild size="sm">
          <Link href="/transfer">Make a transfer</Link>
        </Button>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-3">
      <ul className="divide-y rounded-xl border bg-card" aria-label="Recent transfers">
        {items.map((t) => (
          <li key={t.id}>
            <Link
              href={`/transfers/${t.id}`}
              className="flex items-start justify-between gap-4 px-4 py-3 hover:bg-accent"
            >
              <div className="flex min-w-0 flex-col">
                <span className="font-medium">
                  {t.source.name} → {t.destination.name}
                </span>
                <span className="truncate text-sm text-muted-foreground">
                  {formatDateTime(t.createdAt)} · {t.reference}
                  {t.note ? ` · ${t.note}` : ""}
                </span>
              </div>
              <span className="shrink-0 font-medium tabular-nums">{formatMoney(t.amount)}</span>
            </Link>
          </li>
        ))}
      </ul>
      {hasNextPage && (
        <div className="flex justify-center">
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
