import type { TransferQuoteRequest } from "@/api/bankApi";
import type { TransferFormValues } from "./transferDraftSlice";
import { parseDestination } from "./transferSchema";

/** Typed as a string, sent as a number; the note is null when empty. */
export function toQuoteRequest(form: TransferFormValues): TransferQuoteRequest {
  return {
    sourceAccountId: form.sourceAccountId,
    destination: parseDestination(form.destination),
    amount: Number(form.amount.trim()),
    note: form.note.trim() === "" ? null : form.note,
  };
}
