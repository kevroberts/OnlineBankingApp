"use client";

import { CheckCircle2 } from "lucide-react";
import Link from "next/link";
import { notFound, useParams } from "next/navigation";
import { useGetTransferQuery } from "@/api/bankApi";
import { isApiProblem } from "@/api/problem";
import { ErrorPanel } from "@/components/ErrorPanel";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardFooter, CardHeader } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { formatDateTime } from "@/lib/dates";
import { formatMoney } from "@/lib/money";

/** Reads the finished transfer by id, so refresh and Back are harmless. */
export function ResultPanel() {
  const { transferId } = useParams<{ transferId: string }>();
  const { data: transfer, error, isLoading, refetch } = useGetTransferQuery({ transferId });

  if (isApiProblem(error) && (error.status === 404 || error.status === 400)) notFound();
  if (isLoading) return <Skeleton className="h-72 max-w-xl" aria-label="Loading" />;
  if (!transfer)
    return isApiProblem(error) ? <ErrorPanel problem={error} onRetry={refetch} /> : null;

  const toAccount = transfer.destination.kind === "account";
  const destinationAfter = transfer.destination.balanceAfter;

  return (
    <Card className="max-w-xl">
      <CardHeader>
        <div role="status" className="flex items-start gap-3">
          <CheckCircle2 aria-hidden className="mt-0.5 size-6 shrink-0 text-success" />
          <div className="flex flex-col gap-1">
            <h1 className="text-xl font-semibold">
              {formatMoney(transfer.amount)} sent to {transfer.destination.name}
            </h1>
            <p className="text-sm text-muted-foreground">
              Reference <span className="font-mono text-foreground">{transfer.reference}</span>
            </p>
          </div>
        </div>
      </CardHeader>
      <CardContent>
        <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-3 text-sm">
          <dt className="text-muted-foreground">Date</dt>
          <dd>{formatDateTime(transfer.createdAt)}</dd>
          <dt className="text-muted-foreground">From</dt>
          <dd>
            {transfer.source.name} {transfer.source.maskedNumber}
          </dd>
          <dt className="text-muted-foreground">To</dt>
          <dd>
            {transfer.destination.name} {transfer.destination.maskedNumber}
          </dd>
          {transfer.note && (
            <>
              <dt className="text-muted-foreground">Note</dt>
              <dd className="break-words">{transfer.note}</dd>
            </>
          )}
          <dt className="text-muted-foreground">{transfer.source.name} balance after</dt>
          <dd className="tabular-nums">{formatMoney(transfer.source.balanceAfter)}</dd>
          {toAccount && destinationAfter != null && (
            <>
              <dt className="text-muted-foreground">{transfer.destination.name} balance after</dt>
              <dd className="tabular-nums">{formatMoney(destinationAfter)}</dd>
            </>
          )}
        </dl>
      </CardContent>
      <CardFooter className="flex-wrap">
        <Button asChild>
          <Link href={`/accounts/${transfer.source.id}`}>View account</Link>
        </Button>
        <Button asChild variant="outline">
          <Link href="/transfer">Make another transfer</Link>
        </Button>
      </CardFooter>
    </Card>
  );
}
