"use client";

import { ArrowLeft, ArrowRightLeft } from "lucide-react";
import Link from "next/link";
import { notFound, useParams } from "next/navigation";
import { useGetAccountQuery } from "@/api/bankApi";
import { isApiProblem } from "@/api/problem";
import { ErrorPanel } from "@/components/ErrorPanel";
import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { formatDateTime } from "@/lib/dates";
import { formatMoney } from "@/lib/money";
import { HistoryFilters } from "./HistoryFilters";
import { HistoryList } from "./HistoryList";

export function AccountHistory() {
  const { accountId } = useParams<{ accountId: string }>();
  const { data: account, error, isLoading, refetch } = useGetAccountQuery({ accountId });

  if (isApiProblem(error) && (error.status === 404 || error.status === 400)) notFound();

  return (
    <div className="flex flex-col gap-6">
      <Button asChild variant="ghost" size="sm" className="-ml-3 w-fit">
        <Link href="/accounts">
          <ArrowLeft aria-hidden />
          All accounts
        </Link>
      </Button>

      {isLoading ? (
        <Skeleton className="h-20 w-72" />
      ) : account ? (
        <PageHeader
          title={account.name}
          description={
            <>
              {account.maskedNumber} · Balance{" "}
              <span className="font-medium text-foreground tabular-nums">
                {formatMoney(account.balance, account.currency)}
              </span>{" "}
              as of {formatDateTime(account.asOf)}
            </>
          }
          actions={
            <Button asChild variant="outline">
              <Link href={`/transfer?from=${account.id}`}>
                <ArrowRightLeft aria-hidden />
                Move money
              </Link>
            </Button>
          }
        />
      ) : isApiProblem(error) ? (
        <ErrorPanel problem={error} onRetry={refetch} />
      ) : null}

      <section aria-labelledby="history-heading" className="flex flex-col gap-4">
        <h2 id="history-heading" className="text-lg font-semibold">
          Transactions
        </h2>
        <HistoryFilters />
        <HistoryList accountId={accountId} currency={account?.currency ?? "USD"} />
      </section>
    </div>
  );
}
