import { createSlice, nanoid, type PayloadAction } from "@reduxjs/toolkit";
import type { TransferQuoteDto } from "@/api/bankApi";
import type { FieldError } from "@/api/problem";
import { sessionEnded } from "@/api/baseApi";

/** The form as the customer filled it in, kept so Edit and "Get a new quote" can reuse it. */
export interface TransferFormValues {
  sourceAccountId: string;
  /** "account:<id>" or "payee:<id>" */
  destination: string;
  amount: string;
  note: string;
}

export interface TransferDraftState {
  form: TransferFormValues | null;
  quote: TransferQuoteDto | null;
  /** One key per quote. It survives retries and refreshes so the API can replay instead of re-posting. */
  idempotencyKey: string | null;
  /** Rule errors from executing the quote, shown on the form when the customer is sent back. */
  errors: FieldError[];
  /** A form-level message for the same trip back (for example, an expired quote). */
  notice: string | null;
}

export const emptyDraft: TransferDraftState = {
  form: null,
  quote: null,
  idempotencyKey: null,
  errors: [],
  notice: null,
};

function newKey(): string {
  return typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID() : nanoid();
}

export const transferDraftSlice = createSlice({
  name: "transferDraft",
  initialState: emptyDraft,
  reducers: {
    quoteReceived: {
      reducer(
        state,
        action: PayloadAction<{ quote: TransferQuoteDto; form: TransferFormValues; key: string }>,
      ) {
        state.quote = action.payload.quote;
        state.form = action.payload.form;
        state.idempotencyKey = action.payload.key;
        state.errors = [];
        state.notice = null;
      },
      // The key is generated here, outside the reducer, so the reducer stays pure.
      prepare(payload: { quote: TransferQuoteDto; form: TransferFormValues }) {
        return { payload: { ...payload, key: newKey() } };
      },
    },
    /** Back to the form with its values, dropping the quote and its key. */
    quoteDiscarded(
      state,
      action: PayloadAction<{ errors?: FieldError[]; notice?: string } | undefined>,
    ) {
      state.quote = null;
      state.idempotencyKey = null;
      state.errors = action.payload?.errors ?? [];
      state.notice = action.payload?.notice ?? null;
    },
    /** The form has shown the errors and notice it was sent back with. */
    feedbackShown(state) {
      state.errors = [];
      state.notice = null;
    },
    draftCleared: () => emptyDraft,
  },
  extraReducers: (builder) => {
    builder.addCase(sessionEnded, () => emptyDraft);
  },
  selectors: {
    selectTransferDraft: (state) => state,
  },
});

export const { quoteReceived, quoteDiscarded, feedbackShown, draftCleared } =
  transferDraftSlice.actions;
export const { selectTransferDraft } = transferDraftSlice.selectors;

export const DRAFT_STORAGE_KEY = "obk.transfer";

export function loadDraft(): TransferDraftState | undefined {
  try {
    const raw = sessionStorage.getItem(DRAFT_STORAGE_KEY);
    if (!raw) return undefined;
    const parsed = JSON.parse(raw) as TransferDraftState;
    return parsed && typeof parsed === "object" && "quote" in parsed
      ? { ...emptyDraft, ...parsed }
      : undefined;
  } catch {
    return undefined;
  }
}

export function saveDraft(draft: TransferDraftState) {
  try {
    if (draft.form || draft.quote) sessionStorage.setItem(DRAFT_STORAGE_KEY, JSON.stringify(draft));
    else sessionStorage.removeItem(DRAFT_STORAGE_KEY);
  } catch {
    // Storage can be unavailable (private mode, quota); the draft then lasts only for this page.
  }
}
