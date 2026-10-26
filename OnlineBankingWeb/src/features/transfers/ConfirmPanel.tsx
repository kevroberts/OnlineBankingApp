"use client";

import { AlertCircle, Clock } from "lucide-react";
import { useRouter } from "next/navigation";
import { useEffect, useRef } from "react";
import { useCreateTransferQuoteMutation, useExecuteTransferMutation } from "@/api/bankApi";
import { isApiProblem } from "@/api/problem";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardFooter } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { formatMoney } from "@/lib/money";
import { messageForCode, problemMessage } from "@/lib/problemMessages";
import { useHydrated } from "@/lib/useHydrated";
import { useAppDispatch, useAppSelector } from "@/store/hooks";
import { toQuoteRequest } from "./quoteRequest";
import {
  draftCleared,
  quoteDiscarded,
  quoteReceived,
  selectTransferDraft,
} from "./transferDraftSlice";
import { formatCountdown, useCountdown } from "./useCountdown";

export function ConfirmPanel() {
  const hydrated = useHydrated();
  const router = useRouter();
  const dispatch = useAppDispatch();
  const { quote, form, idempotencyKey } = useAppSelector(selectTransferDraft);
  const [execute, { isLoading: executing, error: executeError, reset }] =
    useExecuteTransferMutation();
  const [requote, { isLoading: requoting, error: requoteError }] = useCreateTransferQuoteMutation();
  const secondsLeft = useCountdown(quote?.expiresAt);
  const pending = useRef(false);
  // Set once this screen has decided where to go next, so the no-draft redirect stays out of the way.
  const leaving = useRef(false);
  const heading = useRef<HTMLHeadingElement>(null);

  // With no draft (direct visit, or a draft cleared in another step) there is nothing to confirm.
  useEffect(() => {
    if (hydrated && (!quote || !idempotencyKey) && !leaving.current) router.replace("/transfer");
  }, [hydrated, quote, idempotencyKey, router]);

  useEffect(() => {
    if (hydrated && quote) heading.current?.focus();
  }, [hydrated, quote]);

  if (!hydrated || !quote || !idempotencyKey) {
    return <Skeleton className="h-80" aria-label="Loading" />;
  }

  const expired = secondsLeft === 0;

  const onConfirm = async () => {
    // Double click: one request at a time. Retries inside the mutation reuse the same key and body.
    if (pending.current || executing) return;
    pending.current = true;
    try {
      const result = await execute({
        "Idempotency-Key": idempotencyKey,
        executeTransferRequest: { quoteId: quote.quoteId },
      });
      if ("data" in result && result.data) {
        leaving.current = true;
        dispatch(draftCleared());
        router.replace(`/transfers/${result.data.id}`);
        return;
      }
      const problem = result.error;
      if (!isApiProblem(problem)) return;
      if (problem.code === "quote_already_used" && problem.transferId) {
        leaving.current = true;
        dispatch(draftCleared());
        router.replace(`/transfers/${problem.transferId}`);
        return;
      }
      if (problem.status === 422) {
        leaving.current = true;
        const codes = problem.errors.map((e) => e.code);
        if (codes.includes("idempotency_key_reused")) {
          dispatch(draftCleared());
          dispatch(quoteDiscarded({ notice: messageForCode("idempotency_key_reused") }));
        } else if (codes.includes("quote_expired")) {
          dispatch(quoteDiscarded({ notice: messageForCode("quote_expired") }));
        } else {
          dispatch(quoteDiscarded({ errors: problem.errors }));
        }
        router.replace("/transfer");
      }
      // Anything else (network after retries, 500): stay here; Confirm again sends the same key.
    } finally {
      pending.current = false;
    }
  };

  const onRequote = async () => {
    if (!form) return;
    reset();
    const result = await requote({ transferQuoteRequest: toQuoteRequest(form) });
    // A new quote gets a new Idempotency-Key.
    if ("data" in result && result.data) dispatch(quoteReceived({ quote: result.data, form }));
  };

  const onEdit = () => {
    leaving.current = true;
    dispatch(quoteDiscarded());
    router.push("/transfer");
  };

  const onCancel = () => {
    leaving.current = true;
    dispatch(draftCleared());
    router.replace("/accounts");
  };

  const problem = isApiProblem(executeError)
    ? executeError
    : isApiProblem(requoteError)
      ? requoteError
      : null;
  const toPayee = quote.destination.kind === "payee";

  return (
    <div className="flex max-w-xl flex-col gap-4">
      <h1
        ref={heading}
        tabIndex={-1}
        className="text-2xl font-semibold tracking-tight outline-none"
      >
        Confirm your transfer
      </h1>
      {problem && (
        <Alert variant="destructive">
          <AlertCircle aria-hidden />
          <AlertDescription>
            {problemMessage(problem)}
            {problem.status === 0 &&
              " Confirming again is safe: the transfer will only happen once."}
          </AlertDescription>
        </Alert>
      )}
      <Card>
        <CardContent>
          <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-3 text-sm">
            <dt className="text-muted-foreground">From</dt>
            <dd>
              {quote.source.name} {quote.source.maskedNumber}
            </dd>
            <dt className="text-muted-foreground">To</dt>
            <dd>
              {quote.destination.name} {quote.destination.maskedNumber}
              {toPayee && <span className="text-muted-foreground"> (payee)</span>}
            </dd>
            <dt className="text-muted-foreground">Amount</dt>
            <dd className="text-lg font-semibold tabular-nums">{formatMoney(quote.amount)}</dd>
            <dt className="text-muted-foreground">Fee</dt>
            <dd className="tabular-nums">{formatMoney(quote.fee)}</dd>
            {quote.note && (
              <>
                <dt className="text-muted-foreground">Note</dt>
                <dd className="break-words">{quote.note}</dd>
              </>
            )}
            <dt className="text-muted-foreground">{quote.source.name} after</dt>
            <dd className="tabular-nums">{formatMoney(quote.sourceBalanceAfter)}</dd>
          </dl>
        </CardContent>
        <CardFooter className="flex-col items-stretch gap-3 sm:flex-row sm:items-center">
          <p
            className="flex items-center gap-1.5 text-sm text-muted-foreground sm:mr-auto"
            aria-live="polite"
          >
            <Clock aria-hidden className="size-4" />
            {expired ? (
              "This quote has expired."
            ) : (
              <>This quote expires in {formatCountdown(secondsLeft)}</>
            )}
          </p>
          <Button variant="ghost" onClick={onCancel}>
            Cancel
          </Button>
          <Button variant="outline" onClick={onEdit}>
            Edit
          </Button>
          {expired ? (
            <Button onClick={onRequote} disabled={requoting}>
              {requoting ? "Getting a new quote…" : "Get a new quote"}
            </Button>
          ) : (
            <Button onClick={onConfirm} disabled={executing}>
              {executing ? "Sending…" : "Confirm transfer"}
            </Button>
          )}
        </CardFooter>
      </Card>
    </div>
  );
}
