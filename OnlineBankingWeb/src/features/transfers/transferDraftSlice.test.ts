import { describe, expect, it } from "vitest";
import type { TransferQuoteDto } from "@/api/bankApi";
import { sessionEnded } from "@/api/baseApi";
import {
  draftCleared,
  emptyDraft,
  feedbackShown,
  quoteDiscarded,
  quoteReceived,
  transferDraftSlice,
  type TransferFormValues,
} from "./transferDraftSlice";

const reducer = transferDraftSlice.reducer;
const form: TransferFormValues = {
  sourceAccountId: "a",
  destination: "account:b",
  amount: "10",
  note: "",
};
const quote = { quoteId: "q1", expiresAt: "2026-10-25T14:05:00Z" } as TransferQuoteDto;

describe("transferDraftSlice", () => {
  it("stores the quote and the form with a fresh Idempotency-Key", () => {
    const state = reducer(emptyDraft, quoteReceived({ quote, form }));
    expect(state.quote).toBe(quote);
    expect(state.form).toEqual(form);
    expect(state.idempotencyKey).toMatch(/^[0-9a-f-]{36}$/);
  });

  it("gives every new quote a new key", () => {
    const first = reducer(emptyDraft, quoteReceived({ quote, form }));
    const second = reducer(first, quoteReceived({ quote: { ...quote, quoteId: "q2" }, form }));
    expect(second.idempotencyKey).not.toBe(first.idempotencyKey);
  });

  it("keeps the form but drops the quote and key when sent back, with any errors", () => {
    const withQuote = reducer(emptyDraft, quoteReceived({ quote, form }));
    const errors = [{ field: "amount", code: "insufficient_funds", message: "x" }];
    const state = reducer(withQuote, quoteDiscarded({ errors }));
    expect(state).toEqual({ ...emptyDraft, form, errors });
    expect(reducer(state, feedbackShown()).errors).toEqual([]);
  });

  it("clears on draftCleared and when the session ends", () => {
    const withQuote = reducer(emptyDraft, quoteReceived({ quote, form }));
    expect(reducer(withQuote, draftCleared())).toEqual(emptyDraft);
    expect(reducer(withQuote, sessionEnded())).toEqual(emptyDraft);
  });
});
