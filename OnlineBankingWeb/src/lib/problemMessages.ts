import { NETWORK_ERROR, type ApiProblem } from "@/api/problem";

/** Customer-facing copy for every code in the API's error table. Unknown codes fall back to `detail`. */
const MESSAGES: Record<string, string> = {
  bad_request: "Something about that request wasn't right. Please try again.",
  invalid_date_range: "The start date must be on or before the end date.",
  invalid_cursor: "That page is no longer available. Showing the newest transactions.",
  idempotency_key_required: "Something went wrong preparing the transfer. Please start again.",
  unauthenticated: "Your session has ended. Please sign in again.",
  invalid_credentials: "That username and password don't match. Check them and try again.",
  account_locked: "Too many failed attempts. Your sign-in is locked for a few minutes.",
  rate_limited: "Too many attempts. Please wait a moment and try again.",
  not_found: "We couldn't find that.",
  quote_already_used: "This transfer has already been sent.",
  request_in_progress: "Your transfer is still being processed.",
  validation_failed: "Please fix the highlighted fields.",
  internal_error: "Something went wrong on our side. Please try again.",
  [NETWORK_ERROR]: "We couldn't reach the bank. Check your connection and try again.",

  // Rule codes (errors[].code)
  amount_not_positive: "Enter an amount greater than zero.",
  amount_invalid_precision: "Amounts can have at most 2 decimal places.",
  destination_not_found: "Choose where the money should go.",
  same_account: "Choose a different account to transfer to.",
  insufficient_funds: "The amount is more than the available balance.",
  account_not_active: "This account can't be used for transfers right now.",
  note_invalid: "Notes can be up to 140 characters, with no line breaks or control characters.",
  quote_expired: "That quote expired. Review and confirm again.",
  idempotency_key_reused: "Something went wrong with this transfer. Please start again.",
};

export function messageForCode(code: string, fallback?: string): string {
  return MESSAGES[code] ?? fallback ?? "Something went wrong. Please try again.";
}

export function problemMessage(problem: ApiProblem): string {
  if (problem.code === "rate_limited" && problem.retryAfter) {
    const s = problem.retryAfter;
    return `Too many attempts. Try again in ${s} second${s === 1 ? "" : "s"}.`;
  }
  return messageForCode(problem.code, problem.detail);
}
