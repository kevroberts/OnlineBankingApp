"use client";

import { useListAccountsQuery } from "@/api/bankApi";
import { isApiProblem } from "@/api/problem";
import { ErrorPanel } from "@/components/ErrorPanel";
import { PageHeader } from "@/components/PageHeader";
import { Skeleton } from "@/components/ui/skeleton";
import { formatDateTime } from "@/lib/dates";
import { formatMoney } from "@/lib/money";
import { AccountCard } from "./AccountCard";

export function AccountsSummary() {
  const { data, error, isLoading, refetch } = useListAccountsQuery();

  if (isLoading) {
    return (
      <div className="flex flex-col gap-4" aria-busy="true" aria-label="Loading accounts">
        <Skeleton className="h-16 w-64" />
        <div className="grid gap-4 md:grid-cols-2">
          <Skeleton className="h-44" />
          <Skeleton className="h-44" />
        </div>
      </div>
    );
  }
  if (!data) return isApiProblem(error) ? <ErrorPanel problem={error} onRetry={refetch} /> : null;

  const currency = data.items[0]?.currency ?? "USD";
  const asOf = data.items[0]?.asOf;

  return (
    <>
      <PageHeader
        title="Your accounts"
        description={asOf ? <>Balances as of {formatDateTime(asOf)}</> : undefined}
      />
      <section aria-labelledby="total-heading" className="mb-6">
        <h2 id="total-heading" className="text-sm text-muted-foreground">
          Total balance
        </h2>
        <p className="text-4xl font-semibold tabular-nums">
          {formatMoney(data.totalBalance, currency)}
        </p>
      </section>
      <ul className="grid gap-4 md:grid-cols-2" aria-label="Accounts">
        {data.items.map((account) => (
          <li key={account.id}>
            <AccountCard account={account} />
          </li>
        ))}
      </ul>
    </>
  );
}
