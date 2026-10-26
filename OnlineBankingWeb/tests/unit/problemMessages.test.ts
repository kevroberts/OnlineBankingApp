import { describe, expect, it } from "vitest";
import { messageForCode, problemMessage } from "@/lib/problemMessages";

// Every code in the API's error table (Infrastructure/ApiProblem.cs, ProblemCodes).
const API_CODES = [
  "bad_request",
  "invalid_date_range",
  "invalid_cursor",
  "idempotency_key_required",
  "unauthenticated",
  "invalid_credentials",
  "not_found",
  "quote_already_used",
  "request_in_progress",
  "validation_failed",
  "account_locked",
  "rate_limited",
  "internal_error",
  "amount_not_positive",
  "amount_invalid_precision",
  "destination_not_found",
  "same_account",
  "insufficient_funds",
  "account_not_active",
  "note_invalid",
  "quote_expired",
  "idempotency_key_reused",
];

describe("problem messages", () => {
  it.each(API_CODES)("has customer-facing copy for %s", (code) => {
    expect(messageForCode(code, "FALLBACK")).not.toBe("FALLBACK");
  });

  it("falls back to detail for unknown codes", () => {
    expect(messageForCode("something_new", "Detail from the API")).toBe("Detail from the API");
  });

  it("includes the Retry-After wait for rate limiting", () => {
    expect(
      problemMessage({ status: 429, code: "rate_limited", errors: [], retryAfter: 42 }),
    ).toMatch(/42 seconds/);
  });
});
