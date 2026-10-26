"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { AlertCircle } from "lucide-react";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import {
  useCreateTransferQuoteMutation,
  useListAccountsQuery,
  useListPayeesQuery,
  type AccountDto,
  type PayeeDto,
} from "@/api/bankApi";
import { isApiProblem, type ApiProblem, type FieldError } from "@/api/problem";
import { ErrorPanel } from "@/components/ErrorPanel";
import { FormField } from "@/components/FormField";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { formatMoney } from "@/lib/money";
import { messageForCode, problemMessage } from "@/lib/problemMessages";
import { useHydrated } from "@/lib/useHydrated";
import { useAppDispatch, useAppSelector } from "@/store/hooks";
import { toQuoteRequest } from "./quoteRequest";
import {
  feedbackShown,
  quoteReceived,
  selectTransferDraft,
  type TransferFormValues,
} from "./transferDraftSlice";
import { formFieldFor, makeTransferSchema, MAX_NOTE_LENGTH } from "./transferSchema";

const EMPTY: TransferFormValues = { sourceAccountId: "", destination: "", amount: "", note: "" };

export function TransferForm() {
  const hydrated = useHydrated();
  const accounts = useListAccountsQuery();
  const payees = useListPayeesQuery();

  if (!hydrated || accounts.isLoading || payees.isLoading) {
    return (
      <div className="flex flex-col gap-4" aria-busy="true" aria-label="Loading">
        <Skeleton className="h-10" />
        <Skeleton className="h-10" />
        <Skeleton className="h-10" />
      </div>
    );
  }
  const problem = accounts.error ?? payees.error;
  if (!accounts.data || !payees.data) {
    return isApiProblem(problem) ? (
      <ErrorPanel
        problem={problem}
        onRetry={() => {
          void accounts.refetch();
          void payees.refetch();
        }}
      />
    ) : null;
  }
  return <TransferFormInner accounts={accounts.data.items} payees={payees.data.items} />;
}

function TransferFormInner({ accounts, payees }: { accounts: AccountDto[]; payees: PayeeDto[] }) {
  const router = useRouter();
  const params = useSearchParams();
  const dispatch = useAppDispatch();
  const draft = useAppSelector(selectTransferDraft);
  const [createQuote, { isLoading, error }] = useCreateTransferQuoteMutation();

  const schema = useMemo(() => makeTransferSchema(accounts), [accounts]);

  // Edit (and a trip back from the confirmation screen) prefills from the draft; ?from= preselects the source.
  const defaults = useMemo<TransferFormValues>(() => {
    if (draft.form) return draft.form;
    const from = params.get("from");
    return {
      ...EMPTY,
      sourceAccountId: accounts.some((a) => a.id === from) ? from! : (accounts[0]?.id ?? ""),
    };
    // Only on first render: later draft changes come from this form itself.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const {
    register,
    control,
    handleSubmit,
    setError,
    setValue,
    getValues,
    formState: { errors },
  } = useForm<TransferFormValues>({ resolver: zodResolver(schema), defaultValues: defaults });

  const sourceId = useWatch({ control, name: "sourceAccountId" });
  const note = useWatch({ control, name: "note" }) ?? "";
  const source = accounts.find((a) => a.id === sourceId);

  // The source can't also be the destination.
  useEffect(() => {
    if (getValues("destination") === `account:${sourceId}`) setValue("destination", "");
  }, [sourceId, getValues, setValue]);

  const applyServerErrors = (fieldErrors: FieldError[]) => {
    let mapped = 0;
    for (const e of fieldErrors) {
      const field = formFieldFor(e.field);
      if (field) {
        setError(field, { type: "server", message: messageForCode(e.code, e.message) });
        mapped++;
      }
    }
    return mapped;
  };

  // Errors the confirmation screen sent back with (for example, insufficient funds at execute time).
  const sentBack = draft.errors;
  useEffect(() => {
    if (sentBack.length > 0) applyServerErrors(sentBack);
    if (sentBack.length > 0 || draft.notice) {
      // Keep the notice on screen for this visit, but don't show it again on the next one.
      const t = setTimeout(() => dispatch(feedbackShown()), 0);
      return () => clearTimeout(t);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
  const [notice] = useState(() => draft.notice);

  const onSubmit = handleSubmit(async (form) => {
    const result = await createQuote({ transferQuoteRequest: toQuoteRequest(form) });
    if ("data" in result && result.data) {
      dispatch(quoteReceived({ quote: result.data, form }));
      router.push("/transfer/confirm");
    } else if (isApiProblem(result.error) && result.error.status === 422) {
      applyServerErrors(result.error.errors);
    }
  });

  const problem = isApiProblem(error) ? (error as ApiProblem) : null;
  const formLevelProblem =
    problem && !(problem.status === 422 && problem.errors.some((e) => formFieldFor(e.field)));
  const otherAccounts = accounts.filter((a) => a.id !== sourceId);

  return (
    <Card>
      <CardContent>
        <form onSubmit={onSubmit} noValidate className="flex flex-col gap-5">
          {notice && !problem && (
            <Alert>
              <AlertCircle aria-hidden />
              <AlertDescription>{notice}</AlertDescription>
            </Alert>
          )}
          {formLevelProblem && (
            <Alert variant="destructive">
              <AlertCircle aria-hidden />
              <AlertDescription>{problemMessage(problem)}</AlertDescription>
            </Alert>
          )}

          <FormField
            id="source"
            label="From"
            error={errors.sourceAccountId?.message}
            hint={
              source ? (
                <>Available balance {formatMoney(source.availableBalance, source.currency)}</>
              ) : undefined
            }
          >
            {(aria) => (
              <Controller
                control={control}
                name="sourceAccountId"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger {...aria} onBlur={field.onBlur} ref={field.ref}>
                      <SelectValue placeholder="Choose an account" />
                    </SelectTrigger>
                    <SelectContent>
                      {accounts.map((a) => (
                        <SelectItem key={a.id} value={a.id}>
                          {a.name} {a.maskedNumber} · {formatMoney(a.balance, a.currency)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
            )}
          </FormField>

          <FormField id="destination" label="To" error={errors.destination?.message}>
            {(aria) => (
              <Controller
                control={control}
                name="destination"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger {...aria} onBlur={field.onBlur} ref={field.ref}>
                      <SelectValue placeholder="Choose an account or payee" />
                    </SelectTrigger>
                    <SelectContent>
                      {otherAccounts.length > 0 && (
                        <SelectGroup>
                          <SelectLabel>My accounts</SelectLabel>
                          {otherAccounts.map((a) => (
                            <SelectItem key={a.id} value={`account:${a.id}`}>
                              {a.name} {a.maskedNumber}
                            </SelectItem>
                          ))}
                        </SelectGroup>
                      )}
                      {payees.length > 0 && (
                        <SelectGroup>
                          <SelectLabel>Payees</SelectLabel>
                          {payees.map((p) => (
                            <SelectItem key={p.id} value={`payee:${p.id}`}>
                              {p.nickname} · {p.bankName} {p.maskedAccountNumber}
                            </SelectItem>
                          ))}
                        </SelectGroup>
                      )}
                    </SelectContent>
                  </Select>
                )}
              />
            )}
          </FormField>

          <FormField id="amount" label="Amount" error={errors.amount?.message}>
            {(aria) => (
              <div className="relative">
                <span
                  aria-hidden
                  className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-muted-foreground"
                >
                  $
                </span>
                <Input
                  {...aria}
                  inputMode="decimal"
                  autoComplete="off"
                  placeholder="0.00"
                  className="pl-7 tabular-nums"
                  {...register("amount")}
                />
              </div>
            )}
          </FormField>

          <FormField
            id="note"
            label="Note (optional)"
            error={errors.note?.message}
            hint={
              <span aria-live="polite">
                {note.length}/{MAX_NOTE_LENGTH} characters
              </span>
            }
          >
            {(aria) => (
              <Textarea {...aria} rows={2} maxLength={MAX_NOTE_LENGTH + 20} {...register("note")} />
            )}
          </FormField>

          <div className="flex flex-wrap gap-2">
            <Button type="submit" disabled={isLoading}>
              {isLoading ? "Checking…" : "Review transfer"}
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}
