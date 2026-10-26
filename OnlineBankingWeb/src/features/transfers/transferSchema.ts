import { z } from "zod";
import type { AccountDto } from "@/api/bankApi";
import { AMOUNT_PATTERN, toNumber } from "@/lib/money";

export const MAX_NOTE_LENGTH = 140;

// Same rule as the API: no control characters (which includes line breaks and tabs).
const CONTROL_CHARS = /[\u0000-\u001f\u007f-\u009f]/;

/**
 * Mirrors the API's transfer rules for instant feedback. The API stays the authority: it re-checks
 * everything when quoting and again, under row locks, when executing.
 */
export function makeTransferSchema(accounts: readonly Pick<AccountDto, "id" | "balance">[]) {
  return z
    .object({
      sourceAccountId: z.string().min(1, "Choose the account to send from."),
      destination: z.string().min(1, "Choose where the money should go."),
      amount: z
        .string()
        .trim()
        .min(1, "Enter an amount greater than zero.")
        .regex(AMOUNT_PATTERN, "Enter an amount like 25 or 25.50, with at most 2 decimal places.")
        .refine(
          (v) => !AMOUNT_PATTERN.test(v) || Number(v) > 0,
          "Enter an amount greater than zero.",
        ),
      note: z
        .string()
        .max(MAX_NOTE_LENGTH, `Notes can be up to ${MAX_NOTE_LENGTH} characters.`)
        .refine(
          (v) => !CONTROL_CHARS.test(v),
          "Notes can't contain line breaks or control characters.",
        ),
    })
    .superRefine((values, ctx) => {
      if (values.destination === `account:${values.sourceAccountId}`) {
        ctx.addIssue({
          code: "custom",
          path: ["destination"],
          message: "Choose a different account to transfer to.",
        });
      }
      const source = accounts.find((a) => a.id === values.sourceAccountId);
      if (
        source &&
        AMOUNT_PATTERN.test(values.amount) &&
        Number(values.amount) > toNumber(source.balance)
      ) {
        ctx.addIssue({
          code: "custom",
          path: ["amount"],
          message: "The amount is more than the available balance.",
        });
      }
    });
}

export type TransferSchemaValues = z.infer<ReturnType<typeof makeTransferSchema>>;

/** Maps an API field name from errors[].field onto the form field that shows it. */
export function formFieldFor(apiField: string | null): keyof TransferSchemaValues | null {
  switch (apiField) {
    case "amount":
      return "amount";
    case "destination":
    case "destination.id":
    case "destination.kind":
      return "destination";
    case "sourceAccountId":
      return "sourceAccountId";
    case "note":
      return "note";
    default:
      return null;
  }
}

export function parseDestination(value: string): { kind: "account" | "payee"; id: string } {
  const [kind, id = ""] = value.split(":");
  return { kind: kind === "payee" ? "payee" : "account", id };
}
